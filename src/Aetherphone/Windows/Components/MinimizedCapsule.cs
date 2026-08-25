using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum MinimizedAction : byte
{
    None,
    Expand,
    Close,
}

internal enum CapsuleActivity : byte
{
    None,
    Music,
    Call,
}

internal readonly struct CapsuleDrag
{
    public readonly Vector2 Delta;
    public readonly bool Released;

    public CapsuleDrag(Vector2 delta, bool released)
    {
        Delta = delta;
        Released = released;
    }
}

internal sealed class MinimizedCapsule : IDisposable
{
    private const float ScreenHeight = 40f;
    private const float EndPadding = 13f;
    private const float SegmentGap = 10f;
    private const float HoverGrow = 3f;
    private const float MoonHeight = 11f;
    private const float MoonGap = 6f;
    private const float MusicWidth = 130f;
    private const float CallWidth = 134f;
    private const float BadgeWidth = 28f;
    private const float MusicExpandedHeight = 38f;
    private const float CallExpandedHeight = 40f;
    private const float CardWidth = 250f;
    private const float CardScreenHeight = 52f;
    private const float HoldSeconds = 0.55f;
    private const float DragSlop = 5f;
    private const float CardHoldSeconds = 4.5f;
    private const float PulseSeconds = 0.8f;
    private const float TooltipClearance = 44f;
    private const int MaxQueuedCards = 3;
    private const float PresenceSmoothTime = 0.16f;
    private const float HoverSmoothTime = 0.12f;
    private const float ExpandSmoothTime = 0.17f;
    private const float CardSmoothTime = 0.20f;
    private const float HoldSmoothTime = 0.07f;
    private const float ControlThreshold = 0.6f;
    private const float ClockScale = 1.0f;
    private const FontWeight ClockWeight = FontWeight.SemiBold;
    private static readonly TimeSpan ShowingGrace = TimeSpan.FromSeconds(0.5);

    private const string MusicAppId = "music";
    private const string CallAppId = "message";

    private readonly PlaybackHub playback;
    private readonly CallHub calls;
    private readonly NotificationService notifications;
    private readonly NotificationRouter router;
    private readonly INavigator navigation;
    private readonly Configuration configuration;
    private readonly Queue<PhoneNotification> queuedCards = new();
    private bool activityHovered;
    private Spring hover;
    private Spring expand;
    private Spring activity;
    private Spring badge;
    private Spring dnd;
    private Spring card;
    private Spring hold;
    private CapsuleActivity shownActivity = CapsuleActivity.None;
    private PhoneNotification? cardNotification;
    private bool cardDismissed;
    private float cardElapsed;
    private float clock;
    private float pulseRemaining;
    private Vector4 pulseAccent;
    private bool pressed;
    private bool dragging;
    private bool holdFired;
    private float held;
    private Vector2 pressOrigin;
    private Vector2 dragDelta;
    private bool dragReleased;
    private string? badgeAppId;
    private Vector4 badgeAccent;
    private string countLabel = string.Empty;
    private int countValue = -1;
    private string durationLabel = string.Empty;
    private int durationSeconds = -1;
    private float clockWidth;
    private int clockWidthFrame = -1;
    private DateTime lastInteractiveDrawUtc = DateTime.MinValue;

    public MinimizedCapsule(PlaybackHub playback, CallHub calls, NotificationService notifications,
        NotificationRouter router, INavigator navigation, Configuration configuration)
    {
        this.playback = playback;
        this.calls = calls;
        this.notifications = notifications;
        this.router = router;
        this.navigation = navigation;
        this.configuration = configuration;
        notifications.Changed += RefreshBadge;
        notifications.Presented += OnPresented;
        notifications.Vibration += OnVibration;
        RefreshBadge();
    }

    public bool IsShowing => DateTime.UtcNow - lastInteractiveDrawUtc < ShowingGrace;

    public Vector2 Measure(float scale)
    {
        var band = ChassisGeometry.CapsuleBand(scale);
        var expandEased = Easing.SmoothStep(Math.Clamp(expand.Value, 0f, 1f));
        var cardEased = Easing.SmoothStep(Math.Clamp(card.Value, 0f, 1f));
        var hoverValue = Math.Clamp(hover.Value, 0f, 1f);
        var row = RowWidth(scale);
        var rowHeight = ScreenHeight * scale + ExpandedHeight(shownActivity) * scale * expandEased;
        var width = Easing.Lerp(row, CardWidth * scale, cardEased) + HoverGrow * scale * hoverValue;
        var height = Easing.Lerp(rowHeight, CardScreenHeight * scale, cardEased) + HoverGrow * scale * hoverValue;
        return new Vector2(MathF.Round(width + band), MathF.Round(height + band));
    }

