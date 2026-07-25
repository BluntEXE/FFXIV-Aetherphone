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
        if (!ScreenPenumbraGate.IsInstalled())
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

        // Manual re-detect - ScanForCompanions already runs every tick on its own, but its
        // "recognized once, keep it until it's actually gone" tolerance (ScreenController's own
        // comment) can leave a stale positive or negative hanging around after re-summoning.
        // Clearing just the local player's tracked state forces a clean re-read on the very next
        // tick without touching playback, unlike Stop (which also clears the queue).
        var refreshRect = new Rect(new Vector2(content.Min.X, bodyTop), new Vector2(content.Max.X, bodyTop + 34f * scale));
        if (SmallButton(refreshRect, Loc.T(L.AetherStream.RefreshDetection), true, scale))
        {
            var localPlayer = Plugin.ObjectTable.LocalPlayer;
            if (localPlayer is not null)
            {
                screen.ClearCompanion(localPlayer.EntityId);
            }
        }

        bodyTop = refreshRect.Max.Y + 12f * scale;

        if (screen.State == ScreenState.AwaitingMaterial)
        {
            var buttonRect = new Rect(new Vector2(content.Min.X, bodyTop),
                new Vector2(content.Max.X, bodyTop + 38f * scale));
            if (DrawSubmitButton(buttonRect, Loc.T(L.AetherStream.ApplyCompanionMod), true))
            {
                screen.ApplyCompanionAppearance(out var error);
                if (error is not null)
                {
                    AepLog.Warning($"[Video] {error}");
                }
            }

            bodyTop = buttonRect.Max.Y + 12f * scale;
        }

        // A second, independent render option - a plain resizable, movable window showing the
        // same decoded frames, for whenever the Penumbra/companion path isn't what's wanted.
        var windowRect = new Rect(new Vector2(content.Min.X, bodyTop), new Vector2(content.Max.X, bodyTop + 38f * scale));
        var windowLabel = screenWindow.IsOpen ? Loc.T(L.AetherStream.CloseScreenWindow) : Loc.T(L.AetherStream.OpenScreenWindow);
        if (SmallButton(windowRect, windowLabel, true, scale))
        {
            screenWindow.IsOpen = !screenWindow.IsOpen;
        }

        bodyTop = windowRect.Max.Y + 12f * scale;

        var stopRect = new Rect(new Vector2(content.Min.X, bodyTop), new Vector2(content.Max.X, bodyTop + 38f * scale));
        var canStop = queue.Current is not null || video.State != VideoPlaybackState.Idle ||
            screen.State != ScreenState.NotSummoned;
        if (SmallButton(stopRect, Loc.T(L.AetherStream.Stop), canStop, scale, danger: true) && canStop)
        {
            queue.Clear();

            // Also resets companion tracking rather than waiting on the automatic scan - the
            // reference implementation doesn't trust that alone for a clean "off" state either,
            // see ClearCompanion's own comment.
            var localPlayer = Plugin.ObjectTable.LocalPlayer;
            if (localPlayer is not null)
            {
                screen.ClearCompanion(localPlayer.EntityId);
            }
        }
    }

    private Vector4 StateColor() => screen.State switch
    {
        ScreenState.Ready => new Vector4(0.4f, 1f, 0.5f, 1f),
        ScreenState.AwaitingMaterial => new Vector4(1f, 0.8f, 0.3f, 1f),
        _ => ui.MutedInk,
    };

    private string StateLabel() => screen.State switch
    {
        ScreenState.Ready => Loc.T(L.AetherStream.CastingStateReady),
        ScreenState.AwaitingMaterial => Loc.T(L.AetherStream.CastingStateAwaitingMaterial),
        _ => Loc.T(L.AetherStream.CastingStateNotSummoned),
    };
}
