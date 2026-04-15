using System.Text.Json;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.Scan;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Workflows.Scan;

public sealed class ScanWorkflow(
    AIAgent scanAgent,
    AIAgent scanVerifierAgent,
    IScanProjectStore scanProjectStore,
    ReviewVerdictBuffer verdictBuffer,
    ScanWorkflowOptions? options = null)
{
    private readonly AIAgent _scanAgent = scanAgent;
    private readonly AIAgent _scanVerifierAgent = scanVerifierAgent;
    private readonly IScanProjectStore _scanProjectStore = scanProjectStore;
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly ScanWorkflowOptions _options = options ?? new ScanWorkflowOptions();

    public async Task<Result<ScanWorkflowResult>> RunAsync(
        string repositoryRootPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail("Repository root path is required.");

        repositoryRootPath = repositoryRootPath.Trim();
        await _scanProjectStore.ClearAsync(cancellationToken).ConfigureAwait(false);

        List<ChatMessage> scanMessages = CreateScanMessages(repositoryRootPath);

        int scanAttempts = 0;
        int verifierAttempts = 0;
        int verifierRejectionAttempts = 0;
        int missingSubmissionAttempts = 0;
        int scanAgentResetCount = 0;

        while (true)
        {
            scanAttempts++;

            Result runScanResult = await RunAgentAsync(_scanAgent, scanMessages, cancellationToken).ConfigureAwait(false);

            if (runScanResult.IsFailed)
                return runScanResult.ToResult<ScanWorkflowResult>();

            IReadOnlyList<StoredScanProject> projects = await _scanProjectStore.ListAsync(cancellationToken).ConfigureAwait(false);

            if (projects.Count == 0)
            {
                missingSubmissionAttempts++;

                if (missingSubmissionAttempts >= _options.MaxMissingSubmissionAttempts)
                {
                    scanAgentResetCount++;

                    if (scanAgentResetCount > _options.MaxScanAgentResets)
                        return Result.Fail<ScanWorkflowResult>("Scan Agent did not submit any scan projects after the allowed reset limit.");

                    scanMessages = CreateScanMessages(repositoryRootPath);
                    missingSubmissionAttempts = 0;
                    continue;
                }

                scanMessages.Add(new ChatMessage(ChatRole.User, ScanToolSet.MissingScanSubmissionMessage));
                continue;
            }

            missingSubmissionAttempts = 0;
            verifierAttempts++;
            _verdictBuffer.Reset();

            List<ChatMessage> verifierMessages =
            [
                new(ChatRole.User, BuildVerifierInput(projects)),
            ];

            Result runVerifierResult = await RunAgentAsync(_scanVerifierAgent, verifierMessages, cancellationToken).ConfigureAwait(false);

            if (runVerifierResult.IsFailed)
                return runVerifierResult.ToResult<ScanWorkflowResult>();

            if (_verdictBuffer.Latest is not ReviewVerdict verdict)
                return Result.Fail<ScanWorkflowResult>("Scan Verifier Agent finished without submitting a verdict.");

            if (verdict.Approved)
                return Result.Ok(CreateResult(
                    projects,
                    verdict,
                    scanAttempts,
                    verifierAttempts,
                    scanAgentResetCount,
                    scanVerifierApproved: true,
                    continuedAfterVerifierRejectionLimit: false));

            verifierRejectionAttempts++;

            if (verifierRejectionAttempts >= _options.MaxVerifierRejectionAttempts)
                return Result.Ok(CreateResult(
                    projects,
                    verdict,
                    scanAttempts,
                    verifierAttempts,
                    scanAgentResetCount,
                    scanVerifierApproved: false,
                    continuedAfterVerifierRejectionLimit: true));

            scanMessages.Add(new ChatMessage(ChatRole.User, verdict.Message));
        }
    }

    private static ScanWorkflowResult CreateResult(
        IReadOnlyList<StoredScanProject> projects,
        ReviewVerdict verdict,
        int scanAttempts,
        int verifierAttempts,
        int scanAgentResetCount,
        bool scanVerifierApproved,
        bool continuedAfterVerifierRejectionLimit) => new()
    {
        Projects = projects,
        Verdict = verdict,
        ScanVerifierApproved = scanVerifierApproved,
        ContinuedAfterVerifierRejectionLimit = continuedAfterVerifierRejectionLimit,
        ShouldEnterProjectPlanning = true,
        ScanAttempts = scanAttempts,
        VerifierAttempts = verifierAttempts,
        ScanAgentResetCount = scanAgentResetCount,
    };

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

    private static string BuildScanInput(string repositoryRootPath) =>
        $"{ScanToolSet.ScanInputPrefix}{Environment.NewLine}{Environment.NewLine}{repositoryRootPath}";

    private static List<ChatMessage> CreateScanMessages(string repositoryRootPath) =>
    [
        new(ChatRole.User, BuildScanInput(repositoryRootPath)),
    ];

    private static string BuildVerifierInput(IReadOnlyList<StoredScanProject> projects) =>
        $"{ScanToolSet.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{JsonSerializer.Serialize(projects)}";
}
