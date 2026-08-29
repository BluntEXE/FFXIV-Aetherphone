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
    public readonly bool IsCommand;
    public readonly string Text;
    public readonly string ChannelKey;

    public GameComposerResult(bool submitted, bool isCommand, string text, string channelKey)
    {
        Submitted = submitted;
        IsCommand = isCommand;
        Text = text;
        ChannelKey = channelKey;
    }
}

internal sealed class GameComposer
{
    private const float ChipMaxWidth = 58f;
    private const float RingThreshold = 0.55f;
    private const float RowHeight = 38f;
    private const float PillInset = 7f;
    private const float EmojiRadius = 12f;
    private const int MinimumLines = 1;
    private const int MaximumLines = 10;
    private const long DoubleEnterWindowMilliseconds = 700;

    private readonly DropdownMenu channelMenu = new();
    private readonly GameEmojiComposer emoji = new();
    private readonly List<DropdownMenu.Item> menuItems = new(24);
    private readonly List<string> menuKeys = new(24);
    private readonly List<string> splitScratch = new(MessageSplitter.MaxParts);
    private readonly ImGui.ImGuiInputTextCallbackPtrDelegate multilineCallback;
    private string conversationKey = string.Empty;
    private string draft = string.Empty;
    private string display = string.Empty;
    private string wrappedSource = string.Empty;
    private string countedSource = string.Empty;
    private string countedIndicator = string.Empty;
    private float wrappedWidth;
    private int countedBudget = -1;
    private int countedParts = 1;
    private int lineCount = 1;
    private int capacityBytes;
    private long lastEnterMilliseconds;
    private bool wrappedMultiline;
    private bool pendingSync;
    private bool enterPressed;
    private bool focus;

    public GameComposer() => multilineCallback = OnMultilineCallback;

    public string Draft => draft;

    public void Gate() => channelMenu.Gate();

    public void CloseMenus()
    {
        channelMenu.Close();
        emoji.Close();
    }

    public void Bind(string nextConversationKey)
    {
        if (string.Equals(conversationKey, nextConversationKey, StringComparison.Ordinal))
        {
            return;
        }

        ChatDrafts.Store(conversationKey, draft);
        conversationKey = nextConversationKey;
        Adopt(ChatDrafts.Load(nextConversationKey));
        focus = false;
        channelMenu.Close();
    }

    public void Unbind()
    {
        ChatDrafts.Store(conversationKey, draft);
        ChatDrafts.Flush();
        conversationKey = string.Empty;
        Adopt(string.Empty);
        focus = false;
        channelMenu.Close();
    }

    public void Reset()
    {
        Adopt(string.Empty);
        focus = false;
        channelMenu.Close();
        emoji.Close();
    }

    public void Refill(string text)
    {
        Adopt(text);
        focus = true;
    }

    public void Clear()
    {
        Adopt(string.Empty);
        ChatDrafts.Store(conversationKey, string.Empty);
    }

    public float Measure(float barWidth, in GameComposerModel model)
    {
        var scale = UiScale.Current;
        var baseHeight = (RowHeight + PillInset * 2f) * scale;
        if (!Multiline || !Sendable(model, out var channel))
        {
            return baseHeight;
        }

        Rewrap(WrapWidthOf(InnerWidth(barWidth, channel, scale), scale));
        var visible = Math.Clamp(lineCount, MinimumLines, MaxLines);
        return baseHeight + (visible - 1) * ImGui.GetTextLineHeight();
    }

