using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Casino;

// A turn alert is for the player who is not looking at the table. Firing one at somebody already
// watching their own cards land is the notification equivalent of tapping them on the shoulder to
// tell them where they are, so the table stamps its own attention every frame it draws and this
// only speaks once that stamp has gone stale.
//
// One alert per hand per seat: the key is the hand id and the split being asked for, which is also
// what makes a resync harmless. A snapshot re-delivered after a reconnect names the same hand, so
// it cannot ring twice for one decision.
internal sealed class CasinoTurnNotifier : IDisposable
{
    public const string AppId = "casino";

    // The group prefix is the whole channel: it stacks every alert from one table together in the
    // shell, dedupes the sound, and is what the router reads back to know which felt to open. A
    // separate settings key would be a switch nobody could find, because the settings list is built
    // from installed app ids and a channel id is not one.
    public const string GroupPrefix = "casino:";

    private const long AttentionWindowMilliseconds = 1_200;

    private readonly AethernetSession session;
    private readonly CasinoRoomsStore rooms;
    private readonly NotificationService notifications;
    private readonly Vector4 accent;

    private long attentionStampedAtTick;
    private string spokenTurnKey = string.Empty;

    public CasinoTurnNotifier(AethernetSession session, CasinoRoomsStore rooms, NotificationService notifications,
        Vector4 accent)
    {
        this.session = session;
        this.rooms = rooms;
        this.notifications = notifications;
        this.accent = accent;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    // Called from the table's own draw path, which only runs while the table is the thing on screen.
    public void StampAttention()
    {
        Interlocked.Exchange(ref attentionStampedAtTick, Environment.TickCount64);
    }

    public void Forget()
    {
        spokenTurnKey = string.Empty;
    }

    internal static string TurnKeyFor(string handId, int seatIndex, int splitIndex)
    {
        return string.Concat(handId, ":", seatIndex.ToString(Loc.Culture), ":",
            splitIndex.ToString(Loc.Culture));
    }

    internal static bool Watching(long stampedAtTick, long nowTick)
    {
        return stampedAtTick != 0 && nowTick - stampedAtTick <= AttentionWindowMilliseconds;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var board = rooms.Room.State?.Blackjack;
        if (board is null || board.HandId.Length == 0 || !BlackjackRules.IsSeat(board.MySeat)
            || board.ActiveSeat != board.MySeat)
        {
            return;
        }

        var key = TurnKeyFor(board.HandId, board.MySeat, board.ActiveSplit);
        if (string.Equals(spokenTurnKey, key, StringComparison.Ordinal))
        {
            return;
        }

        spokenTurnKey = key;
        if (Watching(Interlocked.Read(ref attentionStampedAtTick), Environment.TickCount64))
        {
            return;
        }

        var roomId = rooms.Room.RoomId;
        notifications.Notify(new PhoneNotification(AppId, Loc.T(L.Casino.NotifyTurnTitle),
            Loc.T(L.Casino.NotifyTurnBody), DateTime.Now, accent, string.Concat(GroupPrefix, roomId))
        {
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
    }
}
