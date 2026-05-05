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
using System.Text.Json;

namespace CodeSnifferDog.Workflows.Scan;

public sealed class ScanWorkflow(
    Func<string, AIAgent> scanAgentFactory,
    Func<string, AIAgent> scanVerifierAgentFactory,
    IScanProjectStore scanProjectStore,
    ReviewVerdictBuffer verdictBuffer,
    PromptAssetReader? promptAssetReader = null,
    ScanWorkflowOptions? options = null,
    IAgentStatusEventPublisher? agentStatusEventPublisher = null)
{
    private readonly Func<string, AIAgent> _scanAgentFactory = scanAgentFactory;
    private readonly Func<string, AIAgent> _scanVerifierAgentFactory = scanVerifierAgentFactory;
    private readonly IScanProjectStore _scanProjectStore = scanProjectStore;
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly ScanWorkflowMessageTemplates _messageTemplates =
        new(promptAssetReader ?? new PromptAssetReader());
    private readonly ScanWorkflowOptions _options = options ?? new ScanWorkflowOptions();
    private readonly IAgentStatusEventPublisher _agentStatusEventPublisher = agentStatusEventPublisher ?? NoOpAgentStatusEventPublisher.Instance;

    public async Task<Result<ScanWorkflowResult>> RunAsync(
        string repositoryRootPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail("Repository root path is required.");

        repositoryRootPath = repositoryRootPath.Trim();
        await _scanProjectStore.ClearAsync(cancellationToken).ConfigureAwait(false);
        string scanGroupKey = AgentStatusCatalog.CreateScanGroupKey();
        string scanAgentKey = AgentStatusCatalog.CreateScanAgentKey();
        string scanVerifierAgentKey = AgentStatusCatalog.CreateScanVerifierAgentKey();

        await _agentStatusEventPublisher.PublishAsync(new AgentGroupCreatedEvent
        {
            GroupKey = scanGroupKey,
            DisplayName = AgentStatusCatalog.CreateScanGroupDisplayName(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken).ConfigureAwait(false);

        AIAgent scanAgent = _scanAgentFactory(repositoryRootPath);
        await _agentStatusEventPublisher.PublishAsync(new AgentCreatedEvent
        {
            AgentKey = scanAgentKey,
            DisplayName = AgentStatusCatalog.CreateScanAgentDisplayName(),
            InitialStatus = AgentStatusCatalog.WaitingStatus,
            GroupKey = scanGroupKey,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken).ConfigureAwait(false);

        AIAgent scanVerifierAgent = _scanVerifierAgentFactory(repositoryRootPath);
        await _agentStatusEventPublisher.PublishAsync(new AgentCreatedEvent
        {
            AgentKey = scanVerifierAgentKey,
            DisplayName = AgentStatusCatalog.CreateScanVerifierAgentDisplayName(),
            InitialStatus = AgentStatusCatalog.WaitingStatus,
            GroupKey = scanGroupKey,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken).ConfigureAwait(false);

        List<ChatMessage> scanMessages = CreateScanMessages(repositoryRootPath);

        int scanAttempts = 0;
        int verifierAttempts = 0;
        int verifierRejectionAttempts = 0;
        int missingSubmissionAttempts = 0;
        int scanAgentResetCount = 0;

        while (true)
        {
            scanAttempts++;
            await PublishStatusAsync(scanGroupKey, scanAgentKey, AgentStatusCatalog.RunningStatus, cancellationToken).ConfigureAwait(false);

            Result runScanResult = await RunAgentAsync(scanAgent, scanMessages, cancellationToken).ConfigureAwait(false);

            if (runScanResult.IsFailed)
            {
                await PublishStatusAsync(scanGroupKey, scanAgentKey, AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                return runScanResult.ToResult<ScanWorkflowResult>();
            }

            await PublishStatusAsync(scanGroupKey, scanAgentKey, AgentStatusCatalog.CompletedStatus, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<StoredScanProject> projects = await _scanProjectStore.ListAsync(cancellationToken).ConfigureAwait(false);

            if (projects.Count == 0)
            {
                missingSubmissionAttempts++;

                if (missingSubmissionAttempts >= _options.MaxMissingSubmissionAttempts)
                {
                    scanAgentResetCount++;

                    if (scanAgentResetCount > _options.MaxScanAgentResets)
                    {
                        await PublishStatusAsync(scanGroupKey, scanAgentKey, AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                        return Result.Fail<ScanWorkflowResult>("Scan Agent did not submit any scan projects after the allowed reset limit.");
                    }

                    scanAgent = _scanAgentFactory(repositoryRootPath);
                    await PublishStatusAsync(scanGroupKey, scanAgentKey, AgentStatusCatalog.WaitingStatus, cancellationToken).ConfigureAwait(false);
                    scanMessages = CreateScanMessages(repositoryRootPath);
                    missingSubmissionAttempts = 0;
                    continue;
                }

                scanMessages.Add(new ChatMessage(ChatRole.User, _messageTemplates.MissingScanSubmissionMessage));
                continue;
            }

            missingSubmissionAttempts = 0;
            verifierAttempts++;
            _verdictBuffer.Reset();
            await PublishStatusAsync(scanGroupKey, scanVerifierAgentKey, AgentStatusCatalog.RunningStatus, cancellationToken).ConfigureAwait(false);

            List<ChatMessage> verifierMessages =
            [
                new(ChatRole.User, BuildVerifierInput(projects)),
            ];

            Result runVerifierResult = await RunAgentAsync(scanVerifierAgent, verifierMessages, cancellationToken).ConfigureAwait(false);

            if (runVerifierResult.IsFailed)
            {
                await PublishStatusAsync(scanGroupKey, scanVerifierAgentKey, AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                return runVerifierResult.ToResult<ScanWorkflowResult>();
            }

            await PublishStatusAsync(scanGroupKey, scanVerifierAgentKey, AgentStatusCatalog.CompletedStatus, cancellationToken).ConfigureAwait(false);

            if (_verdictBuffer.Latest is not ReviewVerdict verdict)
            {
                await PublishStatusAsync(scanGroupKey, scanVerifierAgentKey, AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
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
                await PublishStatusAsync(scanGroupKey, scanVerifierAgentKey, AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
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

    private ValueTask PublishStatusAsync(
        string groupKey,
        string agentKey,
        string status,
        CancellationToken cancellationToken)
        => _agentStatusEventPublisher.PublishAsync(new AgentStatusChangedEvent
        {
            GroupKey = groupKey,
            AgentKey = agentKey,
            Status = status,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken);

    private static async Task<Result> RunAgentAsync(
        AIAgent agent,
        List<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        try
        {
            AgentResponse response = await agent.RunAsync(messages, session: null, options: null, cancellationToken).ConfigureAwait(false);

            foreach (ChatMessage message in response.Messages)
                messages.Add(message);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(new ExceptionalError($"Agent run failed: {ex}", ex));
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
        $"{_messageTemplates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{JsonSerializer.Serialize(projects)}";
}
