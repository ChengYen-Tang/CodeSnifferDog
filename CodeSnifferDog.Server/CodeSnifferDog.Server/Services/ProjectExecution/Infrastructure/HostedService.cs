using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Readiness;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Recovery;
using Microsoft.Extensions.Options;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

/// <summary>
/// Background service that claims queued projects and executes their review workflows.
/// </summary>
internal sealed class HostedService : BackgroundService
{
    private readonly IGate _readinessGate;
    private readonly IClaimer _queueClaimer;
    private readonly IClaimExecutor _claimExecutor;
    private readonly IService _recoveryService;
    private readonly IQueueLock _queueLock;
    private readonly Settings _options;
    private readonly ILogger<HostedService> _logger;
    private bool _loggedAnalysisRunnerNotReady;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostedService"/> class.
    /// </summary>
    /// <param name="readinessGate">Gate that determines whether execution can start.</param>
    /// <param name="queueClaimer">Service that claims the next queued project.</param>
    /// <param name="claimExecutor">Service that executes a claimed project.</param>
    /// <param name="recoveryService">Service that restores interrupted execution state at startup.</param>
    /// <param name="queueLock">Lock that serializes queue claims.</param>
    /// <param name="options">Project execution settings.</param>
    /// <param name="logger">Logger for worker activity and failures.</param>
    public HostedService(
        IGate readinessGate,
        IClaimer queueClaimer,
        IClaimExecutor claimExecutor,
        IService recoveryService,
        IQueueLock queueLock,
        IOptions<Settings> options,
        ILogger<HostedService> logger)
    {
        _readinessGate = readinessGate;
        _queueClaimer = queueClaimer;
        _claimExecutor = claimExecutor;
        _recoveryService = recoveryService;
        _queueLock = queueLock;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.MaxConcurrentWorkers <= 0)
            throw new InvalidOperationException("ProjectExecution:MaxConcurrentWorkers must be greater than zero.");

        await _recoveryService.RecoverAsync(stoppingToken);

        Task[] workers = Enumerable
            .Range(0, _options.MaxConcurrentWorkers)
            .Select(workerIndex => RunWorkerLoopAsync(workerIndex + 1, stoppingToken))
            .ToArray();

        await Task.WhenAll(workers);
    }

    /// <summary>
    /// Runs a single worker loop that repeatedly claims and executes queued projects.
    /// </summary>
    /// <param name="workerNumber">One-based worker number used in logs.</param>
    /// <param name="stoppingToken">Token that stops the worker loop.</param>
    private async Task RunWorkerLoopAsync(int workerNumber, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Result readiness = _readinessGate.Check();
                if (!readiness.IsReady)
                {
                    if (!_loggedAnalysisRunnerNotReady)
                    {
                        _loggedAnalysisRunnerNotReady = true;
                        _logger.LogInformation(
                            "Project executor is waiting for a configured project analysis runner. Reason: {Reason}",
                            readiness.Reason ?? "Unknown reason.");
                    }

                    await Task.Delay(_options.QueuePollingInterval, stoppingToken);
                    continue;
                }

                _loggedAnalysisRunnerNotReady = false;

                Claim? claim;
                using (await _queueLock.AcquireAsync(stoppingToken))
                    claim = await _queueClaimer.TryClaimNextAsync(stoppingToken);

                if (claim is null)
                {
                    await Task.Delay(_options.QueuePollingInterval, stoppingToken);
                    continue;
                }

                await _claimExecutor.ExecuteAsync(workerNumber, claim, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Project executor worker {WorkerNumber} loop failed.", workerNumber);
                await Task.Delay(_options.QueuePollingInterval, stoppingToken);
            }
        }
    }
}
