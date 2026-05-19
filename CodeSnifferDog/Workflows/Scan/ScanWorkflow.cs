using CodeSnifferDog.Json;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.Scan;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Workflows.Scan;

public sealed class ScanWorkflow(
    Func<string, IAgentEventScope, AIAgent> scanAgentFactory,
    Func<string, IAgentEventScope, AIAgent> scanVerifierAgentFactory,
    IScanProjectStore scanProjectStore,
    ReviewVerdictBuffer verdictBuffer,
    PromptAssetReader? promptAssetReader = null,
    ScanWorkflowOptions? options = null,
    IAgentEventBus? agentEventBus = null)
{
    private readonly Func<string, IAgentEventScope, AIAgent> _scanAgentFactory = scanAgentFactory;
    private readonly Func<string, IAgentEventScope, AIAgent> _scanVerifierAgentFactory = scanVerifierAgentFactory;
    private readonly IScanProjectStore _scanProjectStore = scanProjectStore;
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly ScanWorkflowMessageTemplates _messageTemplates =
        new(promptAssetReader ?? new PromptAssetReader());
    private readonly ScanWorkflowOptions _options = options ?? new ScanWorkflowOptions();
    private readonly IAgentEventBus _agentEventBus = agentEventBus ?? NoOpAgentEventBus.Instance;

    public async Task<Result<ScanWorkflowResult>> RunAsync(
        string repositoryRootPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail("Repository root path is required.");

        repositoryRootPath = repositoryRootPath.Trim();
        await _scanProjectStore.ClearAsync(cancellationToken).ConfigureAwait(false);
        string scanGroupKey = AgentStatusCatalog.CreateScanGroupKey();
        IAgentEventScope scanAgentScope = _agentEventBus.CreateScope(scanGroupKey, AgentStatusCatalog.CreateScanAgentKey());
        IAgentEventScope scanVerifierAgentScope = _agentEventBus.CreateScope(scanGroupKey, AgentStatusCatalog.CreateScanVerifierAgentKey());

        await _agentEventBus.PublishGroupCreatedAsync(
            scanGroupKey,
            AgentStatusCatalog.CreateScanGroupDisplayName(),
            cancellationToken).ConfigureAwait(false);

        AIAgent scanAgent = _scanAgentFactory(repositoryRootPath, scanAgentScope);
        await scanAgentScope.PublishCreatedAsync(
            AgentStatusCatalog.CreateScanAgentDisplayName(),
            AgentStatusCatalog.WaitingStatus,
            cancellationToken).ConfigureAwait(false);

        AIAgent scanVerifierAgent = _scanVerifierAgentFactory(repositoryRootPath, scanVerifierAgentScope);
        await scanVerifierAgentScope.PublishCreatedAsync(
            AgentStatusCatalog.CreateScanVerifierAgentDisplayName(),
            AgentStatusCatalog.WaitingStatus,
            cancellationToken).ConfigureAwait(false);

        List<ChatMessage> scanMessages = CreateScanMessages(repositoryRootPath);
        int scanPublishedMessageCount = 0;

        int scanAttempts = 0;
        int verifierAttempts = 0;
        int verifierRejectionAttempts = 0;
        int missingSubmissionAttempts = 0;
        int scanAgentResetCount = 0;

        while (true)
        {
            scanAttempts++;
            await scanAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.RunningStatus, cancellationToken).ConfigureAwait(false);

            (Result runScanResult, scanPublishedMessageCount) = await RunAgentAsync(
                scanAgent,
                scanMessages,
                scanAgentScope,
                scanPublishedMessageCount,
                cancellationToken).ConfigureAwait(false);

            if (runScanResult.IsFailed)
            {
                await scanAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                return runScanResult.ToResult<ScanWorkflowResult>();
            }

            await scanAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.CompletedStatus, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<StoredScanProject> projects = await _scanProjectStore.ListAsync(cancellationToken).ConfigureAwait(false);

            if (projects.Count == 0)
            {
                missingSubmissionAttempts++;

                if (missingSubmissionAttempts >= _options.MaxMissingSubmissionAttempts)
                {
                    scanAgentResetCount++;

                    if (scanAgentResetCount > _options.MaxScanAgentResets)
                    {
                        await scanAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                        return Result.Fail<ScanWorkflowResult>("Scan Agent did not submit any scan projects after the allowed reset limit.");
                    }

                    scanAgent = _scanAgentFactory(repositoryRootPath, scanAgentScope);
                    await scanAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.WaitingStatus, cancellationToken).ConfigureAwait(false);
                    scanMessages = CreateScanMessages(repositoryRootPath);
                    scanPublishedMessageCount = 0;
                    missingSubmissionAttempts = 0;
                    continue;
                }

                scanMessages.Add(new ChatMessage(ChatRole.User, _messageTemplates.MissingScanSubmissionMessage));
                continue;
            }

            missingSubmissionAttempts = 0;
            verifierAttempts++;
            _verdictBuffer.Reset();
            await scanVerifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.RunningStatus, cancellationToken).ConfigureAwait(false);

            List<ChatMessage> verifierMessages =
            [
                new(ChatRole.User, BuildVerifierInput(projects)),
            ];
            int verifierPublishedMessageCount = 0;

            (Result runVerifierResult, verifierPublishedMessageCount) = await RunAgentAsync(
                scanVerifierAgent,
                verifierMessages,
                scanVerifierAgentScope,
                verifierPublishedMessageCount,
                cancellationToken).ConfigureAwait(false);

            if (runVerifierResult.IsFailed)
            {
                await scanVerifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                return runVerifierResult.ToResult<ScanWorkflowResult>();
            }

            await scanVerifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.CompletedStatus, cancellationToken).ConfigureAwait(false);

            if (_verdictBuffer.Latest is not ReviewVerdict verdict)
            {
                await scanVerifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                return Result.Fail<ScanWorkflowResult>("Scan Verifier Agent finished without submitting a verdict.");
            }

            if (verdict.Approved)
                return Result.Ok(CreateResult(
                    projects,
                    verdict,
                    scanAttempts,
                    verifierAttempts,
                    scanAgentResetCount));

            verifierRejectionAttempts++;

            if (verifierRejectionAttempts >= _options.MaxVerifierRejectionAttempts)
            {
                await scanVerifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                return Result.Ok(CreateResult(
                    projects,
                    verdict,
                    scanAttempts,
                    verifierAttempts,
                    scanAgentResetCount));
            }

            scanMessages.Add(new ChatMessage(ChatRole.User, verdict.Message));
        }
    }

    private static ScanWorkflowResult CreateResult(
        IReadOnlyList<StoredScanProject> projects,
        ReviewVerdict verdict,
        int scanAttempts,
        int verifierAttempts,
        int scanAgentResetCount) => new()
        {
            Projects = projects,
            Verdict = verdict,
            ScanAttempts = scanAttempts,
            VerifierAttempts = verifierAttempts,
            ScanAgentResetCount = scanAgentResetCount,
        };

    private static async Task<(Result Result, int PublishedMessageCount)> RunAgentAsync(
        AIAgent agent,
        List<ChatMessage> messages,
        IAgentEventScope eventScope,
        int publishedMessageCount,
        CancellationToken cancellationToken)
    {
        try
        {
            await PublishPendingUserMessagesAsync(messages, eventScope, publishedMessageCount, cancellationToken).ConfigureAwait(false);
            AgentResponse response = await agent.RunAsync(messages, session: null, options: null, cancellationToken).ConfigureAwait(false);

            foreach (ChatMessage message in response.Messages)
            {
                messages.Add(message);
                await AgentToolEventPublisher.PublishAsync(message, eventScope, cancellationToken).ConfigureAwait(false);
                if (message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text))
                    await eventScope.PublishAssistantMessageAsync(message.Text, cancellationToken).ConfigureAwait(false);
            }

            publishedMessageCount = messages.Count;

            return (Result.Ok(), publishedMessageCount);
        }
        catch (Exception ex)
        {
            return (Result.Fail(new ExceptionalError($"Agent run failed: {ex}", ex)), publishedMessageCount);
        }
    }

    private static async ValueTask PublishPendingUserMessagesAsync(
        List<ChatMessage> messages,
        IAgentEventScope eventScope,
        int publishedMessageCount,
        CancellationToken cancellationToken)
    {
        for (int index = publishedMessageCount; index < messages.Count; index++)
        {
            ChatMessage message = messages[index];
            if (message.Role == ChatRole.User && !string.IsNullOrWhiteSpace(message.Text))
                await eventScope.PublishUserMessageAsync(message.Text, cancellationToken).ConfigureAwait(false);
        }
    }

    private string BuildScanInput(string repositoryRootPath)
        =>
        $"{_messageTemplates.ScanInputPrefix}{Environment.NewLine}{Environment.NewLine}{repositoryRootPath}";

    private List<ChatMessage> CreateScanMessages(string repositoryRootPath)
        =>
    [
        new(ChatRole.User, BuildScanInput(repositoryRootPath)),
    ];

    private string BuildVerifierInput(IReadOnlyList<StoredScanProject> projects)
        =>
        $"{_messageTemplates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(projects)}";
}
