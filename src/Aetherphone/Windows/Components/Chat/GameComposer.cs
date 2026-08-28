using System.Runtime.InteropServices;
using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal readonly ref struct GameComposerModel
{
    public required PhoneTheme Theme { get; init; }
    public required Rect Screen { get; init; }
    public required ReadOnlySpan<string> Channels { get; init; }
    public required string ActiveChannel { get; init; }
    public required string SendTarget { get; init; }
}

internal readonly struct GameComposerResult
{
    public readonly bool Submitted;
    public readonly string Text;
    public readonly string ChannelKey;

    public GameComposerResult(bool submitted, string text, string channelKey)
    {
        Submitted = submitted;
        Text = text;
        ChannelKey = channelKey;
    }
}

internal sealed class GameComposer
{
    private const float ChipMaxWidth = 58f;
    private const float RingThreshold = 0.55f;

    private readonly DropdownMenu channelMenu = new();
    private readonly List<DropdownMenu.Item> menuItems = new(24);
    private readonly List<string> menuKeys = new(24);
    private string draft = string.Empty;
    private bool focus;

    public string Draft => draft;

    public void Gate() => channelMenu.Gate();

    public void Reset()
    {
        Clear();
        focus = false;
        channelMenu.Close();
    }

    public void Refill(string text)
    {
        draft = text;
        focus = true;
    }

    public GameComposerResult Draw(Rect bar, in GameComposerModel model)
    {
        var scale = UiScale.Current;
        var theme = model.Theme;
        var drawList = ImGui.GetWindowDrawList();
        var channelKey = model.ActiveChannel;
        var hasChannel = GameChannels.TryByKey(channelKey, out var channel);
        var readOnly = !hasChannel || !channel.CanSend ||
                       (channel.NeedsTarget && model.SendTarget.Length == 0);
        if (readOnly)
        {
            DrawReadOnly(bar, theme);
            return new GameComposerResult(false, string.Empty, channelKey);
        }

        var budget = ChatSend.Budget(channel, model.SendTarget);
        var pillMin = new Vector2(bar.Min.X + Metrics.Space.Md * scale, bar.Min.Y + 7f * scale);
        var pillMax = new Vector2(bar.Max.X - Metrics.Space.Md * scale, bar.Max.Y - 7f * scale);
        var height = pillMax.Y - pillMin.Y;
        var chipWidth = MathF.Min(ChipMaxWidth * scale, DrawChipWidth(channel, scale));
        var chipMin = new Vector2(pillMin.X, pillMin.Y + 3f * scale);
        var chipMax = new Vector2(chipMin.X + chipWidth, pillMax.Y - 3f * scale);
        var sendDiameter = height - 6f * scale;
        var sendCenter = new Vector2(pillMax.X - sendDiameter * 0.5f, (pillMin.Y + pillMax.Y) * 0.5f);
        var fieldMin = new Vector2(chipMax.X + Metrics.Space.Xs * scale, pillMin.Y);
        var fieldMax = new Vector2(sendCenter.X - sendDiameter * 0.5f - Metrics.Space.Xs * scale, pillMax.Y);
        Squircle.Fill(drawList, fieldMin, fieldMax, (fieldMax.Y - fieldMin.Y) * 0.5f,
            ImGui.GetColorU32(theme.GroupedCard));
        DrawChip(drawList, chipMin, chipMax, channel, scale);
        if (UiInteract.HoverClick(chipMin, chipMax))
        {
            channelMenu.Toggle("linkpearl.composer.channel", new Rect(chipMin, chipMax));
        }

        var submitted = false;
        var fieldWidth = fieldMax.X - fieldMin.X - Metrics.Space.Md * scale;
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + Metrics.Space.Sm * scale,
            (fieldMin.Y + fieldMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldWidth);
        if (focus)
        {
            ImGui.SetKeyboardFocusHere();
            focus = false;
        }

        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##linkpearl.composer", Loc.T(L.Messages.Placeholder), ref draft, budget,
                    ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        if (ChatCommands.TryAbsorb(draft, out var absorbed, out var remainder) && Offered(model.Channels, absorbed.Key))
        {
            draft = remainder;
            channelKey = absorbed.Key;
        }

        var used = Encoding.UTF8.GetByteCount(draft);
        var hasText = draft.Trim().Length > 0;
        DrawSend(drawList, sendCenter, sendDiameter, hasText, used, budget, theme, scale);
        if (hasText && UiInteract.HoverClickCircle(sendCenter, sendDiameter * 0.5f))
        {
            submitted = true;
        }

        var picked = DrawChannelMenu(model, channelKey);
        if (picked.Length > 0)
        {
            channelKey = picked;
        }

        if (!submitted || !hasText)
        {
            return new GameComposerResult(false, string.Empty, channelKey);
        }

        var text = draft;
        focus = true;
        return new GameComposerResult(true, text, channelKey);
    }

