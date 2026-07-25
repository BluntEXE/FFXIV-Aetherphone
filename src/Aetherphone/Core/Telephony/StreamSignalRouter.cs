using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Telephony;

// AetherStream's watch-along signalling - rides CallSignalRouter's existing RealtimeConnection
// (same wss /rt socket, same CallControl JSON envelope as call.*) rather than opening a second
// WebSocket per user. Mirrors CallSignalRouter's own OnControl dispatch shape, just filtered to
// the stream.* family. Server-side policy (mutual-contact + block checks, room capacity, auto-leave
// on switching rooms) is enforced entirely server-side - this class only reflects what the server
// already decided, it doesn't re-implement any of that.
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

    public bool Connected => calls.Connected;

    // Host only - the server treats a viewer's stream.state as a no-op, and the first one from an
    // actual host creates the room ("go live"); later ones update it. Do not treat yourself as
    // hosting until the echoed stream.state comes back through StateReceived - see spec note on
    // the live-ack.
    public void PublishState(string url, double positionSeconds, bool paused)
    {
        calls.Send(new CallControl
        {
            Type = SignalType.StreamState, Url = url, PositionSeconds = positionSeconds, Paused = paused,
        });
    }

    // 2s per-connection cooldown is enforced server-side - callers must not auto-retry declines in
    // a tight loop.
    public void Join(string hostId)
    {
        calls.Send(new CallControl { Type = SignalType.StreamJoin, HostId = hostId });
    }

    // No hostId needed - the server already knows which room this connection is in. From the host
    // this ends the stream for everyone.
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
        }
    }

    public void Dispose()
    {
        calls.Connection.ControlReceived -= OnControl;
    }
}
