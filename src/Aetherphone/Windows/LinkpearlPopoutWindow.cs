using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

internal sealed class LinkpearlPopoutWindow : Window
{
    public const float DefaultWidth = 336f;
    public const float DefaultHeight = 430f;

    private const ImGuiWindowFlags PopoutFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar |
                                                 ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse |
                                                 ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings |
                                                 ImGuiWindowFlags.NoFocusOnAppearing;

    private const int ScaledStyleVarCount = 6;
    private const int GripColorCount = 3;
    private const float MinWidth = 250f;
    private const float MinHeight = 210f;
    private const float MaxSide = 2400f;
    private const float TitleHeight = 44f;
    private const float Rounding = 18f;
    private const float BodyInset = 4f;
    private const float AvatarRadius = 13f;
    private const float ButtonRadius = 14f;
    private const float ButtonPitch = 31f;
    private const float EdgeInset = 14f;
    private const float CaretGap = 6f;
    private const float StaggerStep = 28f;
    private const float ViewportMargin = 24f;
    private const float GripArm = 9f;
    private const int SwitchMenuLimit = 14;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 GripInk = new(1f, 1f, 1f, 0.22f);

    private readonly LinkpearlPopouts owner;
    private readonly int slot;
    private readonly Configuration configuration;
    private readonly ChatInbox inbox;
    private readonly TabStore tabs;
    private readonly ThemeProvider themes;
    private readonly LodestoneService lodestone;
    private readonly NotificationService notifications;
    private readonly GameChatThread thread;
    private readonly GameChatMenu chatMenu;
    private readonly DropdownMenu switchMenu = new();
    private readonly List<DropdownMenu.Item> switchItems = new(SwitchMenuLimit);
    private readonly List<string> switchKeys = new(SwitchMenuLimit);
    private readonly string switchMenuId;
    private readonly string closeButtonId;
    private readonly string phoneButtonId;
    private readonly string bellButtonId;
    private string key = string.Empty;
    private string threadKey = string.Empty;
    private bool attended;
    private bool placePending;
    private LinkpearlPopoutState? savedPlacement;
    private Vector2 pendingPosition;
    private Vector2 pendingSize;
    private Rect frame;