    public void Clear() => draft = string.Empty;

    private string DrawChannelMenu(in GameComposerModel model, string activeKey)
    {
        if (!channelMenu.IsOpenFor("linkpearl.composer.channel"))
        {
            return string.Empty;
        }

        menuItems.Clear();
        menuKeys.Clear();
        var channels = model.Channels;
        for (var index = 0; index < channels.Length; index++)
        {
            if (!GameChannels.TryByKey(channels[index], out var channel) || !channel.CanSend || channel.NeedsTarget)
            {
                continue;
            }

            menuItems.Add(new DropdownMenu.Item(GameChannels.DisplayName(channel), string.Empty, false,
                string.Equals(channel.Key, activeKey, StringComparison.Ordinal)));
            menuKeys.Add(channel.Key);
        }

        if (menuItems.Count == 0)
        {
            channelMenu.Close();
            return string.Empty;
        }

        var clicked = channelMenu.Draw(model.Screen, model.Theme, CollectionsMarshal.AsSpan(menuItems));
        return clicked >= 0 ? menuKeys[clicked] : string.Empty;
    }

    private static bool Offered(ReadOnlySpan<string> channels, string key)
    {
        for (var index = 0; index < channels.Length; index++)
        {
            if (string.Equals(channels[index], key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static float DrawChipWidth(GameChannel channel, float scale) =>
        Typography.Measure(ShortName(channel), TextStyles.Caption1).X + 18f * scale;

    private static void DrawChip(ImDrawListPtr drawList, Vector2 min, Vector2 max, GameChannel channel,
        float scale)
    {
        var tint = channel.Tint;
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.18f)));
        var label = Typography.FitText(ShortName(channel), max.X - min.X - 14f * scale, TextStyles.Caption1);
        var center = new Vector2((min.X + max.X) * 0.5f - 3f * scale, (min.Y + max.Y) * 0.5f);
        Typography.DrawCentered(drawList, center, label, tint, TextStyles.Caption1);
        AppSkin.Icon(drawList, new Vector2(max.X - 6f * scale, center.Y + 1f * scale),
            IconGlyph.Of(FontAwesomeIcon.CaretDown), Palette.WithAlpha(tint, 0.8f), 0.6f);
    }

    private static void DrawSend(ImDrawListPtr drawList, Vector2 center, float diameter, bool hasText, int used,
        int budget, PhoneTheme theme, float scale)
    {
        var radius = diameter * 0.5f;
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(hasText ? theme.Accent : theme.SurfaceMuted), 24);
        AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.ArrowUp), new Vector4(1f, 1f, 1f, 1f), 0.72f);
        if (budget <= 0)
        {
            return;
        }

        var fraction = Math.Clamp((float)used / budget, 0f, 1f);
        if (fraction < RingThreshold)
        {
            return;
        }

        var ringColor = fraction >= 0.95f ? theme.Danger : new Vector4(0.88f, 0.65f, 0.38f, 1f);
        ProgressRing.Fill(center, radius + 2.5f * scale, 2f * scale, fraction, ringColor);
    }

    private static void DrawReadOnly(Rect bar, PhoneTheme theme) =>
        Typography.DrawCentered(ImGui.GetWindowDrawList(), bar.Center, Loc.T(L.Linkpearl.ChannelReadOnly),
            theme.TextMuted, TextStyles.Caption1);

    private static string ShortName(GameChannel channel)
    {
        if (channel.IsSlotted)
        {
            return channel.Key.ToUpperInvariant();
        }

        var name = GameChannels.DisplayName(channel);
        var space = name.IndexOf(' ');
        return space > 0 ? name[..space] : name;
    }
}