    public Vector2 IdleSize(float scale)
    {
        var band = ChassisGeometry.CapsuleBand(scale);
        return new Vector2(MathF.Round(EndPadding * 2f * scale + ClockWidth() + band),
            MathF.Round(ScreenHeight * scale + band));
    }

    public CapsuleDrag ConsumeDrag()
    {
        var result = new CapsuleDrag(dragDelta, dragReleased);
        dragDelta = Vector2.Zero;
        dragReleased = false;
        return result;
    }

    public MinimizedAction Draw(Rect body, PhoneTheme theme, float delta)
    {
        var scale = UiScale.Global;
        var geometry = ChassisGeometry.Capsule(body, scale);
        var dl = ImGui.GetForegroundDrawList();
        var lift = Math.Clamp(hover.Value, 0f, 1f);
        Elevation.Squircle(dl, geometry.Body.Min, geometry.Body.Max, geometry.BodyRadius, scale, 0.85f + 0.35f * lift);
        DeviceChrome.DrawShell(dl, geometry, scale, theme.Case, theme.ScreenBase);
        return DrawFace(dl, geometry, theme, delta, true, 1f);
    }

    public MinimizedAction DrawFace(ImDrawListPtr dl, in ChassisGeometry geometry, PhoneTheme theme, float delta,
        bool interactive, float alpha)
    {
        clock += delta;
        if (interactive)
        {
            lastInteractiveDrawUtc = DateTime.UtcNow;
        }

        var scale = UiScale.Global;
        var body = geometry.Body;
        var bodyHovered = interactive && UiInteract.Hover(body.Min, body.Max);
        var view = calls.Snapshot();
        StepState(delta, interactive, bodyHovered, view);
        if (alpha <= 0.001f)
        {
            return MinimizedAction.None;
        }

        var screen = geometry.Screen;
        var hoverValue = Math.Clamp(hover.Value, 0f, 1f);
        var expandEased = Easing.SmoothStep(Math.Clamp(expand.Value, 0f, 1f));
        var cardEased = Easing.SmoothStep(Math.Clamp(card.Value, 0f, 1f));
        var rowAlpha = alpha * (1f - cardEased);
        var rowCenterY = screen.Min.Y + (ScreenHeight + HoverGrow * hoverValue) * scale * 0.5f;
        var hoveredControl = false;
        dl.PushClipRect(screen.Min, screen.Max, true);
        if (rowAlpha > 0.01f)
        {
            DrawRow(dl, screen, theme, view, scale, rowCenterY, rowAlpha, hoverValue, bodyHovered);
            if (expandEased > 0.02f)
            {
                hoveredControl = DrawExpandedRow(dl, screen, theme, view, scale, rowCenterY, hoverValue, rowAlpha,
                    expandEased, interactive);
            }
        }

        if (cardEased > 0.01f && cardNotification is { } notification)
        {
            CapsuleRenderer.DrawCard(dl, geometry, notification, theme, alpha * cardEased, scale, bodyHovered);
        }

        var holdValue = Math.Clamp(hold.Value, 0f, 1f);
        if (holdValue > 0.005f)
        {
            CapsuleRenderer.DrawHoldSweep(dl, geometry, theme, holdValue * alpha, scale);
        }

        dl.PopClipRect();
        if (pulseRemaining > 0f)
        {
            var strength = pulseRemaining / PulseSeconds;
            CapsuleRenderer.DrawPulse(dl, geometry, pulseAccent, strength * strength * alpha, scale);
        }

        if (!interactive)
        {
            return MinimizedAction.None;
        }

        return HandleGesture(body, scale, delta, bodyHovered, hoveredControl, cardEased, expandEased);
    }

