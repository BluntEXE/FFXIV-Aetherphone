using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Chirper;

internal static class ChirperInk
{
    public static Vector4 Accent => AppPalettes.Chirper.Accent;
    public static Vector4 TitleInk => AppPalettes.Chirper.TitleInk;
    public static Vector4 BodyInk => AppPalettes.Chirper.BodyInk;
    public static Vector4 MutedInk => AppPalettes.Chirper.MutedInk;
    public static Vector4 BackdropTop => AppPalettes.Chirper.BackdropTop;

    public static readonly Vector4 FaintInk = Palette.WithAlpha(AppPalettes.Chirper.MutedInk, 0.62f);
    public static readonly Vector4 AccentDeep = Palette.Darken(AppPalettes.Chirper.Accent, 0.22f);
    public static readonly Vector4 AccentLink = Palette.Lighten(AppPalettes.Chirper.Accent, 0.18f);
    public static readonly Vector4 AccentWash = Palette.WithAlpha(AppPalettes.Chirper.Accent, 0.14f);
    public static Vector4 Hairline => AppPalettes.Chirper.Hairline;
    public static readonly Vector4 ChipFill = new(1f, 1f, 1f, 0.055f);
    public static readonly Vector4 ChipStroke = new(1f, 1f, 1f, 0.08f);
    public static readonly Vector4 ChipHover = new(1f, 1f, 1f, 0.09f);
    public static readonly Vector4 MineFill = Palette.WithAlpha(AppPalettes.Chirper.Accent, 0.15f);
    public static readonly Vector4 MineStroke = Palette.WithAlpha(AppPalettes.Chirper.Accent, 0.48f);
    public static readonly Vector4 MineInk = Palette.Lighten(AppPalettes.Chirper.Accent, 0.38f);
    public static readonly Vector4 RechirpGreen = new(0.188f, 0.820f, 0.345f, 1f);
    public static readonly Vector4 Danger = new(1f, 0.373f, 0.420f, 1f);
    public static readonly Vector4 Warning = new(1f, 0.690f, 0.180f, 1f);
    public static readonly Vector4 LikeRed = new(1f, 0.216f, 0.373f, 1f);
    public static Vector4 HoverTint => AppPalettes.Chirper.HoverWash;
    public static readonly Vector4 QuoteFill = new(1f, 1f, 1f, 0.028f);
    public static readonly Vector4 QuoteHover = new(1f, 1f, 1f, 0.05f);
    public static readonly Vector4 QuoteBodyInk = Palette.WithAlpha(AppPalettes.Chirper.BodyInk, 0.85f);
    public static readonly Vector4 SegmentTrack = new(1f, 1f, 1f, 0.07f);
    public static readonly Vector4 SegmentThumb = new(1f, 1f, 1f, 0.16f);
    public static readonly Vector4 SegmentIdleInk = Palette.WithAlpha(AppPalettes.Chirper.MutedInk, 0.95f);
    public static readonly Vector4 GlassPanel =
        Palette.WithAlpha(Palette.Lighten(AppPalettes.Chirper.BackdropTop, 0.10f), 0.92f);
    public static readonly Vector4 GlassStroke = new(1f, 1f, 1f, 0.12f);
    public static readonly Vector4 ComposerGlass = Palette.WithAlpha(AppPalettes.Chirper.BackdropTop, 0.78f);
    public static readonly Vector4 FieldFill = new(1f, 1f, 1f, 0.08f);
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);
}
