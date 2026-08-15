using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Telephony;

internal sealed class StreamSignalRouter : IDisposable
{
    private readonly CallSignalRouter calls;

    public StreamSignalRouter(CallSignalRouter calls)
    {
        this.calls = calls;
        calls.Connection.ControlReceived += OnControl;
    }

    public event Action<CallControl>? Joined;
    public event Action<CallControl>? Declined;
    public event Action<CallControl>? RosterReceived;

    public event Action<CallControl>? LeftReceived;
    public event Action<CallControl>? Ended;
    public event Action<CallControl>? StateReceived;
    public event Action<CallControl>? NearbyReceived;

    public event Action<CallControl>? JoinRequested;
    public event Action<CallControl>? JoinPending;

    public event Action<CallControl>? QueueSuggested;
    public event Action<CallControl>? QueueSuggestionResult;

    public event Action<CallControl>? Kicked;

    public bool Connected => calls.Connected;

    public void PublishState(string url, double positionSeconds, bool paused, uint territoryId, uint worldId,
        bool approvalRequired, bool discoverable, StreamQueueEntry[]? upcomingQueue = null,
        Vector3? screenPosition = null, float? screenYaw = null, float? screenScale = null)
    {
        calls.Send(new CallControl
        {
            Type = SignalType.StreamState, Url = url, PositionSeconds = positionSeconds, Paused = paused,
            TerritoryId = territoryId, WorldId = worldId, ApprovalRequired = approvalRequired,
            Discoverable = discoverable,
            UpcomingQueue = upcomingQueue,
            ScreenX = screenPosition?.X, ScreenY = screenPosition?.Y, ScreenZ = screenPosition?.Z,
            ScreenYaw = screenYaw, ScreenScale = screenScale,
        });
    }

    public void Approve(string userId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamApprove, UserId = userId });
    }

    public void Deny(string userId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamDeny, UserId = userId });
    }

    public void SuggestQueueItem(string url, string suggestionId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamQueueSuggest, Url = url, SuggestionId = suggestionId });
    }

    public void ApproveQueueSuggestion(string suggestionId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamQueueApprove, SuggestionId = suggestionId });
    }

    public void DenyQueueSuggestion(string suggestionId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamQueueDeny, SuggestionId = suggestionId });
    }

    public void Kick(string userId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamKick, UserId = userId });
    }

    public void Join(string hostId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamJoin, HostId = hostId });
    }

    public void RequestNearby(uint territoryId, uint worldId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamNearby, TerritoryId = territoryId, WorldId = worldId });
    }

    public void Leave()
    {
        calls.Send(new CallControl { Type = SignalType.StreamLeave });
    }

    private void OnControl(CallControl message)
    {
        switch (message.Type)
        {
            case SignalType.StreamJoined:
                Joined?.Invoke(message);
                return;
            case SignalType.StreamDeclined:
                Declined?.Invoke(message);
                return;
            case SignalType.StreamRoster:
                RosterReceived?.Invoke(message);
                return;
            case SignalType.StreamLeft:
                LeftReceived?.Invoke(message);
                return;
            case SignalType.StreamState:
                StateReceived?.Invoke(message);
                return;
            case SignalType.StreamEnded:
                Ended?.Invoke(message);
                return;
            case SignalType.StreamNearbyRoster:
                NearbyReceived?.Invoke(message);
                return;
            case SignalType.StreamJoinRequest:
                JoinRequested?.Invoke(message);
                return;
            case SignalType.StreamJoinPending:
                JoinPending?.Invoke(message);
                return;
            case SignalType.StreamQueueSuggestion:
                QueueSuggested?.Invoke(message);
                return;
            case SignalType.StreamQueueSuggestionResult:
                QueueSuggestionResult?.Invoke(message);
                return;
            case SignalType.StreamKicked:
                Kicked?.Invoke(message);
                return;
        }
    }

    public void Dispose()
    {
        calls.Connection.ControlReceived -= OnControl;
    }
}