    private void StepState(float delta, bool interactive, bool bodyHovered, in CallView view)
    {
        if (pulseRemaining > 0f)
        {
            pulseRemaining = MathF.Max(0f, pulseRemaining - delta);
        }

        var callActive = view.State is CallState.Dialing or CallState.Connecting or CallState.Active;
        var current = callActive ? CapsuleActivity.Call : playback.IsActive ? CapsuleActivity.Music : CapsuleActivity.None;
        if (current != CapsuleActivity.None)
        {
            shownActivity = current;
        }

        activity.Step(current == CapsuleActivity.None ? 0f : 1f, PresenceSmoothTime, delta);
        badge.Step(badgeAppId is null ? 0f : 1f, PresenceSmoothTime, delta);
        dnd.Step(configuration.DoNotDisturb ? 1f : 0f, PresenceSmoothTime, delta);
        AdvanceCard(delta, bodyHovered);
        var cardShowing = cardNotification is not null && !cardDismissed;
        hover.Step(interactive && bodyHovered ? 1f : 0f, HoverSmoothTime, delta);
        var wantsExpand = interactive && bodyHovered && current != CapsuleActivity.None && !cardShowing && !dragging;
        expand.Step(wantsExpand ? 1f : 0f, ExpandSmoothTime, delta);
        var holdTarget = pressed && !dragging ? Math.Clamp(held / HoldSeconds, 0f, 1f) : 0f;
        hold.Step(holdTarget, HoldSmoothTime, delta);
        if (activity.Value < 0.01f && current == CapsuleActivity.None)
        {
            shownActivity = CapsuleActivity.None;
        }
    }

    private void AdvanceCard(float delta, bool bodyHovered)
    {
        if (cardNotification is null)
        {
            card.SnapTo(0f);
            if (queuedCards.Count > 0)
            {
                BeginCard(queuedCards.Dequeue());
            }

            return;
        }

        if (!cardDismissed)
        {
            card.Step(1f, CardSmoothTime, delta);
            if (!bodyHovered)
            {
                cardElapsed += delta;
            }

            if (cardElapsed >= CardHoldSeconds)
            {
                cardDismissed = true;
            }

            return;
        }

        card.Step(0f, CardSmoothTime, delta);
        if (card.Value > 0.02f)
        {
            return;
        }

        card.SnapTo(0f);
        cardNotification = null;
        cardDismissed = false;
    }

    private void DrawRow(ImDrawListPtr dl, Rect screen, PhoneTheme theme, in CallView view, float scale,
        float rowCenterY, float alpha, float hoverValue, bool bodyHovered)
    {
        var x = screen.Min.X + (EndPadding + HoverGrow * hoverValue * 0.5f) * scale;
        var time = StatusBar.CurrentTime();
        var timeSize = Typography.Measure(time, TextScale(ClockScale), ClockWeight);
        Typography.Draw(dl, new Vector2(x, rowCenterY - timeSize.Y * 0.5f), time,
            Palette.WithAlpha(theme.TextStrong, alpha), TextScale(ClockScale), ClockWeight);
        x += timeSize.X;
        var dndValue = Math.Clamp(dnd.Value, 0f, 1f);
        if (dndValue > 0.01f)
        {
            var moonWidth = (MoonGap + MoonHeight) * scale * dndValue;
            CapsuleRenderer.DrawMoon(dl, new Vector2(x + moonWidth - MoonHeight * scale * 0.5f, rowCenterY),
                MoonHeight * scale, alpha * dndValue);
            x += moonWidth;
        }

        var activityValue = Math.Clamp(activity.Value, 0f, 1f);
        activityHovered = false;
        if (activityValue > 0.01f && shownActivity != CapsuleActivity.None)
        {
            var width = ActivityWidth(shownActivity) * scale;
            var slotLeft = x + SegmentGap * scale * activityValue;
            var slot = new Rect(new Vector2(slotLeft, rowCenterY - ScreenHeight * scale * 0.5f),
                new Vector2(slotLeft + width, rowCenterY + ScreenHeight * scale * 0.5f));
            activityHovered = bodyHovered && activityValue > 0.9f && UiInteract.Hover(slot.Min, slot.Max);
            dl.PushClipRect(slot.Min, new Vector2(slotLeft + width * activityValue, slot.Max.Y), true);
            if (shownActivity == CapsuleActivity.Music)
            {
                CapsuleRenderer.DrawMusicCompact(dl, slot, playback, clock, alpha * activityValue, bodyHovered, scale,
                    theme);
            }
            else
            {
                CapsuleRenderer.DrawCallCompact(dl, slot, view, DurationLabel(view), clock, alpha * activityValue,
                    scale, theme);
            }

            dl.PopClipRect();
            x += (SegmentGap + ActivityWidth(shownActivity)) * scale * activityValue;
        }

        var badgeValue = Math.Clamp(badge.Value, 0f, 1f);
        if (badgeValue > 0.01f && badgeAppId is { } appId)
        {
            var width = BadgeWidth * scale;
            var slotLeft = x + SegmentGap * scale * badgeValue;
            var slot = new Rect(new Vector2(slotLeft, rowCenterY - ScreenHeight * scale * 0.5f),
                new Vector2(slotLeft + width, rowCenterY + ScreenHeight * scale * 0.5f));
            dl.PushClipRect(slot.Min, new Vector2(slotLeft + width * badgeValue, slot.Max.Y), true);
            CapsuleRenderer.DrawBadge(dl, slot, appId, badgeAccent, countLabel, theme, alpha * badgeValue, scale);
            dl.PopClipRect();
        }
    }

