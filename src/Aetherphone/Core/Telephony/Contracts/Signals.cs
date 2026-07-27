namespace Aetherphone.Core.Telephony.Contracts;

internal static class SignalType
{
    public const string Hello = "hello";
    public const string Start = "call.start";
    public const string Invite = "call.invite";
    public const string Accept = "call.accept";
    public const string Decline = "call.decline";
    public const string Cancel = "call.cancel";
    public const string Leave = "call.leave";
    public const string Rejoin = "call.rejoin";
    public const string Mute = "call.mute";
    public const string Incoming = "call.incoming";
    public const string Ringing = "call.ringing";
    public const string Roster = "call.roster";
    public const string Accepted = "call.accepted";
    public const string Declined = "call.declined";
    public const string Left = "call.left";
    public const string Ended = "call.ended";
    public const string Handled = "call.handled";
    public const string Unavailable = "call.unavailable";
    public const string ChatPing = "chat.ping";
    public const string VelvetPing = "velvet.ping";
    public const string SocialPing = "social.ping";
    public const string Error = "error";

    // AetherStream watch-along - rides the same wss /rt socket and CallControl envelope as the
    // call.* signals above (see StreamSignalRouter), not a second connection.
    public const string StreamState = "stream.state";
    public const string StreamJoin = "stream.join";
    public const string StreamLeave = "stream.leave";
    public const string StreamJoined = "stream.joined";
    public const string StreamDeclined = "stream.declined";
    public const string StreamRoster = "stream.roster";
    public const string StreamLeft = "stream.left";
    public const string StreamEnded = "stream.ended";

    // Zone-scoped discovery, deliberately separate from StreamJoin's mutual-contact/block-gated
    // path - a client asks "what's live in my current territory" and the server answers with
    // whoever is currently hosting in that same TerritoryId, no contact relationship required.
    public const string StreamNearby = "stream.nearby";
    public const string StreamNearbyRoster = "stream.nearby.roster";
}

internal static class ParticipantState
{
    public const string Ringing = "ringing";
    public const string Active = "active";
    public const string Left = "left";
}

internal sealed record ParticipantInfo(
    string UserId,
    string Name,
    string World,
    string DisplayName,
    int Slot,
    string State,
    bool Muted);

// One entry in a stream.nearby.roster response - a currently-hosting user in the same territory
// as the requester. Deliberately just enough to show and join with, not a full ParticipantInfo
// (there's no call slot/mute state for someone you haven't joined yet).
internal sealed record NearbyStreamInfo(string HostId, string Name, string World, string DisplayName);

internal sealed record CallControl
{
    public string Type { get; init; } = string.Empty;
    public string? CallId { get; init; }
    public string[]? InviteeIds { get; init; }
    public ParticipantInfo? From { get; init; }
    public ParticipantInfo[]? Participants { get; init; }
    public string? UserId { get; init; }
    public bool? Muted { get; init; }
    public string? Reason { get; init; }

    // stream.* fields - nulls are omitted on the wire and unknown fields are ignored server-side,
    // so adding these here is backward compatible with the existing call.* parsing above.
    public string? HostId { get; init; }
    public string? Url { get; init; }
    public double? PositionSeconds { get; init; }
    public bool? Paused { get; init; }

    // The host's AetherStream world-anchored screen transform (VideoEngine.ScreenPosition/
    // ScreenYaw/ScreenScale) - optional, omitted entirely if the host's screen isn't active. Each
    // viewer's own client applies this to their own local ScreenPainter (see
    // VideoEngine.ApplyRemoteScreenTransform) - there is no shared/networked 3D object, every
    // Aetherphone client just draws its own copy at the same coordinates.
    public float? ScreenX { get; init; }
    public float? ScreenY { get; init; }
    public float? ScreenZ { get; init; }
    public float? ScreenYaw { get; init; }
    public float? ScreenScale { get; init; }

    // Zone-scoped discovery (stream.nearby / stream.nearby.roster). TerritoryId does double duty:
    // sent by a host alongside stream.state so the server knows which zone their stream is "in",
    // and sent by a would-be viewer on stream.nearby to ask "what's live in this zone". The server
    // is the only thing that ever compares the two - the client just reports its own current one.
    public uint? TerritoryId { get; init; }
    public NearbyStreamInfo[]? NearbyStreams { get; init; }
}
