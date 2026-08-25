using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal enum CapsuleControl : byte
{
    None,
    Previous,
    PlayPause,
    Next,
    ToggleMute,
    Hangup,
}

internal readonly struct CapsuleControlResult
{
    public readonly CapsuleControl Action;
    public readonly bool Hovered;

    public CapsuleControlResult(CapsuleControl action, bool hovered)
    {
        Action = action;
        Hovered = hovered;
    }
}

internal static class CapsuleRenderer
{
    private const float DiscRadius = 11f;
    private const float TitleWidth = 78f;
    private const float TitleGap = 8f;
    private const float EqualizerWidth = 14f;
    private const float EqualizerHeight = 16f;
    private const float CallDotInset = 4f;
    private const float CallNameLeft = 16f;
    private const float CallNameWidth = 72f;
    private const float BadgeTile = 22f;
    private const float BadgePillHeight = 14f;
    private const float CardPadding = 13f;
    private const float CardTile = 30f;
    private const float CardTextGap = 10f;
    private const float CardTitleRise = 17f;
    private const float CardBodyDrop = 1f;
    private const float TransportSmall = 12f;
    private const float TransportLarge = 14f;
    private const float TransportStride = 40f;
    private const float CallButtonRadius = 15f;
    private const float CallButtonSpread = 26f;
    private const float CallButtonIcon = 13f;
    private const float PowerIcon = 15f;
    private static readonly Vector4 MusicAccent = AppAccents.For("music");
    private static readonly Vector4 CallAccent = new(0.20f, 0.78f, 0.35f, 1f);
    private static readonly Vector4 BadgeTone = new(0.90f, 0.22f, 0.19f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);

    public static void DrawMoon(ImDrawListPtr dl, Vector2 center, float height, float alpha)
    {
        ProgressRing.CenterIcon(dl, center, FontAwesomeIcon.Moon, Palette.WithAlpha(StatusBar.DndTone, alpha), height);
    }

    public static void DrawMusicCompact(ImDrawListPtr dl, Rect slot, PlaybackHub playback, float clock, float alpha,
        bool hovering, float scale, PhoneTheme theme)
    {
        var centerY = slot.Center.Y;
        var radius = DiscRadius * scale;
        var discCenter = new Vector2(slot.Min.X + radius, centerY);
        ArtGradient.DrawDisc(dl, discCenter, radius, ArtGradient.FromName(playback.Title), alpha);
        var style = new TextStyle(Text(0.88f), FontWeight.SemiBold);
        var titleHeight = Typography.Measure(playback.Title, style).Y;
        var titleLeft = discCenter.X + radius + TitleGap * scale;
        Marquee.DrawLeft(dl, "minimized.music.title", playback.Title, titleLeft, centerY - titleHeight * 0.5f,
            TitleWidth * scale, style, Palette.WithAlpha(theme.TextStrong, alpha), hovering);
        var equalizerCenter = new Vector2(slot.Max.X - EqualizerWidth * scale * 0.5f, centerY);
        Equalizer.Draw(dl, equalizerCenter, scale, EqualizerHeight * scale, clock, MusicAccent, alpha,
            playback.IsPlaying);
    }

    public static void DrawCallCompact(ImDrawListPtr dl, Rect slot, in CallView view, string status, float clock,
        float alpha, float scale, PhoneTheme theme)
    {
        var centerY = slot.Center.Y;
        var pulse = 0.5f + 0.5f * MathF.Sin(clock * 3f);
        var dotCenter = new Vector2(slot.Min.X + CallDotInset * scale, centerY);
        dl.AddCircleFilled(dotCenter, (3.4f + 1.2f * pulse) * scale,
            ImGui.GetColorU32(Palette.WithAlpha(CallAccent, alpha)), 16);
        var nameScale = Text(0.86f);
        var name = Typography.FitText(view.PeerLabel, CallNameWidth * scale, nameScale, FontWeight.SemiBold);
        var nameSize = Typography.Measure(name, nameScale, FontWeight.SemiBold);
        Typography.Draw(dl, new Vector2(slot.Min.X + CallNameLeft * scale, centerY - nameSize.Y * 0.5f), name,
            Palette.WithAlpha(theme.TextStrong, alpha), nameScale, FontWeight.SemiBold);
        var statusScale = Text(0.8f);
        var statusSize = Typography.Measure(status, statusScale, FontWeight.Medium);
        Typography.Draw(dl, new Vector2(slot.Max.X - statusSize.X, centerY - statusSize.Y * 0.5f), status,
            Palette.WithAlpha(CallAccent, 0.95f * alpha), statusScale, FontWeight.Medium);
    }

