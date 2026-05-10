using CodeSnifferDog.Server.Shared.Projects;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.AspNetCore.SignalR.Client;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus;

public sealed class SignalRProjectAgentStatusLiveSubscriptionClient(HttpClient httpClient) : IProjectAgentStatusLiveSubscriptionClient
{
    private readonly HttpClient _httpClient = httpClient;
    private HubConnection? _connection;
    private Guid? _subscribedProjectId;
    private Func<ProjectAgentLiveUpdateDto, Task>? _onUpdate;

    public async Task SubscribeAsync(
        ProjectAgentLiveSubscriptionRequestDto request,
        Func<ProjectAgentLiveUpdateDto, Task> onUpdate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onUpdate);

        _onUpdate = onUpdate;
        await EnsureConnectionAsync(cancellationToken).ConfigureAwait(false);

        if (_subscribedProjectId is Guid subscribedProjectId)
        {
            if (subscribedProjectId == request.ProjectId)
            {
                await _connection!.InvokeAsync(ProjectUpdatesContract.SubscribeToProjectMethodName, request, cancellationToken).ConfigureAwait(false);
                return;
            }

            await _connection!.InvokeAsync(ProjectUpdatesContract.UnsubscribeFromProjectMethodName, subscribedProjectId, cancellationToken).ConfigureAwait(false);
        }

        await _connection!.InvokeAsync(ProjectUpdatesContract.SubscribeToProjectMethodName, request, cancellationToken).ConfigureAwait(false);
        _subscribedProjectId = request.ProjectId;
    }

    public async Task UnsubscribeAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null || _subscribedProjectId is null || _connection.State != HubConnectionState.Connected)
        {
            _subscribedProjectId = null;
            return;
        }

        await _connection.InvokeAsync(ProjectUpdatesContract.UnsubscribeFromProjectMethodName, _subscribedProjectId.Value, cancellationToken).ConfigureAwait(false);
        _subscribedProjectId = null;
    }

    private async Task EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(new Uri(_httpClient.BaseAddress!, ProjectUpdatesContract.HubPath))
                .Build();

            _connection.On<ProjectAgentLiveUpdateDto>(ProjectUpdatesContract.AgentStatusUpdatedMethodName, update =>
            {
                Func<ProjectAgentLiveUpdateDto, Task>? handler = _onUpdate;
                return handler is null ? Task.CompletedTask : handler(update);
            });
        }

        if (_connection.State == HubConnectionState.Connected)
            return;

        await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        _subscribedProjectId = null;
        _onUpdate = null;
    }
}
