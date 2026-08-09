using System.Text.Json.Serialization;
using Aetherphone.Core.Aethernet.Contracts;

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
    public const string ContentRemoved = "content.removed";
    public const string ChatPing = "chat.ping";
    public const string VelvetPing = "velvet.ping";
    public const string GramPing = "gram.ping";
    public const string SocialPing = "social.ping";
    public const string MusterPing = "muster.ping";
    public const string AnnouncePing = "announce.ping";
    public const string CasinoPrefix = "casino.";
    public const string CasinoAttach = "casino.attach";
    public const string CasinoDetach = "casino.detach";
    public const string CasinoResync = "casino.resync";
    public const string CasinoAttached = "casino.attached";
    public const string CasinoDeclined = "casino.declined";
    public const string CasinoSnapshot = "casino.snapshot";
    public const string CasinoEvent = "casino.event";
    public const string CasinoPrivate = "casino.private";
    public const string CasinoEnded = "casino.ended";
    public const string CasinoPing = "casino.ping";
    public const string Error = "error";
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
    public string? App { get; init; }
    public string? ContentKind { get; init; }
    public string? ContentId { get; init; }
    public string? ParentId { get; init; }
    public ChatMessageDto? Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CasinoPayload? Casino { get; init; }
}

// The casino gets exactly one envelope field and nests everything else, so a room event never
// widens the schema every call, chat, and stream message pays for. The field drops out of the
// JSON when it is null, which keeps a call control byte identical to the one shipping today.
internal sealed record CasinoPayload
{
    public string RoomId { get; init; } = string.Empty;
    public int Epoch { get; init; }
    public long Seq { get; init; }
    public long ServerNowUnixMs { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EventKind { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CasinoRoomSnapshotDto? Snapshot { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CasinoRoomEventDto? Event { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CasinoPrivateDto? Private { get; init; }
}