    public static void DrawBadge(ImDrawListPtr dl, Rect slot, string appId, Vector4 accent, string count,
        PhoneTheme theme, float alpha, float scale)
    {
        var tile = BadgeTile * scale;
        var centerY = slot.Center.Y;
        var tileMin = new Vector2(slot.Min.X, centerY - tile * 0.5f);
        var tileMax = new Vector2(slot.Min.X + tile, centerY + tile * 0.5f);
        var tileCenter = (tileMin + tileMax) * 0.5f;
        var surface = IconTile.Surface(accent);
        Squircle.Fill(dl, tileMin, tileMax, tile * Metrics.Radius.TileFactor,
            ImGui.GetColorU32(Palette.WithAlpha(surface, alpha)));
        var ink = Palette.WithAlpha(AccentRing.Ink, alpha);
        if (!AppIconArt.TryDraw(dl, appId, tileCenter, tile, ink, Palette.WithAlpha(surface, alpha)))
        {
            ProgressRing.CenterIcon(dl, tileCenter, FontAwesomeIcon.Bell, ink, tile * 0.5f);
        }

        var countScale = Text(0.6f);
        var countSize = Typography.Measure(count, countScale, FontWeight.Bold);
        var pillHeight = BadgePillHeight * scale;
        var pillWidth = MathF.Max(pillHeight, countSize.X + 8f * scale);
        var pillCenter = new Vector2(tileMax.X - 2f * scale, tileMin.Y + 2f * scale);
        var pillMin = new Vector2(pillCenter.X - pillWidth * 0.5f, pillCenter.Y - pillHeight * 0.5f);
        var pillMax = new Vector2(pillCenter.X + pillWidth * 0.5f, pillCenter.Y + pillHeight * 0.5f);
        var outline = 1.5f * scale;
        dl.AddRectFilled(pillMin - new Vector2(outline, outline), pillMax + new Vector2(outline, outline),
            ImGui.GetColorU32(Palette.WithAlpha(theme.ScreenBase, alpha)), pillHeight * 0.5f + outline);
        dl.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(Palette.WithAlpha(BadgeTone, alpha)), pillHeight * 0.5f);
        Typography.Draw(dl, pillCenter - countSize * 0.5f, count, Palette.WithAlpha(White, alpha), countScale,
            FontWeight.Bold);
    }

    public static CapsuleControlResult DrawMusicExpanded(ImDrawListPtr dl, Rect row, PlaybackHub playback,
        PhoneTheme theme, float alpha, bool active, float scale)
    {
        var centerY = row.Center.Y;
        var right = row.Max.X;
        var small = TransportSmall * scale;
        var large = TransportLarge * scale;
        var stride = TransportStride * scale;
        var hasQueue = playback.HasQueue;
        var playCenter = new Vector2(right - (hasQueue ? stride + large : large), centerY);
        var prevCenter = new Vector2(playCenter.X - stride, centerY);
        var nextCenter = new Vector2(playCenter.X + stride, centerY);
        var hovered = active && (Hovered(playCenter, large) || hasQueue && (Hovered(prevCenter, small) ||
                                                                              Hovered(nextCenter, small)));
        var controlsLeft = hasQueue ? prevCenter.X - small : playCenter.X - large;
        var subtitleScale = Text(0.8f);
        var subtitleWidth = MathF.Max(1f, controlsLeft - CardTextGap * scale - row.Min.X);
        var subtitle = Typography.FitText(playback.Subtitle, subtitleWidth, subtitleScale, FontWeight.Regular);
        var subtitleSize = Typography.Measure(subtitle, subtitleScale, FontWeight.Regular);
        Typography.Draw(dl, new Vector2(row.Min.X, centerY - subtitleSize.Y * 0.5f), subtitle,
            Palette.WithAlpha(theme.TextMuted, alpha), subtitleScale, FontWeight.Regular);
        var action = CapsuleControl.None;
        var ink = theme.TextStrong;
        if (hasQueue)
        {
            if (TransportButton.Draw(prevCenter, small, TransportAction.Previous, MusicAccent, ink, alpha, active, dl))
            {
                action = CapsuleControl.Previous;
            }

            if (TransportButton.Draw(nextCenter, small, TransportAction.Next, MusicAccent, ink, alpha, active, dl))
            {
                action = CapsuleControl.Next;
            }
        }

        if (TransportButton.Draw(playCenter, large, playback.IsPlaying ? TransportAction.Pause : TransportAction.Play,
                MusicAccent, ink, alpha, active, dl))
        {
            action = CapsuleControl.PlayPause;
        }

        return new CapsuleControlResult(action, hovered);
    }

    public static CapsuleControlResult DrawCallExpanded(ImDrawListPtr dl, Rect row, in CallView view,
        PhoneTheme theme, float alpha, bool active, float scale)
    {
        var centerY = row.Center.Y;
        var centerX = row.Center.X;
        var radius = CallButtonRadius * scale;
        var muteCenter = new Vector2(centerX - CallButtonSpread * scale, centerY);
        var hangupCenter = new Vector2(centerX + CallButtonSpread * scale, centerY);
        var muteFill = view.Muted ? CallAccent : Palette.WithAlpha(theme.TextStrong, 0.18f);
        var action = CapsuleControl.None;
        var muteHovered = active && Hovered(muteCenter, radius);
        var hangupHovered = active && Hovered(hangupCenter, radius);
        if (RoundButton(dl, muteCenter, radius, view.Muted ? FontAwesomeIcon.MicrophoneSlash : FontAwesomeIcon.Microphone,
                muteFill, theme.TextStrong, alpha, muteHovered, scale))
        {
            action = CapsuleControl.ToggleMute;
        }

        if (RoundButton(dl, hangupCenter, radius, FontAwesomeIcon.PhoneSlash, theme.Danger, White, alpha,
                hangupHovered, scale))
        {
            action = CapsuleControl.Hangup;
        }

        return new CapsuleControlResult(action, muteHovered || hangupHovered);
    }

    public static void DrawCard(ImDrawListPtr dl, in ChassisGeometry geometry, PhoneNotification notification,
        PhoneTheme theme, float alpha, float scale, bool hovered)
    {
        var screen = geometry.Screen;
        var accent = notification.Accent;
        Squircle.Stroke(dl, geometry.Glass.Min, geometry.Glass.Max, geometry.GlassRadius,
            ImGui.GetColorU32(Palette.WithAlpha(accent, (hovered ? 0.7f : 0.45f) * alpha)), 1.5f * scale);
        var tile = CardTile * scale;
        var centerY = screen.Center.Y;
        var tileMin = new Vector2(screen.Min.X + CardPadding * scale, centerY - tile * 0.5f);
        var tileMax = new Vector2(tileMin.X + tile, centerY + tile * 0.5f);
        var tileCenter = (tileMin + tileMax) * 0.5f;
        var surface = IconTile.Surface(accent);
        Squircle.Fill(dl, tileMin, tileMax, tile * Metrics.Radius.TileFactor,
            ImGui.GetColorU32(Palette.WithAlpha(surface, alpha)));
        var ink = Palette.WithAlpha(AccentRing.Ink, alpha);
        if (!AppIconArt.TryDraw(dl, notification.AppId, tileCenter, tile, ink, Palette.WithAlpha(surface, alpha)))
        {
            var initial = notification.Title.Length > 0 ? notification.Title.Substring(0, 1) : "?";
            Typography.DrawCentered(dl, tileCenter, initial, ink, Text(1.0f), FontWeight.SemiBold);
        }

        var textLeft = tileMax.X + CardTextGap * scale;
        var textWidth = MathF.Max(1f, screen.Max.X - CardPadding * scale - textLeft);
        var titleStyle = new TextStyle(Text(0.9f), FontWeight.SemiBold);
        var bodyStyle = new TextStyle(Text(0.8f), FontWeight.Regular);
        Marquee.DrawLeftAuto(dl, "minimized.card.title", notification.Title, textLeft, centerY - CardTitleRise * scale,
            textWidth, titleStyle, Palette.WithAlpha(theme.TextStrong, alpha));
        Marquee.DrawLeftAuto(dl, "minimized.card.body", notification.SingleLineBody, textLeft,
            centerY + CardBodyDrop * scale, textWidth, bodyStyle, Palette.WithAlpha(theme.TextMuted, alpha));
    }

    public static void DrawHoldSweep(ImDrawListPtr dl, in ChassisGeometry geometry, PhoneTheme theme, float progress,
        float scale)
    {
        var screen = geometry.Screen;
        Squircle.Fill(dl, screen.Min, screen.Max, geometry.ScreenRadius,
            ImGui.GetColorU32(Palette.WithAlpha(Black, 0.35f * progress)));
        var sweepRight = screen.Min.X + screen.Width * progress;
        dl.PushClipRect(screen.Min, new Vector2(sweepRight, screen.Max.Y), true);
        Squircle.Fill(dl, screen.Min, screen.Max, geometry.ScreenRadius,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Danger, 0.62f)));
        dl.PopClipRect();
        ProgressRing.CenterIcon(dl, screen.Center, FontAwesomeIcon.PowerOff, Palette.WithAlpha(White, progress),
            PowerIcon * scale);
    }

    public static void DrawPulse(ImDrawListPtr dl, in ChassisGeometry geometry, Vector4 accent, float strength,
        float scale)
    {
        var glass = geometry.Glass;
        var inner = 1f * scale;
        var outer = 4f * scale;
        Squircle.Stroke(dl, glass.Min - new Vector2(inner, inner), glass.Max + new Vector2(inner, inner),
            geometry.GlassRadius + inner, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.75f * strength)), 2f * scale);
        Squircle.Stroke(dl, glass.Min - new Vector2(outer, outer), glass.Max + new Vector2(outer, outer),
            geometry.GlassRadius + outer, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.28f * strength)), 3f * scale);
    }

    private static bool RoundButton(ImDrawListPtr dl, Vector2 center, float radius, FontAwesomeIcon icon, Vector4 fill,
        Vector4 ink, float alpha, bool hovered, float scale)
    {
        var color = hovered ? Palette.Mix(fill, White, 0.14f) : fill;
        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(color, alpha * color.W)), 28);
        ProgressRing.CenterIcon(dl, center, icon, Palette.WithAlpha(ink, alpha), CallButtonIcon * scale);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static bool Hovered(Vector2 center, float radius) =>
        UiInteract.Hover(center - new Vector2(radius, radius), center + new Vector2(radius, radius));

    private static float Text(float scale) => scale / UiScale.Phone;
}
