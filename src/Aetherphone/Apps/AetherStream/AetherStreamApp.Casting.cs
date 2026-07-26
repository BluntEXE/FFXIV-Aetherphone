using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    // Only one target exists right now (Stage 7 scope - see spec's "Render target" section), so
    // this shows that single screen plainly rather than a "1 of N" list implying more should be
    // here. Watch-party features (viewer lists, handing control to another player) are out of
    // scope until the networking question is settled - this tab has nothing referencing them.
    //
    // The screen is self-drawn and world-anchored on the local player - no companion/minion to
    // summon, so there's no "awaiting material"/"apply appearance mod" step anymore (port/
    // alphachannel-engine Stage 4). State just reflects whether the engine's resource hook and
    // Penumbra are both up and running.
    private void DrawCastingTab(Rect body, float scale)
    {
        var margin = Metrics.Space.Lg * scale;
        var content = new Rect(new Vector2(body.Min.X + margin, body.Min.Y + 8f * scale),
            new Vector2(body.Max.X - margin, body.Max.Y));

        var cardHeight = 150f * scale;
        var card = new Rect(content.Min, new Vector2(content.Max.X, content.Min.Y + cardHeight));
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, card.Min, card.Max, 12f * scale, ImGui.GetColorU32(ui.FieldSurface));

        var iconCenter = new Vector2(card.Min.X + 44f * scale, card.Min.Y + 44f * scale);
        drawList.AddCircleFilled(iconCenter, 26f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(StateColor(), 0.18f)), 32);
        AppSkin.Icon(iconCenter, FontAwesomeIcon.Tv.ToIconString(), StateColor(), 1.1f);

        var textLeft = card.Min.X + 84f * scale;
        Typography.Draw(new Vector2(textLeft, card.Min.Y + 18f * scale), Loc.T(L.AetherStream.CastingTarget),
            ui.MutedInk, TextStyles.Caption1);
        Typography.Draw(new Vector2(textLeft, card.Min.Y + 36f * scale), Loc.T(L.AetherStream.CastingThisScreen),
            ui.TitleInk, TextStyles.Headline);
        Typography.Draw(new Vector2(textLeft, card.Min.Y + 62f * scale), StateLabel(), StateColor(),
            TextStyles.Subheadline);

        var bodyTop = card.Max.Y + 12f * scale;

        // Checked live every draw, not cached - Dalamud's InstalledPlugins reflects a fresh
        // install immediately, so this prompt clears itself on the next frame once Penumbra is
        // loaded, with no explicit "reload" step needed.
        if (!PenumbraIPC.IsInstalled())
        {
            Typography.DrawWrappedLeft(new Vector2(content.Min.X, bodyTop), Loc.T(L.AetherStream.PenumbraRequired),
                Palette.WithAlpha(theme.Danger, 0.9f), TextStyles.Footnote, content.Width);
            bodyTop += 36f * scale;

            var getPenumbraRect = new Rect(new Vector2(content.Min.X, bodyTop),
                new Vector2(content.Min.X + 140f * scale, bodyTop + 32f * scale));
            if (SmallButton(getPenumbraRect, Loc.T(L.AetherStream.GetPenumbra), true, scale))
            {
                UrlActions.OpenInBrowser("https://github.com/xivdev/Penumbra");
            }

            bodyTop += 32f * scale + 12f * scale;
        }
        else if (!screen.PenumbraGate.IsAvailable)
        {
            Typography.DrawWrappedLeft(new Vector2(content.Min.X, bodyTop),
                Loc.T(L.AetherStream.PenumbraUnavailable, screen.PenumbraGate.LastError ?? string.Empty),
                Palette.WithAlpha(theme.Danger, 0.9f), TextStyles.Footnote, content.Width);
            bodyTop += 40f * scale;
        }

        // A second, independent render option - a plain resizable, movable window showing the
        // same decoded frames, for whenever the in-world VFX path isn't what's wanted.
        var windowRect = new Rect(new Vector2(content.Min.X, bodyTop), new Vector2(content.Max.X, bodyTop + 38f * scale));
        var windowLabel = screenWindow.IsOpen ? Loc.T(L.AetherStream.CloseScreenWindow) : Loc.T(L.AetherStream.OpenScreenWindow);
        if (SmallButton(windowRect, windowLabel, true, scale))
        {
            screenWindow.IsOpen = !screenWindow.IsOpen;
        }

        bodyTop = windowRect.Max.Y + 12f * scale;

        var stopRect = new Rect(new Vector2(content.Min.X, bodyTop), new Vector2(content.Max.X, bodyTop + 38f * scale));
        var canStop = queue.Current is not null || video.State != VideoPlaybackState.Idle;
        if (SmallButton(stopRect, Loc.T(L.AetherStream.Stop), canStop, scale, danger: true) && canStop)
        {
            queue.Clear();
        }
    }

    private Vector4 StateColor() => screen.State switch
    {
        ScreenState.Ready => new Vector4(0.4f, 1f, 0.5f, 1f),
        _ => ui.MutedInk,
    };

    private string StateLabel() => screen.State switch
    {
        ScreenState.Ready => Loc.T(L.AetherStream.CastingStateReady),
        _ => Loc.T(L.AetherStream.CastingStateNotReady),
    };
}
