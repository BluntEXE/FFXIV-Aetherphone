using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Online;

internal static class GamesOnlineText
{
    public static LocString ReasonMessage(string reason)
    {
        return reason switch
        {
            "full" => L.Games.OnlineRoomFull,
            "unavailable" => L.Games.OnlineWrongCode,
            "banned_from_room" => L.Games.OnlineBanned,
            "blocked" => L.Games.OnlineBlocked,
            "already_hosting" => L.Games.OnlineAlreadyHosting,
            "cooldown" => L.Games.OnlineCooldown,
            "not_your_turn" => L.Games.OnlineNotYourTurn,
            "stale_action" => L.Games.OnlineStale,
            "ended" => L.Games.OnlineRoomEnded,
            "kicked" => L.Games.OnlineKicked,
            "restarting" => L.Games.OnlineRestarting,
            _ => L.Games.OnlineUnavailable,
        };
    }
}
