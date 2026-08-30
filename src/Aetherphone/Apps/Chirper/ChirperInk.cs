using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Chirper;

internal static class ChirperInk
{
    public static readonly SocialInk Shared = new(AppPalettes.Chirper);

    public static Vector4 Accent => Shared.Accent;
    public static Vector4 TitleInk => Shared.TitleInk;
    public static Vector4 BodyInk => Shared.BodyInk;
    public static Vector4 MutedInk => Shared.MutedInk;
    public static Vector4 BackdropTop => Shared.BackdropTop;
    public static Vector4 FaintInk => Shared.FaintInk;
    public static Vector4 AccentDeep => Shared.AccentDeep;
    public static Vector4 AccentLink => Shared.AccentLink;
    public static Vector4 AccentWash => Shared.AccentWash;
    public static Vector4 Hairline => Shared.Hairline;
    public static Vector4 ChipFill => Shared.ChipFill;
    public static Vector4 ChipStroke => Shared.ChipStroke;
    public static Vector4 ChipHover => Shared.ChipHover;
    public static Vector4 Danger => Shared.Danger;
    public static Vector4 LikeRed => Shared.LikeRed;
    public static Vector4 HoverTint => Shared.HoverTint;
    public static Vector4 SegmentIdleInk => Shared.SegmentIdleInk;
    public static Vector4 GlassPanel => Shared.GlassPanel;
    public static Vector4 GlassStroke => Shared.GlassStroke;
    public static Vector4 FieldFill => Shared.FieldFill;
    public static Vector4 White => Shared.White;

    public static readonly Vector4 MineFill = Palette.WithAlpha(AppPalettes.Chirper.Accent, 0.15f);
    public static readonly Vector4 MineStroke = Palette.WithAlpha(AppPalettes.Chirper.Accent, 0.48f);
    public static readonly Vector4 MineInk = Palette.Lighten(AppPalettes.Chirper.Accent, 0.38f);
    public static readonly Vector4 RechirpGreen = new(0.188f, 0.820f, 0.345f, 1f);
    public static readonly Vector4 Warning = new(1f, 0.690f, 0.180f, 1f);
    public static readonly Vector4 QuoteFill = new(1f, 1f, 1f, 0.028f);
    public static readonly Vector4 QuoteHover = new(1f, 1f, 1f, 0.05f);
    public static readonly Vector4 QuoteBodyInk = Palette.WithAlpha(AppPalettes.Chirper.BodyInk, 0.85f);
    public static readonly Vector4 SegmentTrack = new(1f, 1f, 1f, 0.07f);
}
