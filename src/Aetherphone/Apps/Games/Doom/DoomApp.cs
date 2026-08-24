using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ManagedDoom;

namespace Aetherphone.Apps.Games.Doom;

internal sealed class DoomApp : IMiniGame
{
    private const string GameId = "doom";
    private const float ScreenAspect = 4f / 3f;
    private const float MinimumControlsHeight = 150f;
    private const float KeySize = 46f;
    private const float KeyGap = 8f;
    private const float CardHeight = 66f;
    private readonly DoomAssets assets = new();
    private DoomRuntime? runtime;
    private string? failure;
    private bool dragging;
    private float lastDragX;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Doom);
    public string Genre => Loc.T(L.Games.GenreArcade);
    public bool RunsOnAClock => true;

    public void Open()
    {
        assets.RefreshStates();
        failure = null;
    }

    public void Close()
    {
        DisposeRuntime();
    }

    public void Dispose()
    {
        DisposeRuntime();
        assets.Dispose();
    }

    private void DisposeRuntime()
    {
        runtime?.Dispose();
        runtime = null;
        dragging = false;
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        var body = context.Body;
        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        var iwad = assets.IwadPath();
        if (iwad is null)
        {
            DrawSetup(body, theme, scale);
            return;
        }

        if (failure is not null)
        {
            DrawFailure(body, theme, scale);
            return;
        }

        if (runtime is null || runtime.Finished)
        {
            DisposeRuntime();
            if (!TryStart(iwad))
            {
                return;
            }
        }

        DrawGame(context, body, theme, scale);
    }

    private bool TryStart(string iwad)
    {
        try
        {
            runtime = new DoomRuntime(iwad, assets.SoundfontPath(), assets.Folder);
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Error(exception, "[Doom] The engine could not start.");
            failure = exception.Message;
            runtime = null;
            return false;
        }
    }

    private void DrawGame(in GameContext context, Rect body, PhoneTheme theme, float scale)
    {
        var active = runtime!;
        var pad = 6f * scale;
        var topRow = body.Min.Y + 22f * scale;
        var screenWidth = body.Width - pad * 2f;
        var controlsHeight = MathF.Max(MinimumControlsHeight * scale, body.Height * 0.3f);
        var screenHeight = MathF.Min(screenWidth / ScreenAspect, body.Max.Y - controlsHeight - topRow - 24f * scale);
        screenWidth = MathF.Min(screenWidth, screenHeight * ScreenAspect);
        var screenMin = new Vector2(body.Center.X - screenWidth * 0.5f, topRow + 22f * scale);
        var screen = new Rect(screenMin, screenMin + new Vector2(screenWidth, screenHeight));
        var controls = new Rect(new Vector2(body.Min.X, screen.Max.Y + pad), body.Max);
        var running = context.DeltaSeconds > 0f;
        active.Muted = !running;
        var keyboard = running && GameInput.Claim();
        ReadControls(active, controls, screen, theme, keyboard);
        try
        {
            active.Tick(context.DeltaSeconds, keyboard);
            active.Render();
        }
        catch (Exception exception)
        {
            AepLog.Error(exception, "[Doom] The engine stopped.");
            failure = exception.Message;
            DisposeRuntime();
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var frame = new Rect(screen.Min - new Vector2(pad, pad), screen.Max + new Vector2(pad, pad));
        GameScene.Arena(drawList, frame, 12f * scale, scale, Accent);
        active.Present(drawList, screen);
        Typography.DrawCentered(drawList, new Vector2(body.Center.X, topRow), Loc.T(L.Games.DoomControls),
            theme.TextMuted, TextStyles.Caption1);
        if (GameHud.Button(new Vector2(body.Max.X - 44f * scale, topRow), new Vector2(64f * scale, 26f * scale),
                Loc.T(L.Games.DoomMenu), Accent, theme))
        {
            active.Input.Tap(active.Doom, DoomKey.Escape);
        }
    }

    private void ReadControls(DoomRuntime active, Rect controls, Rect screen, PhoneTheme theme, bool keyboard)
    {
        var input = active.Input;
        var scale = UiScale.Current;
        var key = MathF.Min(KeySize * scale, (controls.Height - KeyGap * scale * 3f) / 3f);
        var gap = KeyGap * scale;
        var half = key * 0.5f;
        var leftCenter = new Vector2(controls.Min.X + key * 1.5f + gap * 2f, controls.Center.Y);
        var size = new Vector2(key, key);
        var forwardMin = new Vector2(leftCenter.X - half, leftCenter.Y - half - key - gap);
        var backMin = new Vector2(leftCenter.X - half, leftCenter.Y + half + gap);
        var strafeLeftMin = new Vector2(leftCenter.X - half - key - gap, leftCenter.Y - half);
        var strafeRightMin = new Vector2(leftCenter.X + half + gap, leftCenter.Y - half);
        var padForward = HeldKey(forwardMin, forwardMin + size, "W", theme, scale, out _);
        var padBack = HeldKey(backMin, backMin + size, "S", theme, scale, out _);
        var padStrafeLeft = HeldKey(strafeLeftMin, strafeLeftMin + size, "A", theme, scale, out _);
        var padStrafeRight = HeldKey(strafeRightMin, strafeRightMin + size, "D", theme, scale, out _);
        var rightCenter = new Vector2(controls.Max.X - key * 1.5f - gap * 2f, controls.Center.Y);
        var turnLeftMin = new Vector2(rightCenter.X - key - gap * 0.5f, rightCenter.Y - half - key - gap);
        var turnRightMin = new Vector2(rightCenter.X + gap * 0.5f, rightCenter.Y - half - key - gap);
        var fireMin = new Vector2(rightCenter.X - key - gap * 0.5f, rightCenter.Y - half);
        var useMin = new Vector2(rightCenter.X - key - gap * 0.5f, rightCenter.Y + half + gap);
        var padTurnLeft = HeldKey(turnLeftMin, turnLeftMin + size, "<", theme, scale, out _);
        var padTurnRight = HeldKey(turnRightMin, turnRightMin + size, ">", theme, scale, out _);
        var wide = new Vector2(key * 2f + gap, key);
        var padFire = HeldKey(fireMin, fireMin + wide, Loc.T(L.Games.DoomFire), theme, scale, out var firePressed);
        var padUse = HeldKey(useMin, useMin + wide, Loc.T(L.Games.DoomUse), theme, scale, out var usePressed);
        input.SetHeld(DoomAction.Forward, padForward || (keyboard && (ImGui.IsKeyDown(ImGuiKey.W) || ImGui.IsKeyDown(ImGuiKey.UpArrow))));
        input.SetHeld(DoomAction.Backward, padBack || (keyboard && (ImGui.IsKeyDown(ImGuiKey.S) || ImGui.IsKeyDown(ImGuiKey.DownArrow))));
        input.SetHeld(DoomAction.StrafeLeft, padStrafeLeft || (keyboard && ImGui.IsKeyDown(ImGuiKey.A)));
        input.SetHeld(DoomAction.StrafeRight, padStrafeRight || (keyboard && ImGui.IsKeyDown(ImGuiKey.D)));
        input.SetHeld(DoomAction.TurnLeft, padTurnLeft || (keyboard && ImGui.IsKeyDown(ImGuiKey.LeftArrow)));
        input.SetHeld(DoomAction.TurnRight, padTurnRight || (keyboard && ImGui.IsKeyDown(ImGuiKey.RightArrow)));
        input.SetHeld(DoomAction.Fire, padFire || (keyboard && (ImGui.IsKeyDown(ImGuiKey.Space) || ImGui.IsKeyDown(ImGuiKey.LeftCtrl))));
        input.SetHeld(DoomAction.Use, padUse || (keyboard && (ImGui.IsKeyDown(ImGuiKey.E) || ImGui.IsKeyDown(ImGuiKey.LeftShift))));
        for (var weapon = 0; weapon < 7; weapon++)
        {
            input.SetWeapon(weapon, keyboard && ImGui.IsKeyDown(ImGuiKey.Key1 + weapon));
        }

        if (active.InMenu)
        {
            if (firePressed)
            {
                input.Tap(active.Doom, DoomKey.Enter);
            }

            if (usePressed)
            {
                input.Tap(active.Doom, DoomKey.Escape);
            }
        }

        ReadDrag(input, screen);
    }

    private void ReadDrag(DoomInput input, Rect screen)
    {
        var mouse = ImGui.GetMousePos();
        if (!dragging)
        {
            if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left) || !UiInteract.Hover(screen.Min, screen.Max))
            {
                return;
            }

            dragging = true;
            lastDragX = mouse.X;
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            dragging = false;
            return;
        }

        input.AddTurn(mouse.X - lastDragX);
        lastDragX = mouse.X;
    }

    private bool HeldKey(Vector2 min, Vector2 max, string glyph, PhoneTheme theme, float scale, out bool pressed)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(min, max);
        var held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        pressed = hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        var radius = (max.Y - min.Y) * 0.28f;
        Material.Frosted(drawList, min, max, radius, scale, held ? 1f : 0.85f);
        if (held)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Accent with { W = 0.32f }));
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Accent with { W = 0.9f }), 1.5f * scale);
        }
        else if (hovered)
        {
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Accent with { W = 0.45f }), 1f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(drawList, (min + max) * 0.5f, glyph, held ? Accent : theme.TextStrong,
            TextStyles.Headline.Scale, TextStyles.Headline.Weight);
        return held;
    }

    private void DrawSetup(Rect body, PhoneTheme theme, float scale)
    {
        assets.RefreshStates();
        var drawList = ImGui.GetWindowDrawList();
        var margin = 18f * scale;
        var content = new Rect(body.Min + new Vector2(margin, margin), body.Max - new Vector2(margin, margin));
        var tileSize = 64f * scale;
        var tileMin = new Vector2(content.Center.X - tileSize * 0.5f, content.Min.Y + 12f * scale);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tileSize, tileSize),
            tileSize * Metrics.Radius.TileFactor, IconTile.Surface(Accent));
        ProgressRing.CenterIcon(drawList, tileMin + new Vector2(tileSize * 0.5f, tileSize * 0.5f),
            FontAwesomeIcon.Skull, AccentRing.Ink, tileSize * 0.5f);
        var titleY = tileMin.Y + tileSize + 16f * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, titleY + titleHeight * 0.5f),
            Loc.T(L.Games.DoomSetupTitle), theme.TextStrong, TextStyles.Title2);
        var bodyY = titleY + titleHeight + 8f * scale;
        var bodyHeight = Typography.DrawWrappedCentered(new Vector2(content.Center.X, bodyY),
            Loc.T(L.Games.DoomSetupBody), theme.TextMuted, TextStyles.Subheadline, content.Width);
        var cardTop = bodyY + bodyHeight + 18f * scale;
        var cardHeight = CardHeight * scale;
        DrawCard(new Rect(new Vector2(content.Min.X, cardTop), new Vector2(content.Max.X, cardTop + cardHeight)),
            Loc.T(L.Games.DoomGameData), Loc.T(L.Games.DoomGameDataDetail), assets.Wad.Snapshot(), theme, scale);
        var secondTop = cardTop + cardHeight + 8f * scale;
        DrawCard(new Rect(new Vector2(content.Min.X, secondTop), new Vector2(content.Max.X, secondTop + cardHeight)),
            Loc.T(L.Games.DoomMusic), Loc.T(L.Games.DoomMusicDetail), assets.Soundfont.Snapshot(), theme, scale);
        var busy = assets.Installing;
        var wadState = assets.Wad.Snapshot().State;
        var pending = assets.PendingBytes();
        var label = busy
            ? Loc.T(L.AetherStream.SetupInstalling)
            : wadState == DependencyState.Failed
                ? Loc.T(L.AetherStream.SetupRetry)
                : string.Format(Loc.T(L.AetherStream.SetupInstallSized), DependencySetup.FormatMegabytes(pending));
        var buttonCenter = new Vector2(content.Center.X, secondTop + cardHeight + 34f * scale);
        if (GameHud.Button(buttonCenter, new Vector2(content.Width * 0.7f, 44f * scale), label, Accent, theme) && !busy)
        {
            assets.Install();
        }
    }

    private void DrawCard(Rect card, string title, string detail, DependencyProgress snapshot, PhoneTheme theme,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = 14f * scale;
        Material.Frosted(drawList, card.Min, card.Max, radius, scale);
        var pad = 12f * scale;
        var left = card.Min.X + pad;
        var right = card.Max.X - pad;
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(left, card.Min.Y + pad * 0.7f),
            Typography.FitText(title, right - left, TextStyles.BodyEmphasized), theme.TextStrong, TextStyles.BodyEmphasized);
        var detailY = card.Min.Y + pad * 0.7f + titleHeight;
        Typography.Draw(drawList, new Vector2(left, detailY), Typography.FitText(detail, right - left, TextStyles.Caption1),
            theme.TextMuted, TextStyles.Caption1);
        var statusY = detailY + Typography.LineHeight(TextStyles.Caption1) + 2f * scale;
        var statusColor = snapshot.State == DependencyState.Failed
            ? theme.Danger
            : snapshot.State == DependencyState.Ready ? Accent : theme.TextMuted;
        Typography.Draw(drawList, new Vector2(left, statusY),
            Typography.FitText(DependencySetup.StatusText(snapshot), right - left, TextStyles.Caption1), statusColor,
            TextStyles.Caption1);
        if (snapshot.State != DependencyState.Downloading)
        {
            return;
        }

        var barTop = card.Max.Y - 5f * scale;
        var barMin = new Vector2(left, barTop);
        var barMax = new Vector2(right, barTop + 3f * scale);
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f)), 1.5f * scale);
        drawList.AddRectFilled(barMin, new Vector2(left + (right - left) * snapshot.Fraction, barMax.Y),
            ImGui.GetColorU32(Accent), 1.5f * scale);
    }

    private void DrawFailure(Rect body, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var center = body.Center;
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y - 40f * scale), Loc.T(L.Games.DoomFailed),
            theme.TextStrong, TextStyles.Title3);
        Typography.DrawWrappedCentered(new Vector2(center.X, center.Y - 14f * scale), failure ?? string.Empty,
            theme.TextMuted, TextStyles.Caption1, body.Width - 40f * scale);
        if (GameHud.Button(new Vector2(center.X, center.Y + 60f * scale), new Vector2(body.Width * 0.5f, 44f * scale),
                Loc.T(L.AetherStream.SetupRetry), Accent, theme))
        {
            failure = null;
        }
    }
}
