using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Tables;

// The deal is choreography over a result the server already decided: cards leave the shoe one every
// ninety milliseconds, take a third of a second to reach their seat, and turn over at sixty percent
// of that travel so the flip lands mid flight rather than on arrival. Nothing here decides what a
// card is, only when it appears to arrive.
//
// A player who comes back to the phone after the hand moved on gets the board, not the show: the
// playback snaps to the end whenever it joins a round already past its deal, which is the same
// promise the rest of the table makes about refocusing on truth instead of replaying theatre.
internal static class BlackjackDealChoreography
{
    public const float StaggerSeconds = 0.09f;

    public const float TravelSeconds = 0.30f;

    public const float FlipFraction = 0.60f;

    public const float ArcHeight = 26f;

    public static float Travel(float elapsedSeconds, int cardIndex)
    {
        if (cardIndex < 0)
        {
            return 1f;
        }

        var started = elapsedSeconds - cardIndex * StaggerSeconds;
        if (started <= 0f)
        {
            return 0f;
        }

        var travel = started / TravelSeconds;
        return travel >= 1f ? 1f : travel;
    }

    public static bool FaceUp(float travel)
    {
        return travel >= FlipFraction;
    }

    // The card squashes to nothing at the flip point and opens again on the far side, so the swap
    // from back to face happens at zero width and is never seen as a pop.
    public static float FlipScaleX(float travel)
    {
        if (travel <= 0f)
        {
            return 1f;
        }

        if (travel < FlipFraction)
        {
            return 1f - travel / FlipFraction;
        }

        var opening = (travel - FlipFraction) / (1f - FlipFraction);
        return opening >= 1f ? 1f : opening;
    }

    public static float Duration(int cardCount)
    {
        if (cardCount <= 0)
        {
            return 0f;
        }

        return (cardCount - 1) * StaggerSeconds + TravelSeconds;
    }

    public static Vector2 Position(Vector2 from, Vector2 to, float travel, float scale)
    {
        var eased = 1f - (1f - travel) * (1f - travel);
        var point = Vector2.Lerp(from, to, eased);
        point.Y -= MathF.Sin(eased * MathF.PI) * ArcHeight * scale;
        return point;
    }

    public static Rect CardRect(Vector2 center, float width, float travel)
    {
        var height = Windows.Components.PlayingCards.HeightFor(width);
        var halfWidth = width * 0.5f * FlipScaleX(travel);
        var halfHeight = height * 0.5f;
        return new Rect(new Vector2(center.X - halfWidth, center.Y - halfHeight),
            new Vector2(center.X + halfWidth, center.Y + halfHeight));
    }
}