    public GameComposerResult Draw(Rect bar, in GameComposerModel model)
    {
        var scale = UiScale.Current;
        var theme = model.Theme;
        var drawList = ImGui.GetWindowDrawList();
        var channelKey = model.ActiveChannel;
        if (!Sendable(model, out var channel))
        {
            DrawReadOnly(bar, theme);
            return new GameComposerResult(false, false, string.Empty, channelKey);
        }

        var indicator = Indicator;
        var budget = ChatSend.Budget(channel, model.SendTarget);
        capacityBytes = Splitting ? MessageSplitter.Capacity(budget, indicator) : budget;
        var pillMin = new Vector2(bar.Min.X + Metrics.Space.Md * scale, bar.Min.Y + PillInset * scale);
        var pillMax = new Vector2(bar.Max.X - Metrics.Space.Md * scale, bar.Max.Y - PillInset * scale);
        var rowHeight = RowHeight * scale;
        var chipWidth = MathF.Min(ChipMaxWidth * scale, ChipWidthOf(channel, scale));
        var chipMin = new Vector2(pillMin.X, pillMax.Y - rowHeight + 3f * scale);
        var chipMax = new Vector2(chipMin.X + chipWidth, pillMax.Y - 3f * scale);
        var sendDiameter = rowHeight - 6f * scale;
        var sendCenter = new Vector2(pillMax.X - sendDiameter * 0.5f, pillMax.Y - rowHeight * 0.5f);
        var emojiRadius = GameEmojiComposer.PickerEnabled ? EmojiRadius * scale : 0f;
        var emojiCenter = new Vector2(chipMax.X + Metrics.Space.Xs * scale + emojiRadius,
            (pillMin.Y + pillMax.Y) * 0.5f);
        var fieldMin = new Vector2(emojiCenter.X + emojiRadius + Metrics.Space.Xs * scale, pillMin.Y);
        var fieldMax = new Vector2(sendCenter.X - sendDiameter * 0.5f - Metrics.Space.Xs * scale, pillMax.Y);
        Squircle.Fill(drawList, fieldMin, fieldMax, MathF.Min(fieldMax.Y - fieldMin.Y, rowHeight) * 0.5f,
            ImGui.GetColorU32(theme.GroupedCard));
        DrawChip(drawList, chipMin, chipMax, channel, scale);
        if (UiInteract.HoverClick(chipMin, chipMax))
        {
            channelMenu.Toggle("linkpearl.composer.channel", new Rect(chipMin, chipMax));
        }

        emoji.DrawToggle(emojiCenter, emojiRadius, theme);

        var innerWidth = MathF.Max(1f, fieldMax.X - fieldMin.X - Metrics.Space.Md * scale);
        enterPressed = false;
        if (Multiline != wrappedMultiline)
        {
            wrappedMultiline = Multiline;
            wrappedSource = string.Empty;
            wrappedWidth = 0f;
        }

        if (Multiline)
        {
            DrawMultiline(fieldMin, fieldMax, innerWidth, theme, scale);
        }
        else
        {
            DrawSingleLine(fieldMin, fieldMax, innerWidth, theme, scale);
        }

        emoji.DrawSuggestions(bar, model.Screen, theme, ref draft);
        var pickedEmoji = emoji.DrawPanel(bar, model.Screen, theme);
        if (pickedEmoji is not null && Encoding.UTF8.GetByteCount(draft) + pickedEmoji.Length <= capacityBytes)
        {
            Adopt(draft + pickedEmoji);
        }

        if (ChatCommands.TryAbsorb(draft, out var absorbed, out var remainder) && Offered(model.Channels, absorbed.Key))
        {
            Adopt(remainder);
            channelKey = absorbed.Key;
        }

        ChatDrafts.Store(conversationKey, draft);
        var used = Encoding.UTF8.GetByteCount(draft);
        var hasText = HasContent(draft);
        var parts = hasText && Splitting ? PartCount(budget, indicator) : 1;
        DrawSend(drawList, sendCenter, sendDiameter, hasText, used, capacityBytes, parts, theme, scale);
        var submitted = ConsumeEnter();
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
            return new GameComposerResult(false, false, string.Empty, channelKey);
        }

