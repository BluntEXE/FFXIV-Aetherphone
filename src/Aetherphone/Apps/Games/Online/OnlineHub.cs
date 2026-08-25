using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Apps;
using Aetherphone.Core.GameRooms;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Games.Online;

// The friends lobby: host a room, type a code, or walk back into a room you are already in. The
// code IS the door, so this screen owns exactly three verbs and the room screen owns the rest.
internal sealed class OnlineHub
{
    private const float HeaderHeight = 42f;
    private const float ActionRowHeight = 60f;
    private const float FieldHeight = 40f;
    private const float RoomRowHeight = 64f;
    private const int CodeBufferLength = 16;

    private readonly GameRoomsStore store;
    private readonly Action<string> openRoom;

    private string codeBuffer = string.Empty;
    private string inlineReason = string.Empty;

    public OnlineHub(GameRoomsStore store, Action<string> openRoom)
    {
        this.store = store;
        this.openRoom = openRoom;
    }

    public void Enter()
    {
        inlineReason = string.Empty;
        codeBuffer = string.Empty;
        store.RefreshNow();
    }

    public void Draw(in PhoneContext context, Action back)
    {
        AppHeader.Draw(context, Loc.T(L.Games.OnlineTitle), back);
        var scale = UiScale.Current;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + HeaderHeight * scale), content.Max);
        Consume();
        store.EnsureFresh();
        using var surface = AppSurface.Begin(body);
        var theme = context.Theme;

        if (store.AccountId.Length == 0)
        {
            DrawNotice(theme, scale, Loc.T(L.Games.OnlineSignIn));
            return;
        }

        DrawHostRow(theme, scale, GameRoomWire.UnoKind, Loc.T(L.Games.OnlineUno),
            Loc.T(L.Games.OnlineHostHint, "6"));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        DrawHostRow(theme, scale, GameRoomWire.ChessKind, Loc.T(L.Games.OnlineChess),
            Loc.T(L.Games.OnlineChessHostHint));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        DrawHostRow(theme, scale, GameRoomWire.PoolKind, Loc.T(L.Games.OnlinePool),
            Loc.T(L.Games.OnlinePoolHostHint));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        DrawJoinByCode(theme, scale);
        if (inlineReason.Length > 0)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            DrawNotice(theme, scale, Loc.T(GamesOnlineText.ReasonMessage(inlineReason)));
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        DrawRoomsHeading(theme, scale);
        DrawRooms(theme, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void Consume()
    {
        var answer = store.TakeRoomAnswer();
        if (answer is null)
        {
            return;
        }

        if (answer.Intent is GameRoomIntent.Created or GameRoomIntent.Joined)
        {
            if (answer.Granted && answer.Room is not null)
            {
                inlineReason = string.Empty;
                codeBuffer = string.Empty;
                store.Enter(answer.Room.RoomId);
                openRoom(answer.Room.RoomId);
                return;
            }

            inlineReason = answer.Reason;
        }
    }

    private void DrawHostRow(PhoneTheme theme, float scale, string gameKind, string gameName, string hint)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + ActionRowHeight * scale));
        var rounding = Metrics.Radius.Card * scale;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        Squircle.Fill(drawList, row.Min, row.Max, rounding, ImGui.GetColorU32(theme.GroupedCard));
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, rounding,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.05f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var accent = Core.Apps.AppAccents.For("games");
        var iconCenter = new Vector2(row.Min.X + 26f * scale, row.Center.Y);
        drawList.AddCircleFilled(iconCenter, 15f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(accent, 0.16f)), 32);
        ProgressRing.CenterIcon(drawList, iconCenter, FontAwesomeIcon.UserFriends, accent, 13f * scale);

        var textLeft = row.Min.X + 48f * scale;
        var textWidth = row.Width - 62f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 12f * scale),
            Typography.FitText(Loc.T(L.Games.OnlineHost) + " · " + gameName, textWidth,
                TextStyles.SubheadlineEmphasized), theme.TextStrong, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 32f * scale),
            Typography.FitText(hint, textWidth, TextStyles.Footnote),
            theme.TextMuted, TextStyles.Footnote);

        var clicked = UiInteract.Click(row.Min, row.Max, hovered);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, ActionRowHeight * scale));
        if (clicked && !store.IntentInFlight)
        {
            inlineReason = string.Empty;
            store.CreateRoom(gameKind);
        }
    }

    private void DrawJoinByCode(PhoneTheme theme, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        Typography.Draw(drawList, origin, Loc.T(L.Games.OnlineJoinHeading), theme.TextMuted,
            TextStyles.FootnoteEmphasized);
        var fieldTop = origin.Y + 20f * scale;
        var pillWidth = 92f * scale;
        var fieldMin = new Vector2(origin.X, fieldTop);
        var fieldMax = new Vector2(origin.X + width - pillWidth - 8f * scale, fieldTop + FieldHeight * scale);
        Squircle.Fill(drawList, fieldMin, fieldMax, Metrics.Radius.Field * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)));
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + 10f * scale,
            (fieldMin.Y + fieldMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldMax.X - fieldMin.X - 20f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.InputTextWithHint("##gameRoomCode", Loc.T(L.Games.OnlineJoinHint), ref codeBuffer,
                CodeBufferLength);
        }

        var trimmed = codeBuffer.Trim();
        var pillCenter = new Vector2(origin.X + width - pillWidth * 0.5f,
            fieldTop + FieldHeight * scale * 0.5f);
        var accent = Core.Apps.AppAccents.For("games");
        if (GameHud.Button(pillCenter, new Vector2(pillWidth, FieldHeight * scale), Loc.T(L.Games.OnlineJoin),
                trimmed.Length > 0 && !store.IntentInFlight ? accent : theme.TextMuted, theme)
            && trimmed.Length > 0 && !store.IntentInFlight)
        {
            inlineReason = string.Empty;
            store.JoinByCode(trimmed);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 20f * scale + FieldHeight * scale));
    }

    private void DrawRoomsHeading(PhoneTheme theme, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        Typography.Draw(ImGui.GetWindowDrawList(), origin, Loc.T(L.Games.OnlineMyRooms), theme.TextStrong,
            TextStyles.Title3);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(1f, 30f * scale));
    }

    private void DrawRooms(PhoneTheme theme, float scale)
    {
        var rooms = store.Rooms;
        if (rooms.Length == 0)
        {
            DrawNotice(theme, scale,
                Loc.T(store.LoadedRooms ? L.Games.OnlineNoRooms : L.Games.OnlineLoading));
            return;
        }

        var card = GroupCard.Begin(theme, rooms.Length, RoomRowHeight);
        for (var index = 0; index < rooms.Length; index++)
        {
            if (DrawRoomRow(card.NextRow(), theme, scale, rooms[index]))
            {
                inlineReason = string.Empty;
                store.Enter(rooms[index].RoomId);
                openRoom(rooms[index].RoomId);
            }
        }

        card.End();
    }

    private static bool DrawRoomRow(Rect row, PhoneTheme theme, float scale, GameRoomCardDto room)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var gameName = Loc.T(GamesOnlineText.GameName(room.GameKind));
        var title = gameName + " · " + Loc.T(L.Games.OnlineHostedBy, room.OwnerName);
        var phase = room.Phase switch
        {
            GameRoomWire.PhasePlaying => L.Games.OnlinePhasePlaying,
            GameRoomWire.PhaseFinished => L.Games.OnlinePhaseFinished,
            _ => L.Games.OnlinePhaseLobby,
        };
        var subtitle = Loc.T(L.Games.OnlineSeats, room.SeatedCount.ToString(Loc.Culture),
            room.MaxSeats.ToString(Loc.Culture)) + " · " + Loc.T(phase);
        var textWidth = row.Width - 8f * scale;
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - 17f * scale),
            Typography.FitText(title, textWidth, TextStyles.Headline), theme.TextStrong, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y + 2f * scale),
            Typography.FitText(subtitle, textWidth, TextStyles.Footnote), theme.TextMuted, TextStyles.Footnote);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static void DrawNotice(PhoneTheme theme, float scale, string message)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = 14f * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, width - pad * 2f);
        var height = block.Y + pad * 2f;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, origin, max, Metrics.Radius.Card * scale,
            ImGui.GetColorU32(theme.GroupedCard));
        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + pad), message, theme.TextMuted,
            TextStyles.Footnote, width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }
}
