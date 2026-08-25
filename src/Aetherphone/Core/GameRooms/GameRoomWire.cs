namespace Aetherphone.Core.GameRooms;

internal static class GameRoomWire
{
    public const string UnoKind = "games.uno";

    public const string ChessKind = "games.chess";

    public const string UnoHandEvent = "uno.hand";

    public const string ActionStart = "start";

    public const string ActionPlay = "play";

    public const string ActionDraw = "draw";

    public const string ActionPass = "pass";

    public const string ActionMove = "move";

    public const string ActionResign = "resign";

    public const string ChessEndCheckmate = "checkmate";

    public const string ChessEndStalemate = "stalemate";

    public const string ChessEndFiftyMove = "fifty";

    public const string ChessEndMaterial = "material";

    public const string ChessEndTimeout = "timeout";

    public const string ChessEndResign = "resign";

    public const string ChessEndDesertion = "desertion";

    public const int PhaseLobby = 0;

    public const int PhasePlaying = 1;

    public const int PhaseFinished = 2;

    public const string ReasonEnded = "ended";

    public const string ReasonKicked = "kicked";

    public const string ReasonRestarting = "restarting";

    public const string ReasonStaleAction = "stale_action";

    public const int WildCard = 52;

    public const int WildDrawFourCard = 53;

    public const int RankSkip = 10;

    public const int RankReverse = 11;

    public const int RankDrawTwo = 12;

    public static int ColorOf(int card)
    {
        return card is >= 0 and < WildCard ? card / 13 : -1;
    }

    public static int RankOf(int card)
    {
        return card is >= 0 and < WildCard ? card % 13 : -1;
    }

    public static bool IsWild(int card)
    {
        return card is WildCard or WildDrawFourCard;
    }

    public static bool IsPlayable(int card, int activeColor, int topCard)
    {
        if (IsWild(card))
        {
            return true;
        }

        if (card is < 0 or >= WildCard)
        {
            return false;
        }

        if (ColorOf(card) == activeColor)
        {
            return true;
        }

        return topCard < WildCard && RankOf(card) == RankOf(topCard);
    }
}
