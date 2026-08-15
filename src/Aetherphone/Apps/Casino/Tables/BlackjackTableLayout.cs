using Aetherphone.Core;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Tables;

internal static class BlackjackTableLayout
{
    public const float DealerFanFraction = 0.13f;

    public const float BubbleFraction = 0.27f;

    public const float RailFraction = 0.475f;

    public const float HeroFanFraction = 0.70f;

    public const float SideInset = 10f;

    public const float DealerCardWidth = 38f;

    public const float DealerTotalDrop = 12f;

    public const float RailCardWidth = 18f;

    public const float RailSplitCardWidth = 14f;

    public const float RailPuckRadius = 16f;

    public const float RailCardsLift = 58f;

    public const float RailTotalLift = 45f;

    public const float RailBadgeLift = 47f;

    public const float RailBetLift = 26f;

    public const float RailNameDrop = 24f;

    public const float RailStackDrop = 35f;

    public const float HeroTotalDrop = 12f;

    public const float HeroChipsDrop = 46f;

    public const float HeroSlotWidthCap = 150f;

    public const float HeroSlotPad = 8f;

    public const float CapsuleDrop = 26f;

    public const float CapsuleHeight = 44f;

    public const float CapsulePuckRadius = 17f;

    public const float ShoeInsetX = 30f;

    public const float ShoeInsetY = 26f;

    public static int RailSeatCount(int mySeat)
    {
        return BlackjackRules.IsSeat(mySeat) ? BlackjackRules.SeatCount - 1 : BlackjackRules.SeatCount;
    }

    public static int RailSlotOf(int seatIndex, int mySeat)
    {
        if (!BlackjackRules.IsSeat(mySeat))
        {
            return seatIndex;
        }

        var slot = (seatIndex - mySeat - 1) % BlackjackRules.SeatCount;
        return slot < 0 ? slot + BlackjackRules.SeatCount : slot;
    }

    public static float RailColumnWidth(in Rect felt, int railCount, float scale)
    {
        if (railCount <= 0)
        {
            return felt.Width;
        }

        return (felt.Width - SideInset * 2f * scale) / railCount;
    }

    public static Vector2 RailPuckCenter(in Rect felt, int slot, int railCount, float scale)
    {
        var columnWidth = RailColumnWidth(felt, railCount, scale);
        var x = felt.Min.X + SideInset * scale + columnWidth * (slot + 0.5f);
        return new Vector2(x, RailPuckY(felt));
    }

    public static float RailPuckY(in Rect felt)
    {
        return felt.Min.Y + felt.Height * RailFraction;
    }

    public static Vector2 DealerFanCenter(in Rect felt)
    {
        return new Vector2(felt.Center.X, felt.Min.Y + felt.Height * DealerFanFraction);
    }

    public static Vector2 BubbleAnchor(in Rect felt)
    {
        return new Vector2(felt.Center.X, felt.Min.Y + felt.Height * BubbleFraction);
    }

    public static Vector2 ShoeAnchor(in Rect felt, float scale)
    {
        return new Vector2(felt.Max.X - ShoeInsetX * scale, felt.Min.Y + ShoeInsetY * scale);
    }

    public static float HeroFanY(in Rect felt)
    {
        return felt.Min.Y + felt.Height * HeroFanFraction;
    }

    public static float HeroCardWidth(int handCount)
    {
        return handCount switch
        {
            <= 1 => 44f,
            2 => 36f,
            3 => 30f,
            _ => 24f,
        };
    }

    public static float HeroSlotWidth(in Rect felt, int handCount, float scale)
    {
        var count = handCount > 0 ? handCount : 1;
        var available = (felt.Width - SideInset * 2f * scale) / count;
        var cap = HeroSlotWidthCap * scale;
        return available < cap ? available : cap;
    }

    public static Vector2 HeroHandCenter(in Rect felt, int handCount, int handIndex, float scale)
    {
        var count = handCount > 0 ? handCount : 1;
        var slotWidth = HeroSlotWidth(felt, count, scale);
        var x = felt.Center.X + (handIndex - (count - 1) * 0.5f) * slotWidth;
        return new Vector2(x, HeroFanY(felt));
    }

    public static Vector2 CapsuleCenter(in Rect felt, float scale)
    {
        return new Vector2(felt.Center.X, felt.Max.Y - CapsuleDrop * scale);
    }

    public static float FanStep(float cardWidth, int cardCount, float maxWidth)
    {
        if (cardCount <= 1)
        {
            return 0f;
        }

        var step = cardWidth * 0.45f;
        var width = cardWidth + step * (cardCount - 1);
        if (width <= maxWidth)
        {
            return step;
        }

        var squeezed = (maxWidth - cardWidth) / (cardCount - 1);
        return squeezed > 0f ? squeezed : 0f;
    }
}
