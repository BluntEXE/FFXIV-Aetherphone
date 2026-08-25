using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Strats;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Strats;

internal sealed partial class StratsApp
{
    private void DrawViewer(Rect area, StratsView view)
    {
        var scale = UiScale.Current;
        var current = resolved;
        if (current is null || view.PhaseIndex < 0 || view.PhaseIndex >= current.Phases.Length)
        {
            router.Pop();
            return;
        }

        var phase = current.Phases[view.PhaseIndex];
        ImageRef? image;
        SpotlightMask? mask;
        string title;
        if (view.MechIndex < 0)
        {
            image = phase.Image;
            mask = phase.Spotlight;
            title = phase.Name;
        }
        else if (view.MechIndex < phase.Mechs.Length)
        {
            var mech = phase.Mechs[view.MechIndex];
            image = view.PlayerImage ? mech.PlayerImage : mech.Image;
            mask = view.PlayerImage ? mech.PlayerSpotlight : null;
            title = mech.Name;
        }
        else
        {
            router.Pop();
            return;
        }

        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, title, back);
        var top = area.Min.Y + AppHeader.Height * scale;
        var pad = Metrics.Space.Sm * scale;
        var stage = new Rect(new Vector2(area.Min.X + pad, top + pad), new Vector2(area.Max.X - pad, area.Max.Y - pad));
        if (image is null)
        {
            router.Pop();
            return;
        }

        var texture = images.Sized(StratsContent.Url(image.Key), stage.Width * 2f);
        if (texture is null)
        {
            LoadingPulse.Draw(stage.Center, 13f * scale, ui.Accent, AppPalettes.Strats.MutedInk,
                Loc.T(L.Strats.GuideLoading));
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        zoom.Draw(stage, texture, theme, Metrics.Radius.Md * scale);
        if (mask is null)
        {
            return;
        }

        var fit = PhotoZoomView.FitScale(stage, texture.Size);
        var drawnSize = texture.Size * fit * zoom.Zoom;
        var center = stage.Center + zoom.Pan;
        var frame = new Rect(center - drawnSize * 0.5f, center + drawnSize * 0.5f);
        drawList.PushClipRect(stage.Min, stage.Max, true);
        SpotlightImage.DrawOverlay(drawList, frame, texture, mask, scale);
        drawList.PopClipRect();
    }
}
