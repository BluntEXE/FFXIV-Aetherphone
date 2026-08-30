using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Aethergram;

internal static class AethergramInk
{
    public static readonly SocialInk Shared = new(AppPalettes.Aethergram);

    public static Vector4 Accent => Shared.Accent;
    public static Vector4 AccentDeep => Shared.AccentDeep;
    public static Vector4 AccentLink => Shared.AccentLink;
    public static Vector4 AccentWash => Shared.AccentWash;
    public static Vector4 TitleInk => Shared.TitleInk;
    public static Vector4 BodyInk => Shared.BodyInk;
    public static Vector4 MutedInk => Shared.MutedInk;
    public static Vector4 FaintInk => Shared.FaintInk;
    public static Vector4 SegmentIdleInk => Shared.SegmentIdleInk;
    public static Vector4 Hairline => Shared.Hairline;
    public static Vector4 HoverTint => Shared.HoverTint;
    public static Vector4 BackdropTop => Shared.BackdropTop;
    public static Vector4 FieldFill => Shared.FieldFill;
    public static Vector4 ButtonFill => Shared.ButtonFill;
    public static Vector4 ButtonHover => Shared.ButtonHover;
    public static Vector4 ChipFill => Shared.ChipFill;
    public static Vector4 ChipStroke => Shared.ChipStroke;
    public static Vector4 ChipHover => Shared.ChipHover;
    public static Vector4 GlassPanel => Shared.GlassPanel;
    public static Vector4 GlassStroke => Shared.GlassStroke;
    public static Vector4 ThumbFill => Shared.ThumbFill;
    public static Vector4 Scrim => Shared.Scrim;
    public static Vector4 White => Shared.White;
    public static Vector4 LikeRed => Shared.LikeRed;
    public static Vector4 Danger => Shared.Danger;
    public static Vector4 PresenceGreen => Shared.PresenceGreen;

    public static readonly Vector4 SeenRing = new(1f, 1f, 1f, 0.28f);

    public static readonly Vector4[] StoryRingStops =
    [
        new(1f, 0.863f, 0.502f, 1f), new(0.969f, 0.435f, 0.216f, 1f), new(0.882f, 0.188f, 0.424f, 1f),
        new(0.514f, 0.227f, 0.706f, 1f),
    ];
}