    private bool DrawExpandedRow(ImDrawListPtr dl, Rect screen, PhoneTheme theme, in CallView view, float scale,
        float rowCenterY, float hoverValue, float alpha, float expandEased, bool interactive)
    {
        var rowTop = rowCenterY + ScreenHeight * scale * 0.5f;
        var rowHeight = ExpandedHeight(shownActivity) * scale * expandEased;
        var row = new Rect(new Vector2(screen.Min.X + EndPadding * scale, rowTop),
            new Vector2(screen.Max.X - EndPadding * scale, rowTop + rowHeight));
        var rowAlpha = alpha * expandEased;
        var active = interactive && expandEased > ControlThreshold;
        if (shownActivity == CapsuleActivity.Music)
        {
            var result = CapsuleRenderer.DrawMusicExpanded(dl, row, playback, theme, rowAlpha, active, scale);
            ApplyMusicControl(result.Action);
            return result.Hovered;
        }

        var callResult = CapsuleRenderer.DrawCallExpanded(dl, row, view, theme, rowAlpha, active, scale);
        if (callResult.Action == CapsuleControl.ToggleMute)
        {
            calls.ToggleMute();
        }
        else if (callResult.Action == CapsuleControl.Hangup)
        {
            calls.Hangup();
        }

        return callResult.Hovered;
    }

    private void ApplyMusicControl(CapsuleControl control)
    {
        switch (control)
        {
            case CapsuleControl.Previous:
                playback.Previous();
                break;
            case CapsuleControl.Next:
                playback.Next();
                break;
            case CapsuleControl.PlayPause:
                playback.TogglePlayPause();
                break;
        }
    }