    public LinkpearlPopoutWindow(LinkpearlPopouts owner, int slot, Configuration configuration, ChatInbox inbox,
        TabStore tabs, ChatLog log, ChatSend send, GameData gameData, ThemeProvider themes,
        LodestoneService lodestone, NotificationService notifications)
        : base($"{AepConstants.Name}##LinkpearlPopout{slot}", PopoutFlags)
    {
        this.owner = owner;
        this.slot = slot;
        this.configuration = configuration;
        this.inbox = inbox;
        this.tabs = tabs;
        this.themes = themes;
        this.lodestone = lodestone;
        this.notifications = notifications;
        var slotText = slot.ToString(Loc.Culture);
        switchMenuId = "linkpearl.popout.switch." + slotText;
        closeButtonId = "linkpearl.popout.close." + slotText;
        phoneButtonId = "linkpearl.popout.phone." + slotText;
        bellButtonId = "linkpearl.popout.bell." + slotText;
        chatMenu = new GameChatMenu("linkpearl.popout.menu." + slotText)
        {
            SendTell = owner.OpenTell,
            LookUp = owner.LookUpInPhone,
            OpenMarket = owner.OpenMarketInPhone,
        };
        thread = new GameChatThread(log, send, gameData)
        {
            Context = chatMenu.Open,
            Link = chatMenu.OpenLink,
        };
        RespectCloseHotkey = false;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(MinWidth, MinHeight),
            MaximumSize = new Vector2(MaxSide, MaxSide),
        };
    }

    public string Key => key;

    public bool Bound => key.Length > 0;

    public void Bind(string conversationKey, LinkpearlPopoutState? saved)
    {
        key = conversationKey;
        threadKey = string.Empty;
        attended = false;
        savedPlacement = saved;
        placePending = true;
        IsOpen = true;
        BringToFront();
    }

    private void ResolvePlacement()
    {
        var zoom = OwnZoom();
        var saved = savedPlacement;
        savedPlacement = null;
        pendingSize = saved is { Width: > 0f, Height: > 0f }
            ? new Vector2(saved.Width, saved.Height)
            : new Vector2(DefaultWidth * zoom, DefaultHeight * zoom);
        pendingPosition = saved is not null
            ? new Vector2(saved.X, saved.Y)
            : DefaultPosition(pendingSize * UiScale.Global);
    }

    public void Rebind(string conversationKey)
    {
        if (string.Equals(key, conversationKey, StringComparison.Ordinal))
        {
            return;
        }

        inbox.SetAttended(key, false);
        key = conversationKey;
        threadKey = string.Empty;
        attended = false;
        thread.Close();
        chatMenu.Close();
    }

    public void Unbind()
    {
        if (!Bound)
        {
            return;
        }

        inbox.SetAttended(key, false);
        key = string.Empty;
        threadKey = string.Empty;
        attended = false;
        thread.Close();
        chatMenu.Close();
        switchMenu.Close();
        IsOpen = false;
    }

    public void Focus() => BringToFront();

    public LinkpearlPopoutState Snapshot() => new()
    {
        Key = key,
        X = frame.Min.X,
        Y = frame.Min.Y,
        Width = frame.Width / UiScale.Global,
        Height = frame.Height / UiScale.Global,
    };

    public override void OnClose() => owner.OnWindowClosed(this);

    public override void PreDraw()
    {
        var zoom = OwnZoom();
        UiScale.SetPhone(zoom);
        Plugin.Fonts.SetPhoneZoom(zoom);
        DragScrollHost.Enabled = false;
        if (placePending)
        {
            ResolvePlacement();
            Position = pendingPosition;
            PositionCondition = ImGuiCond.Always;
            Size = pendingSize;
            SizeCondition = ImGuiCond.Always;
            placePending = false;
        }
        else
        {
            Position = null;
            Size = null;
        }

        var style = ImGui.GetStyle();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, style.FramePadding * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, style.ItemSpacing * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, style.ItemInnerSpacing * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, style.ScrollbarSize * zoom);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabMinSize, style.GrabMinSize * zoom);
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, AppSkin.Transparent);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, AppSkin.Transparent);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, AppSkin.Transparent);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor(GripColorCount);
        ImGui.PopStyleVar(ScaledStyleVarCount);
    }

    public override void Draw()
    {
        if (!Bound)
        {
            IsOpen = false;
            return;
        }

        var position = ImGui.GetWindowPos();
        frame = new Rect(position, position + ImGui.GetWindowSize());
        var hoveredWindow = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows |
                                                  ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        var focusedWindow = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        UiInteract.SetWindowHovered(hoveredWindow);
        UiInteract.SetWindowFocused(focusedWindow);
        inbox.Sync();
        var row = inbox.Find(key);
        UpdateAttention(row, hoveredWindow || focusedWindow);
        chatMenu.Gate();
        switchMenu.Gate();
        thread.Gate();
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        using (Plugin.Fonts.Push(1f))
        {
            var theme = themes.ForApp(true);
            var scale = UiScale.Current;
            DrawSurface(theme, scale, hoveredWindow || focusedWindow);
            var titleBar = new Rect(frame.Min, new Vector2(frame.Max.X, frame.Min.Y + TitleHeight * scale));
            DrawTitleBar(titleBar, row, theme, scale, delta);
            var inset = BodyInset * scale;
            var body = new Rect(new Vector2(frame.Min.X + inset, titleBar.Max.Y),
                new Vector2(frame.Max.X - inset, frame.Max.Y - inset));
            if (row is null)
            {
                Typography.DrawCentered(ImGui.GetWindowDrawList(), body.Center, Loc.T(L.Messages.Empty),
                    theme.TextMuted, TextStyles.Callout);
            }
            else
            {
                OpenThread(row);
                thread.Draw(body, theme);
            }

            DrawGrip(scale);
            DrawSwitchMenu(theme);
            chatMenu.Draw(frame, theme);
        }

        HoverTooltip.Flush();
        CopyToast.Flush();
    }

    private float OwnZoom()
    {
        var phoneZoom = PhoneSizeCatalog.ZoomFor(PhoneBounds.ClampWidth(configuration.PhoneWidth));
        return phoneZoom * Math.Clamp(configuration.LinkpearlPopoutTextScale, 0.6f, 1.8f);
    }

    private Vector2 DefaultPosition(Vector2 scaledSize)
    {
        var viewport = ImGui.GetMainViewport();
        var margin = ViewportMargin * UiScale.Global;
        var stagger = StaggerStep * UiScale.Global * slot;
        var target = viewport.Pos + viewport.Size - scaledSize - new Vector2(margin + stagger, margin + stagger);
        target.X = MathF.Max(viewport.Pos.X, target.X);
        target.Y = MathF.Max(viewport.Pos.Y, target.Y);
        return target;
    }

    private void UpdateAttention(InboxRow? row, bool attending)
    {
        if (attending == attended)
        {
            if (attending && row is { Unread: > 0 })
            {
                inbox.MarkRead(row);
            }

            return;
        }

        attended = attending;
        inbox.SetAttended(key, attending);
        if (!attending)
        {
            inbox.FlushSeen();
            return;
        }

        if (row is not null)
        {
            inbox.MarkRead(row);
        }

        notifications.RemoveGroup(key);
    }

    private void OpenThread(InboxRow row)
    {
        if (string.Equals(threadKey, row.Key, StringComparison.Ordinal))
        {
            return;
        }

        threadKey = row.Key;
        thread.Open(GameChatTargets.For(row));
    }

    private void DrawSurface(PhoneTheme theme, float scale, bool lively)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Rounding * scale;
        var opacity = Math.Clamp(configuration.LinkpearlPopoutOpacity, 0.35f, 1f);
        Elevation.Floating(drawList, frame.Min, frame.Max, rounding, scale, lively ? 1f : 0.7f);
        var surface = ImGui.GetColorU32(Palette.WithAlpha(theme.AppBackground, opacity));
        Squircle.FillVerticalGradient(drawList, frame.Min, frame.Max, rounding, surface, surface);
        var titleBottom = frame.Min.Y + TitleHeight * scale;
        var strip = ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, opacity));
        drawList.PushClipRect(frame.Min, new Vector2(frame.Max.X, titleBottom), true);
        Squircle.FillVerticalGradient(drawList, frame.Min, frame.Max, rounding, strip, strip);
        drawList.PopClipRect();
        drawList.AddLine(new Vector2(frame.Min.X, titleBottom), new Vector2(frame.Max.X, titleBottom),
            ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, theme.Separator.W * opacity)), Metrics.Stroke.Hairline);
        Material.EdgeSquircle(drawList, frame.Min, frame.Max, rounding, scale, lively ? 1f : 0.6f);
    }

    private void DrawTitleBar(Rect bar, InboxRow? row, PhoneTheme theme, float scale, float delta)
    {
        var drawList = ImGui.GetWindowDrawList();
        var centerY = bar.Center.Y;
        var radius = ButtonRadius * scale;
        var closeCenter = new Vector2(bar.Max.X - EdgeInset * scale - radius * 0.5f, centerY);
        var phoneCenter = new Vector2(closeCenter.X - ButtonPitch * scale, centerY);
        var bellCenter = new Vector2(phoneCenter.X - ButtonPitch * scale, centerY);
        var muted = row?.Muted ?? false;
        if (HoverButton.Circle(drawList, closeButtonId, closeCenter, radius, FontAwesomeIcon.Times,
                AppSkin.Transparent, theme.TextMuted, delta, 1f, true, Loc.T(L.Common.Close)))
        {
            owner.Close(key);
            return;
        }

        if (HoverButton.Circle(drawList, phoneButtonId, phoneCenter, radius, FontAwesomeIcon.MobileAlt,
                AppSkin.Transparent, theme.TextMuted, delta, 1f, true, Loc.T(L.Linkpearl.OpenInPhone)))
        {
            owner.OpenInPhone?.Invoke(key);
        }

        if (row is not null && HoverButton.Circle(drawList, bellButtonId, bellCenter, radius,
                muted ? FontAwesomeIcon.BellSlash : FontAwesomeIcon.Bell, AppSkin.Transparent,
                muted ? theme.Accent : theme.TextMuted, delta, 1f, true,
                Loc.T(muted ? L.Linkpearl.Unmute : L.Linkpearl.Mute)))
        {
            inbox.ToggleMuted(row);
        }

        var avatarRadius = AvatarRadius * scale;
        var avatarCenter = new Vector2(bar.Min.X + EdgeInset * scale + avatarRadius, centerY);
        DrawAvatar(drawList, avatarCenter, avatarRadius, row, theme);
        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Sm * scale;
        var textLimit = bellCenter.X - radius - Metrics.Space.Sm * scale;
        var title = row?.Title ?? FallbackTitle();
        var unread = row is { HasBadge: true } && !attended ? row.Unread : 0;
        var badgeWidth = 0f;
        if (unread > 0)
        {
            badgeWidth = BadgeWidth(unread, scale) + Metrics.Space.Xs * scale;
        }

        var caretWidth = 10f * scale;
        var titleStyle = TextStyles.Headline;
        var titleSize = Typography.Measure(title, titleStyle);
        var titleWidth = MathF.Min(titleSize.X, MathF.Max(1f, textLimit - textLeft - caretWidth - badgeWidth));
        var titleTop = centerY - titleSize.Y * 0.5f;
        var hitMin = new Vector2(textLeft - 4f * scale, bar.Min.Y + 6f * scale);
        var hitMax = new Vector2(textLeft + titleWidth + caretWidth + 4f * scale, bar.Max.Y - 6f * scale);
        var titleHovered = UiInteract.Hover(hitMin, hitMax);
        if (titleHovered)
        {
            Squircle.Fill(drawList, hitMin, hitMax, Metrics.Radius.Sm * scale,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.06f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Marquee.DrawLeft(drawList, switchMenuId + ".title", title, textLeft, titleTop, titleWidth, titleStyle,
            theme.TextStrong, titleHovered);
        AppSkin.Icon(drawList, new Vector2(textLeft + titleWidth + CaretGap * scale, centerY + 1f * scale),
            FontAwesomeIcon.ChevronDown.ToIconString(), theme.TextMuted, 0.55f);
        if (unread > 0)
        {
            DrawBadge(drawList, unread, textLeft + titleWidth + caretWidth + Metrics.Space.Xs * scale, centerY,
                theme, scale);
        }

        HoverTooltip.Show(new Rect(hitMin, hitMax), Loc.T(L.Linkpearl.SwitchConversation), HoverLabelSide.Below);
        if (UiInteract.Click(hitMin, hitMax, titleHovered))
        {
            OpenSwitchMenu(new Rect(hitMin, hitMax));
        }
    }

    private void DrawAvatar(ImDrawListPtr drawList, Vector2 center, float radius, InboxRow? row, PhoneTheme theme)
    {
        if (row is null)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(theme.SurfaceMuted), 24);
            return;
        }

        if (row.IsTell)
        {
            AvatarView.Draw(drawList, center, radius, theme.Accent, Initials.Of(row.Title), 0.7f,
                lodestone.Avatar(row.Title, row.World, radius * 2f), 24);
            return;
        }

        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        Squircle.Fill(drawList, min, max, radius * 0.62f, ImGui.GetColorU32(Palette.WithAlpha(row.Tint, 0.24f)));
        Typography.DrawCentered(drawList, center, Initials.Of(row.Title), row.Tint, TextStyles.Caption2);
    }

    private string FallbackTitle()
    {
        if (key.StartsWith("tab:", StringComparison.Ordinal))
        {
            return tabs.Find(key["tab:".Length..])?.Name ?? Loc.T(L.Apps.Linkpearl);
        }

        var target = ChatStreams.TellTarget(key);
        var at = target.IndexOf('@');
        var name = at >= 0 ? target[..at] : target;
        return name.Length > 0 ? Loc.Culture.TextInfo.ToTitleCase(name) : Loc.T(L.Apps.Linkpearl);
    }

    private static float BadgeWidth(int unread, float scale)
    {
        var label = unread > 99 ? "99+" : unread.ToString(Loc.Culture);
        var height = 16f * scale;
        return MathF.Max(Typography.Measure(label, TextStyles.Caption2).X + 10f * scale, height);
    }

    private static void DrawBadge(ImDrawListPtr drawList, int unread, float left, float centerY, PhoneTheme theme,
        float scale)
    {
        var label = unread > 99 ? "99+" : unread.ToString(Loc.Culture);
        var height = 16f * scale;
        var width = BadgeWidth(unread, scale);
        var min = new Vector2(left, centerY - height * 0.5f);
        var max = new Vector2(left + width, centerY + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(theme.Accent));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, White, TextStyles.Caption2);
    }

    private void DrawGrip(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var corner = frame.Max - new Vector2(7f * scale, 7f * scale);
        var arm = GripArm * scale;
        var color = ImGui.GetColorU32(GripInk);
        drawList.AddLine(new Vector2(corner.X - arm, corner.Y), new Vector2(corner.X, corner.Y - arm), color,
            1.4f * scale);
        drawList.AddLine(new Vector2(corner.X - arm * 0.45f, corner.Y), new Vector2(corner.X, corner.Y - arm * 0.45f),
            color, 1.4f * scale);
    }

    private void OpenSwitchMenu(Rect anchor)
    {
        switchItems.Clear();
        switchKeys.Clear();
        AddSwitchRows(inbox.Pinned);
        AddSwitchRows(inbox.Rows);
        if (switchItems.Count == 0)
        {
            return;
        }

        switchMenu.Header = Loc.T(L.Linkpearl.SwitchConversation);
        switchMenu.Toggle(switchMenuId, anchor);
    }

    private void AddSwitchRows(IReadOnlyList<InboxRow> rows)
    {
        for (var index = 0; index < rows.Count && switchItems.Count < SwitchMenuLimit; index++)
        {
            var row = rows[index];
            var label = row.HasBadge
                ? string.Concat(row.Title, " · ", row.Unread.ToString(Loc.Culture))
                : row.Title;
            var glyph = row.IsTell ? FontAwesomeIcon.User.ToIconString() : FontAwesomeIcon.Hashtag.ToIconString();
            switchItems.Add(new DropdownMenu.Item(label, glyph, false,
                string.Equals(row.Key, key, StringComparison.Ordinal)));
            switchKeys.Add(row.Key);
        }
    }

    private void DrawSwitchMenu(PhoneTheme theme)
    {
        if (!switchMenu.IsOpenFor(switchMenuId))
        {
            return;
        }

        var picked = switchMenu.Draw(frame, theme, CollectionsMarshal.AsSpan(switchItems));
        if (picked < 0)
        {
            return;
        }

        owner.Switch(this, switchKeys[picked]);
    }
}
