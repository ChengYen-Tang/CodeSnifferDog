using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.Scan;
using CodeSnifferDog.Workflows.Common;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Modules.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Workflows.Scan;

/// <summary>
/// Runs repository scanning with a scan agent, a verifier agent, retry limits, and submission-reset handling.
/// </summary>
/// <param name="scanAgentFactory">Creates the scan agent for one repository run.</param>
/// <param name="scanVerifierAgentFactory">Creates the verifier agent for scan submissions.</param>
/// <param name="scanProjectStore">Store that receives scan project submissions.</param>
/// <param name="verdictBuffer">Buffer that captures verifier verdict submissions.</param>
/// <param name="promptAssetReader">Optional prompt reader used to load workflow prompt assets.</param>
/// <param name="options">Optional workflow options that control retries and timeouts.</param>
/// <param name="agentEventBus">Optional event bus used to publish agent lifecycle and transcript events.</param>
public sealed class Workflow(
    Func<string, IAgentEventScope, AgentCreationResult> scanAgentFactory,
    Func<string, IAgentEventScope, AgentCreationResult> scanVerifierAgentFactory,
    IScanProjectStore scanProjectStore,
    ReviewVerdictBuffer verdictBuffer,
    PromptAssetReader? promptAssetReader = null,
    ScanWorkflowOptions? options = null,
    IAgentEventBus? agentEventBus = null)
{
    private readonly Func<string, IAgentEventScope, AgentCreationResult> _scanAgentFactory = scanAgentFactory;
    private readonly Func<string, IAgentEventScope, AgentCreationResult> _scanVerifierAgentFactory = scanVerifierAgentFactory;
    private readonly IScanProjectStore _scanProjectStore = scanProjectStore;
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly MessageBuilder _messageBuilder =
        new(new MessageTemplates(promptAssetReader ?? new PromptAssetReader()));
    private readonly ScanWorkflowOptions _options = options ?? new ScanWorkflowOptions();
    private readonly IAgentEventBus _agentEventBus = agentEventBus ?? NoOpAgentEventBus.Instance;

    /// <summary>
    /// Runs the scan workflow for one repository.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path that should be scanned.</param>
    /// <param name="cancellationToken">Cancels the workflow.</param>
    /// <returns>The scan workflow result.</returns>
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

        AgentCreationResult scanAgentCreation = _scanAgentFactory(repositoryRootPath, scanAgentScope);
        AIAgent scanAgent = scanAgentCreation.Agent;
        await scanAgentScope.PublishCreatedAsync(
            AgentStatusCatalog.CreateScanAgentDisplayName(),
            scanAgentCreation.SystemPrompt,
            AgentStatusCatalog.WaitingStatus,
            cancellationToken).ConfigureAwait(false);

        AgentCreationResult scanVerifierAgentCreation = _scanVerifierAgentFactory(repositoryRootPath, scanVerifierAgentScope);
        AIAgent scanVerifierAgent = scanVerifierAgentCreation.Agent;
        await scanVerifierAgentScope.PublishCreatedAsync(
            AgentStatusCatalog.CreateScanVerifierAgentDisplayName(),
            scanVerifierAgentCreation.SystemPrompt,
            AgentStatusCatalog.WaitingStatus,
            cancellationToken).ConfigureAwait(false);

        List<ChatMessage> scanMessages = _messageBuilder.CreateScanMessages(repositoryRootPath);
        int scanPublishedMessageCount = 0;

        int scanAttempts = 0;
        int verifierAttempts = 0;
        int verifierRejectionAttempts = 0;
        int missingSubmissionAttempts = 0;
        int scanAgentResetCount = 0;

        while (true)
        {
            scanAttempts++;

            (Result runScanResult, scanPublishedMessageCount, scanAgent) = await WorkflowAgentRunService.RunAsync(
                scanAgent,
                () => _scanAgentFactory(repositoryRootPath, scanAgentScope).Agent,
                PrepareAttempt,
                static state => state.Restore(),
                scanMessages,
                scanAgentScope,
                scanPublishedMessageCount,
                _options.AgentRunTimeout,
                _options.MaxConsecutiveRunFailures,
                cancellationToken).ConfigureAwait(false);

            if (runScanResult.IsFailed)
                return runScanResult.ToResult<ScanWorkflowResult>();

            IReadOnlyList<StoredScanProject> projects = await _scanProjectStore.ListAsync(cancellationToken).ConfigureAwait(false);

            if (projects.Count == 0)
            {
                missingSubmissionAttempts++;

                if (RetryLimit.IsReached(missingSubmissionAttempts, _options.MaxMissingSubmissionAttempts))
                {
                    scanAgentResetCount++;

                    if (RetryLimit.IsExceeded(scanAgentResetCount, _options.MaxScanAgentResets))
                    {
                        await scanAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                        return Result.Fail<ScanWorkflowResult>("Scan Agent did not submit any scan projects after the allowed reset limit.");
                    }

                    scanAgent = _scanAgentFactory(repositoryRootPath, scanAgentScope).Agent;
                    await scanAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.WaitingStatus, cancellationToken).ConfigureAwait(false);
                    scanMessages = _messageBuilder.CreateScanMessages(repositoryRootPath);
                    scanPublishedMessageCount = 0;
                    missingSubmissionAttempts = 0;
                    continue;
                }

                scanMessages.Add(_messageBuilder.CreateMissingSubmissionMessage());
                continue;
            }

            missingSubmissionAttempts = 0;
            List<ChatMessage> verifierMessages = _messageBuilder.CreateVerifierMessages(projects);
            int verifierPublishedMessageCount = 0;
            int verifierMissingVerdictAttempts = 0;

            while (true)
            {
                verifierAttempts++;
                _verdictBuffer.Reset();

                (Result runVerifierResult, verifierPublishedMessageCount, scanVerifierAgent) = await WorkflowAgentRunService.RunAsync(
                    scanVerifierAgent,
                    () => _scanVerifierAgentFactory(repositoryRootPath, scanVerifierAgentScope).Agent,
                    PrepareAttempt,
                    static state => state.Restore(),
                    verifierMessages,
                    scanVerifierAgentScope,
                    verifierPublishedMessageCount,
                    _options.AgentRunTimeout,
                    _options.MaxConsecutiveRunFailures,
                    cancellationToken).ConfigureAwait(false);

                if (runVerifierResult.IsFailed)
                    return runVerifierResult.ToResult<ScanWorkflowResult>();

                if (_verdictBuffer.Latest is not ReviewVerdict verdict)
                {
                    verifierMissingVerdictAttempts++;

                    if (RetryLimit.IsReached(verifierMissingVerdictAttempts, _options.MaxMissingSubmissionAttempts))
                    {
                        await scanVerifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                        return Result.Fail<ScanWorkflowResult>("Scan Verifier Agent finished without submitting a verdict.");
                    }

                    verifierMessages.Add(_messageBuilder.CreateMissingVerifierVerdictMessage());
                    continue;
                }

                if (verdict.Approved)
                    return Result.Ok(ResultFactory.Create(
                        projects,
                        verdict,
                        scanAttempts,
                        verifierAttempts,
                        scanAgentResetCount));

                verifierRejectionAttempts++;

                if (RetryLimit.IsReached(verifierRejectionAttempts, _options.MaxVerifierRejectionAttempts))
                {
                    await scanVerifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                    return Result.Ok(ResultFactory.Create(
                        projects,
                        verdict,
                        scanAttempts,
                        verifierAttempts,
                        scanAgentResetCount));
                }

                scanMessages.Add(new ChatMessage(ChatRole.User, verdict.Message));
                break;
            }
        }
    }

    /// <summary>
    /// Captures restorable state for one scan workflow attempt.
    /// </summary>
    /// <param name="attemptId">Attempt identifier associated with the snapshot.</param>
    /// <returns>A lease pair that can restore both the scan-project store and verdict buffer.</returns>
    private WorkflowAttemptLeasePair PrepareAttempt(Guid attemptId)
    {
        return new WorkflowAttemptLeasePair(
            _scanProjectStore.BeginAttempt(attemptId),
            _verdictBuffer.BeginAttempt(attemptId));
    }

}