    private MinimizedAction HandleGesture(Rect body, float scale, float delta, bool bodyHovered, bool hoveredControl,
        float cardEased, float expandEased)
    {
        if (bodyHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!pressed && bodyHovered && !hoveredControl && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pressed = true;
            dragging = false;
            holdFired = false;
            held = 0f;
            pressOrigin = ImGui.GetMousePos();
        }

        var action = MinimizedAction.None;
        if (pressed)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var mouse = ImGui.GetMousePos();
                if (!dragging && (mouse - pressOrigin).Length() > DragSlop * scale)
                {
                    dragging = true;
                }

                if (dragging)
                {
                    dragDelta += ImGui.GetIO().MouseDelta;
                }
                else
                {
                    held += delta;
                    if (held >= HoldSeconds && !holdFired)
                    {
                        holdFired = true;
                        action = MinimizedAction.Close;
                    }
                }
            }
            else
            {
                if (dragging)
                {
                    dragReleased = true;
                }
                else if (!holdFired && bodyHovered)
                {
                    action = Tap(cardEased);
                }

                pressed = false;
                dragging = false;
                held = 0f;
            }
        }

        if (!pressed && bodyHovered && cardEased < 0.5f && expandEased < 0.5f && activity.Value < 0.5f)
        {
            var viewport = ImGui.GetMainViewport();
            var side = body.Max.Y + TooltipClearance * scale > viewport.Pos.Y + viewport.Size.Y
                ? HoverLabelSide.Above
                : HoverLabelSide.Below;
            HoverTooltip.Show("minimized.capsule", body, Loc.T(L.Plugin.CapsuleHint), side);
        }

        return action;
    }

    private MinimizedAction Tap(float cardEased)
    {
        if (cardEased > 0.5f && cardNotification is { } notification && !cardDismissed)
        {
            router.Open(notification);
            cardDismissed = true;
            queuedCards.Clear();
            return MinimizedAction.Expand;
        }

        if (activityHovered && shownActivity == CapsuleActivity.Music)
        {
            navigation.Open(MusicAppId);
        }
        else if (activityHovered && shownActivity == CapsuleActivity.Call)
        {
            calls.RequestCallScreen();
            navigation.Open(CallAppId);
        }

        return MinimizedAction.Expand;
    }

    private float RowWidth(float scale)
    {
        var width = EndPadding * 2f * scale + ClockWidth();
        width += (MoonGap + MoonHeight) * scale * Math.Clamp(dnd.Value, 0f, 1f);
        if (shownActivity != CapsuleActivity.None)
        {
            width += (SegmentGap + ActivityWidth(shownActivity)) * scale * Math.Clamp(activity.Value, 0f, 1f);
        }

        width += (SegmentGap + BadgeWidth) * scale * Math.Clamp(badge.Value, 0f, 1f);
        return width;
    }

    private float ClockWidth()
    {
        var frame = ImGui.GetFrameCount();
        if (frame != clockWidthFrame)
        {
            clockWidthFrame = frame;
            clockWidth = Typography.Measure(StatusBar.CurrentTime(), TextScale(ClockScale), ClockWeight).X;
        }

        return clockWidth;
    }

    private string DurationLabel(in CallView view)
    {
        if (view.State != CallState.Active)
        {
            durationSeconds = -1;
            return CallStatusText.Label(view);
        }

        if (view.Seconds != durationSeconds || !view.Connected)
        {
            durationSeconds = view.Seconds;
            durationLabel = CallStatusText.Label(view);
        }

        return durationLabel;
    }

    private static float ActivityWidth(CapsuleActivity kind) => kind == CapsuleActivity.Call ? CallWidth : MusicWidth;

    private static float ExpandedHeight(CapsuleActivity kind) =>
        kind == CapsuleActivity.Call ? CallExpandedHeight : MusicExpandedHeight;

    private static float TextScale(float scale) => scale / UiScale.Phone;

    private void RefreshBadge()
    {
        var unread = notifications.UnreadCount;
        if (unread != countValue)
        {
            countValue = unread;
            countLabel = unread > 99 ? "99+" : unread.ToString(Loc.Culture);
        }

        var recent = notifications.Recent;
        for (var index = recent.Count - 1; index >= 0; index--)
        {
            var notification = recent[index];
            if (notification.Read)
            {
                continue;
            }

            badgeAppId = notification.AppId;
            badgeAccent = notification.Accent;
            return;
        }

        badgeAppId = null;
    }

    private void OnPresented(PhoneNotification notification)
    {
        if (!IsShowing)
        {
            return;
        }

        if (cardNotification is { } showing && !cardDismissed && showing.StackKey == notification.StackKey)
        {
            cardNotification = notification;
            cardElapsed = 0f;
            return;
        }

        RemoveQueuedGroup(notification.StackKey);
        if (queuedCards.Count >= MaxQueuedCards)
        {
            return;
        }

        if (cardNotification is null)
        {
            BeginCard(notification);
            return;
        }

        queuedCards.Enqueue(notification);
    }

    private void RemoveQueuedGroup(string stackKey)
    {
        var count = queuedCards.Count;
        for (var index = 0; index < count; index++)
        {
            var queued = queuedCards.Dequeue();
            if (queued.StackKey != stackKey)
            {
                queuedCards.Enqueue(queued);
            }
        }
    }

    private void BeginCard(PhoneNotification notification)
    {
        cardNotification = notification;
        cardDismissed = false;
        cardElapsed = 0f;
        card.SnapTo(0f);
    }

    private void OnVibration(PhoneNotification notification)
    {
        if (!IsShowing)
        {
            return;
        }

        pulseRemaining = PulseSeconds;
        pulseAccent = notification.Accent;
    }

    public void Dispose()
    {
        notifications.Changed -= RefreshBadge;
        notifications.Presented -= OnPresented;
        notifications.Vibration -= OnVibration;
    }
}
