using Aetherphone.Core;
using Aetherphone.Core.Video;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

// Stage 3/6 spike window: decode a local video file, draw it locally, and optionally push it
// onto the in-game TV model - off the phone shell entirely. No URL handling, no phone UI yet -
// see docs/port-plan.md.
internal sealed class VideoDebugWindow : Window, IDisposable
{
    private readonly VideoPlayer video;
    private readonly ScreenController screen;
    private string path = string.Empty;
    private bool sendToTv;
    private IDalamudTextureWrap? texture;

    public VideoDebugWindow(VideoPlayer video, ScreenController screen)
        : base("Aetherphone: Video Decode Debug (Stage 3/6)###AetherphoneVideoDebug")
    {
        this.video = video;
        this.screen = screen;
        Size = new Vector2(760, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        DrawTvSection();
        ImGui.Separator();
        DrawPlaybackSection();
    }

    private void DrawTvSection()
    {
        ImGui.TextUnformatted("TV (Stage 6)");
        ImGui.TextUnformatted($"State: {screen.State}");
        ImGui.TextUnformatted($"Resource hook healthy: {screen.IsHookHealthy}");

        if (!screen.PenumbraGate.IsAvailable)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f),
                $"Penumbra unavailable: {screen.PenumbraGate.LastError}");
        }

        switch (screen.State)
        {
            case ScreenState.NotSummoned:
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f),
                    "No Carbuncle summoned nearby - summon one to play video on it.");
                break;
            case ScreenState.AwaitingMaterial:
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f),
                    "Carbuncle found, but it doesn't have the screen material yet.");
                if (ImGui.Button("Apply companion appearance mod"))
                {
                    screen.ApplyCompanionAppearance(out var error);
                    if (error is not null)
                    {
                        AepLog.Warning($"[Video] {error}");
                    }
                }

                break;
            case ScreenState.Ready:
                ImGui.TextColored(new Vector4(0.4f, 1f, 0.5f, 1f), "Screen ready.");
                break;
        }

        ImGui.Checkbox("Send decoded frames to the TV", ref sendToTv);
    }

    private void DrawPlaybackSection()
    {
        ImGui.InputText("Local file path or YouTube URL", ref path, 1000);
        ImGui.SameLine();
        if (ImGui.Button("Play"))
        {
            var localPlayer = Plugin.ObjectTable.LocalPlayer;
            if (localPlayer is not null)
            {
                screen.SetActive(localPlayer.EntityId);
            }

            video.Play(path);
        }

        ImGui.SameLine();
        if (ImGui.Button("Stop"))
        {
            video.Stop();
            screen.ClearActive();
        }

        ImGui.Text($"State: {video.State}");
        if (video.LastError is not null)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), video.LastError);
        }

        var (position, duration, paused) = video.GetProgress();
        ImGui.Text($"Position: {position:F1}s / {duration:F1}s   Paused: {paused}");

        var frame = video.TryGetFrame(out var width, out var height);
        if (frame is null)
        {
            ImGui.TextDisabled("No frame yet.");
            return;
        }

        if (sendToTv)
        {
            screen.PushFrame(frame, width, height);
        }

        texture?.Dispose();
        texture = Plugin.TextureProvider.CreateFromRaw(RawImageSpecification.Bgra32(width, height), frame,
            "Aetherphone.VideoDebug.Frame");

        var avail = ImGui.GetContentRegionAvail();
        var aspect = (float)width / height;
        var drawWidth = avail.X;
        var drawHeight = drawWidth / aspect;
        if (drawHeight > avail.Y && avail.Y > 0f)
        {
            drawHeight = avail.Y;
            drawWidth = drawHeight * aspect;
        }

        ImGui.Image(texture.Handle, new Vector2(drawWidth, drawHeight));
    }

    public void Dispose()
    {
        video.Stop();
        screen.ClearActive();
        texture?.Dispose();
        texture = null;
    }
}