        var text = draft.Trim();
        focus = true;
        return new GameComposerResult(true, text[0] == '/', text, channelKey);
    }

    private static bool Multiline => Plugin.Cfg?.LinkpearlComposerMultiline ?? true;

    private static bool Splitting => Plugin.Cfg?.LinkpearlSplitLongMessages ?? true;

    private static bool DoubleEnter => Plugin.Cfg?.LinkpearlDoubleEnterSend ?? false;

    private static string Indicator => Plugin.Cfg?.LinkpearlSplitIndicator ?? string.Empty;

    private static int MaxLines =>
        Math.Clamp(Plugin.Cfg?.LinkpearlComposerMaxLines ?? 4, MinimumLines, MaximumLines);

    private static bool Sendable(in GameComposerModel model, out GameChannel channel) =>
        GameChannels.TryByKey(model.ActiveChannel, out channel) && channel.CanSend &&
        (!channel.NeedsTarget || model.SendTarget.Length > 0);

    private void Adopt(string text)
    {
        draft = text;
        wrappedSource = string.Empty;
        wrappedWidth = 0f;
        display = text;
        lineCount = 1;
        pendingSync = true;
    }

    private void DrawSingleLine(Vector2 fieldMin, Vector2 fieldMax, float innerWidth, PhoneTheme theme, float scale)
    {
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + Metrics.Space.Sm * scale,
            (fieldMin.Y + fieldMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(innerWidth);
        if (focus)
        {
            ImGui.SetKeyboardFocusHere();
            focus = false;
        }

        Plugin.Fonts.NoticeText(draft);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##linkpearl.composer", Loc.T(L.Messages.Placeholder), ref draft,
                    Math.Max(1, capacityBytes), ImGuiInputTextFlags.EnterReturnsTrue))
            {
                enterPressed = true;
            }
        }
    }

    private void DrawMultiline(Vector2 fieldMin, Vector2 fieldMax, float innerWidth, PhoneTheme theme, float scale)
    {
        var wrapWidth = WrapWidthOf(innerWidth, scale);
        Rewrap(wrapWidth);
        var visible = Math.Clamp(lineCount, MinimumLines, MaxLines);
        var boxHeight = visible * ImGui.GetTextLineHeight() + ImGui.GetStyle().FramePadding.Y * 2f;
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + Metrics.Space.Sm * scale,
            (fieldMin.Y + fieldMax.Y) * 0.5f - boxHeight * 0.5f));
        if (focus)
        {
            ImGui.SetKeyboardFocusHere();
            focus = false;
        }

        Plugin.Fonts.NoticeText(display);
        var bufferBytes = capacityBytes * 4 + 1024;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.InputTextMultiline("##linkpearl.composer", ref display, bufferBytes,
                new Vector2(innerWidth, boxHeight),
                ImGuiInputTextFlags.CallbackEdit | ImGuiInputTextFlags.CallbackCharFilter |
                ImGuiInputTextFlags.CallbackAlways, multilineCallback);
        }

        if (!ImGui.IsItemActive())
        {
            pendingSync = false;
        }

        if (draft.Length > 0)
        {
            return;
        }

        var padding = ImGui.GetStyle().FramePadding;
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(fieldMin.X + Metrics.Space.Sm * scale + padding.X,
                (fieldMin.Y + fieldMax.Y) * 0.5f - boxHeight * 0.5f + padding.Y),
            Loc.T(L.Messages.Placeholder), theme.TextMuted, TextStyles.Body);
    }

    private int OnMultilineCallback(ImGuiInputTextCallbackDataPtr data)
    {
        if (data.EventFlag == ImGuiInputTextFlags.CallbackCharFilter)
        {
            return FilterCharacter(data);
        }

        if (data.EventFlag == ImGuiInputTextFlags.CallbackAlways)
        {
            if (pendingSync)
            {
                pendingSync = false;
                SyncBuffer(data);
            }

            return 0;
        }

        ApplyWrap(data);
        return 0;
    }

    private void SyncBuffer(ImGuiInputTextCallbackDataPtr data)
    {
        data.DeleteChars(0, data.BufTextLen);
        if (display.Length > 0)
        {
            data.InsertChars(0, display);
        }

        data.CursorPos = data.BufTextLen;
        data.SelectionStart = data.CursorPos;
        data.SelectionEnd = data.CursorPos;
    }

    private int FilterCharacter(ImGuiInputTextCallbackDataPtr data)
    {
        if (data.EventChar == '\r')
        {
            data.EventChar = 0;
            return 0;
        }

        if (data.EventChar != '\n' || ImGui.GetIO().KeyShift)
        {
            return 0;
        }

        enterPressed = true;
        data.EventChar = 0;
        return 0;
    }

    private void ApplyWrap(ImGuiInputTextCallbackDataPtr data)
    {
        var current = Encoding.UTF8.GetString(data.BufSpan[..data.BufTextLen]);
        var charCursor = CharIndexOf(current, data.CursorPos);
        var logical = Unwrap(current, wrappedWidth, charCursor, out var logicalCursor);
        logical = CapBytes(logical, capacityBytes, ref logicalCursor);
        var wrapped = Wrap(logical, wrappedWidth);
        draft = logical;
        wrappedSource = logical;
        lineCount = CountLines(wrapped);
        if (string.Equals(wrapped, current, StringComparison.Ordinal))
        {
            return;
        }

        var displayCursor = DisplayIndexOf(wrapped, logical, logicalCursor);
        var byteCursor = Encoding.UTF8.GetByteCount(wrapped.AsSpan(0, displayCursor));
        data.DeleteChars(0, data.BufTextLen);
        data.InsertChars(0, wrapped);
        data.CursorPos = byteCursor;
        data.SelectionStart = byteCursor;
        data.SelectionEnd = byteCursor;
    }

    private void Rewrap(float width)
    {
        if (MathF.Abs(width - wrappedWidth) < 0.5f && string.Equals(wrappedSource, draft, StringComparison.Ordinal))
        {
            return;
        }

        wrappedWidth = width;
        wrappedSource = draft;
        display = Wrap(draft, width);
        lineCount = CountLines(display);
    }

    private bool ConsumeEnter()
    {
        if (!enterPressed)
        {
            return false;
        }

        enterPressed = false;
        if (!DoubleEnter)
        {
            return true;
        }

        var now = Environment.TickCount64;
        var quick = now - lastEnterMilliseconds <= DoubleEnterWindowMilliseconds;
        lastEnterMilliseconds = quick ? 0 : now;
        return quick;
    }

    private int PartCount(int budget, string indicator)
    {
        if (countedBudget == budget && string.Equals(countedSource, draft, StringComparison.Ordinal) &&
            string.Equals(countedIndicator, indicator, StringComparison.Ordinal))
        {
            return countedParts;
        }

        countedBudget = budget;
        countedSource = draft;
        countedIndicator = indicator;
        if (draft.IndexOf('\n') < 0 && Encoding.UTF8.GetByteCount(draft) <= budget)
        {
            countedParts = 1;
            return countedParts;
        }

        MessageSplitter.Split(draft, budget, indicator, splitScratch);
        countedParts = Math.Max(1, splitScratch.Count);
        return countedParts;
    }

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

    private static float InnerWidth(float barWidth, GameChannel channel, float scale)
    {
        var chipWidth = MathF.Min(ChipMaxWidth * scale, ChipWidthOf(channel, scale));
        var sendDiameter = (RowHeight - 6f) * scale;
        var emojiWidth = GameEmojiComposer.PickerEnabled
            ? EmojiRadius * 2f * scale + Metrics.Space.Xs * scale
            : 0f;
        var fieldWidth = barWidth - Metrics.Space.Md * 2f * scale - chipWidth - sendDiameter - emojiWidth -
                         Metrics.Space.Xs * 2f * scale;
        return MathF.Max(1f, fieldWidth - Metrics.Space.Md * scale);
    }

    private static float WrapWidthOf(float innerWidth, float scale) =>
        MathF.Max(1f, innerWidth - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale);

    private static string Wrap(string logical, float width)
    {
        if (logical.IndexOf('\n') < 0)
        {
            return SoftWrap.WrapText(logical, width);
        }

        var builder = new StringBuilder(logical.Length + 16);
        var start = 0;
        while (true)
        {
            var found = logical.IndexOf('\n', start);
            var stop = found < 0 ? logical.Length : found;
            builder.Append(SoftWrap.WrapText(logical[start..stop], width));
            if (found < 0)
            {
                break;
            }

            builder.Append('\n');
            start = found + 1;
        }

        return builder.ToString();
    }

    private static string Unwrap(string display, float width, int displayCursor, out int logicalCursor)
    {
        if (display.IndexOf('\n') < 0)
        {
            logicalCursor = Math.Clamp(displayCursor, 0, display.Length);
            return display;
        }

        var builder = new StringBuilder(display.Length);
        var lineStart = 0;
        var index = 0;
        logicalCursor = -1;
        while (index < display.Length)
        {
            if (index == displayCursor)
            {
                logicalCursor = builder.Length;
            }

            if (display[index] != '\n')
            {
                builder.Append(display[index]);
                index++;
                continue;
            }

            if (!IsSoftBreak(display, lineStart, index, width))
            {
                builder.Append('\n');
            }

            index++;
            lineStart = index;
        }

        if (logicalCursor < 0)
        {
            logicalCursor = builder.Length;
        }

        return builder.ToString();
    }

    private static bool IsSoftBreak(string display, int lineStart, int newlineIndex, float width)
    {
        var wordEnd = newlineIndex + 1;
        while (wordEnd < display.Length && display[wordEnd] != '\n' && !char.IsWhiteSpace(display[wordEnd]))
        {
            wordEnd++;
        }

        if (wordEnd == newlineIndex + 1)
        {
            return false;
        }

        var probe = string.Concat(display.AsSpan(lineStart, newlineIndex - lineStart),
            display.AsSpan(newlineIndex + 1, wordEnd - newlineIndex - 1));
        return SoftWrap.WrapText(probe, width).Contains('\n');
    }

    private static int DisplayIndexOf(string display, string logical, int logicalCursor)
    {
        var logicalIndex = 0;
        var displayIndex = 0;
        while (displayIndex < display.Length && logicalIndex < logicalCursor)
        {
            if (logicalIndex < logical.Length && display[displayIndex] == logical[logicalIndex])
            {
                logicalIndex++;
            }

            displayIndex++;
        }

        return displayIndex;
    }

    private static int CharIndexOf(string text, int byteIndex)
    {
        if (byteIndex <= 0)
        {
            return 0;
        }

        var bytes = 0;
        var index = 0;
        while (index < text.Length && bytes < byteIndex)
        {
            var runeLength = RuneLength(text, index);
            bytes += Encoding.UTF8.GetByteCount(text.AsSpan(index, runeLength));
            index += runeLength;
        }

        return index;
    }

    private static string CapBytes(string text, int capacity, ref int cursor)
    {
        if (capacity <= 0)
        {
            cursor = 0;
            return string.Empty;
        }

        if (Encoding.UTF8.GetByteCount(text) <= capacity)
        {
            return text;
        }

        var bytes = 0;
        var index = 0;
        while (index < text.Length)
        {
            var runeLength = RuneLength(text, index);
            var runeBytes = Encoding.UTF8.GetByteCount(text.AsSpan(index, runeLength));
            if (bytes + runeBytes > capacity)
            {
                break;
            }

            bytes += runeBytes;
            index += runeLength;
        }

        if (cursor > index)
        {
            cursor = index;
        }

        return text[..index];
    }

    private static int RuneLength(string text, int index) =>
        char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;

    private static bool HasContent(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountLines(string text)
    {
        var lines = 1;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                lines++;
            }
        }

        return lines;
    }

    private static float ChipWidthOf(GameChannel channel, float scale) =>
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
        int budget, int parts, PhoneTheme theme, float scale)
    {
        var radius = diameter * 0.5f;
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(hasText ? theme.Accent : theme.SurfaceMuted), 24);
        AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.ArrowUp), new Vector4(1f, 1f, 1f, 1f), 0.88f);
        if (parts > 1)
        {
            DrawPartBadge(drawList, center, radius, parts, theme, scale);
            return;
        }

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

    private static void DrawPartBadge(ImDrawListPtr drawList, Vector2 center, float radius, int parts,
        PhoneTheme theme, float scale)
    {
        var badgeCenter = new Vector2(center.X + radius - 1f * scale, center.Y - radius + 1f * scale);
        var badgeRadius = 7f * scale;
        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(theme.AppBackground), 16);
        drawList.AddCircleFilled(badgeCenter, badgeRadius - 1f * scale, ImGui.GetColorU32(theme.Accent), 16);
        Typography.DrawCentered(drawList, badgeCenter, parts.ToString(Loc.Culture), new Vector4(1f, 1f, 1f, 1f),
            TextStyles.Caption2);
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
