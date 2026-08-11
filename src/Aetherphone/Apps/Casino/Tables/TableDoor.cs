using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class TableDoor
{
    private const float PillHeight = 40f;
    private const float RowHeight = 56f;
    private const float RowGap = 8f;

    private readonly CasinoTablesStore tables;
    private readonly ConfirmService confirm;
    private readonly Action<string> openTable;

    private string roomId = string.Empty;
    private string inviteToken = string.Empty;
    private string inlineReason = string.Empty;

    public TableDoor(CasinoTablesStore tables, ConfirmService confirm, Action<string> openTable)
    {
        this.tables = tables;
        this.confirm = confirm;
        this.openTable = openTable;
    }

    public string RoomId => roomId;

    public void Enter(string tableId, string token)
    {
        roomId = tableId;
        inviteToken = token;
        inlineReason = string.Empty;
        tables.RefreshDoorNow(tableId);
    }

    public void Reset()
    {
        roomId = string.Empty;
        inviteToken = string.Empty;
        inlineReason = string.Empty;
        tables.ForgetDoor();
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        ConsumeOutcomes();
        using var surface = AppSurface.Begin(body);
        var door = tables.DoorFor(roomId);
        var token = door is not null && door.InviteToken.Length > 0 ? door.InviteToken : inviteToken;

        DrawInviteCard(ui, token, scale);
        if (inlineReason.Length > 0)
        {
            DrawInlineReason(ui, scale);
        }

        DrawOpenTable(ui, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));

        ui.SectionHeading(Loc.T(L.Casino.DoorKnocksHeading), 4f);
        var knocks = door?.Knocks;
        if (knocks is null || knocks.Length == 0)
        {
            DrawNote(ui, Loc.T(L.Casino.DoorNoKnocks), scale);
        }
        else
        {
            for (var index = 0; index < knocks.Length; index++)
            {
                DrawKnockRow(ui, knocks[index], scale);
            }
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        ui.SectionHeading(Loc.T(L.Casino.DoorSeatedHeading), 4f);
        var seated = door?.Seated;
        if (seated is null || seated.Length == 0)
        {
            DrawNote(ui, Loc.T(L.Casino.DoorNobodySeated), scale);
        }
        else
        {
            for (var index = 0; index < seated.Length; index++)
            {
                DrawSeatedRow(ui, seated[index], scale);
            }
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void ConsumeOutcomes()
    {
        if (tables.TakeIntentFailure())
        {
            inlineReason = CasinoReasons.Unreachable;
        }

        var outcome = tables.TakeNoticeOutcome();
        if (outcome is not null)
        {
            inlineReason = outcome.Granted ? string.Empty : outcome.Reason;
        }
    }

    private void DrawInviteCard(AppSkin ui, string token, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = 14f * scale;
        var shareText = token.Length > 0 ? CasinoShare.Compose(token) : Loc.T(L.Casino.DoorTokenPending);
        var tokenBlock = Typography.MeasureWrappedBlock(shareText, TextStyles.Footnote, width - pad * 2f);
        var height = 20f * scale + tokenBlock.Y + PillHeight * scale + pad * 2f + 8f * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, card.Min, card.Max, rounding);
        Squircle.Stroke(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.30f)), 1f * scale);
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, card.Min.Y + pad), Loc.T(L.Casino.DoorInviteHeading),
            ui.TitleInk, TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(card.Min.X + pad, card.Min.Y + pad + 20f * scale), shareText,
            ui.MutedInk, TextStyles.Footnote, width - pad * 2f);

        var pillRect = new Rect(new Vector2(card.Min.X + pad, card.Max.Y - PillHeight * scale - pad),
            new Vector2(card.Max.X - pad, card.Max.Y - pad));
        if (AppSkin.PillButton(pillRect, Loc.T(L.Casino.DoorCopyInvite), false, token.Length > 0, ui.Theme)
            && token.Length > 0)
        {
            ImGui.SetClipboardText(CasinoShare.Compose(token));
            CopyToast.Show();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawInlineReason(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var message = Loc.T(CasinoReasons.MessageFor(inlineReason));
        var pad = 12f * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, width - pad * 2f);
        var height = block.Y + pad * 2f;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, min, max, 16f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), message, ui.TitleInk, TextStyles.Footnote,
            width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawOpenTable(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + PillHeight * scale));
        if (AppSkin.PillButton(rect, Loc.T(L.Casino.DoorOpenTable), true, roomId.Length > 0, ui.Theme))
        {
            openTable(roomId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, PillHeight * scale));
    }

    private static void DrawNote(AppSkin ui, string text, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = 14f * scale;
        var block = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, width - pad * 2f);
        var height = block.Y + pad * 2f;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), text, ui.MutedInk, TextStyles.Footnote,
            width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + RowGap * scale));
    }

    private void DrawKnockRow(AppSkin ui, CasinoKnockerDto knocker, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = RowHeight * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        ui.Card(drawList, row.Min, row.Max, Metrics.Radius.Card * scale);

        var buttonWidth = 74f * scale;
        var textWidth = width - buttonWidth * 2f - 32f * scale;
        DrawIdentity(drawList, ui, knocker, row, textWidth, scale);

        var approveRect = new Rect(
            new Vector2(row.Max.X - buttonWidth * 2f - 18f * scale, row.Center.Y - PillHeight * scale * 0.5f),
            new Vector2(row.Max.X - buttonWidth - 18f * scale, row.Center.Y + PillHeight * scale * 0.5f));
        if (AppSkin.PillButton(approveRect, Loc.T(L.Casino.DoorApprove), true, !tables.IntentInFlight, ui.Theme))
        {
            inlineReason = string.Empty;
            tables.AnswerKnock(roomId, knocker.UserId, true);
        }

        var denyRect = new Rect(
            new Vector2(row.Max.X - buttonWidth - 10f * scale, row.Center.Y - PillHeight * scale * 0.5f),
            new Vector2(row.Max.X - 10f * scale, row.Center.Y + PillHeight * scale * 0.5f));
        if (AppSkin.PillButton(denyRect, Loc.T(L.Casino.DoorDeny), false, !tables.IntentInFlight, ui.Theme))
        {
            inlineReason = string.Empty;
            tables.AnswerKnock(roomId, knocker.UserId, false);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + RowGap * scale));
    }

    private void DrawSeatedRow(AppSkin ui, CasinoKnockerDto occupant, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = RowHeight * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        ui.Card(drawList, row.Min, row.Max, Metrics.Radius.Card * scale);

        var buttonWidth = 84f * scale;
        DrawIdentity(drawList, ui, occupant, row, width - buttonWidth - 32f * scale, scale);

        var removeRect = new Rect(
            new Vector2(row.Max.X - buttonWidth - 10f * scale, row.Center.Y - PillHeight * scale * 0.5f),
            new Vector2(row.Max.X - 10f * scale, row.Center.Y + PillHeight * scale * 0.5f));
        if (ui.DangerGhostButton(removeRect, Loc.T(L.Casino.DoorRemove)) && !tables.IntentInFlight)
        {
            AskRemove(occupant);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + RowGap * scale));
    }

    private static void DrawIdentity(ImDrawListPtr drawList, AppSkin ui, CasinoKnockerDto person, in Rect row,
        float textWidth, float scale)
    {
        var left = row.Min.X + 14f * scale;
        var name = person.DisplayName.Length > 0 ? person.DisplayName : person.Handle;
        Typography.Draw(drawList, new Vector2(left, row.Min.Y + 10f * scale),
            Typography.FitText(name, textWidth, TextStyles.SubheadlineEmphasized), ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        if (person.Handle.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(left, row.Min.Y + 30f * scale),
                Typography.FitText(person.Handle, textWidth, TextStyles.Caption1), ui.MutedInk, TextStyles.Caption1);
        }
    }

    private void AskRemove(CasinoKnockerDto occupant)
    {
        var name = occupant.DisplayName.Length > 0 ? occupant.DisplayName : occupant.Handle;
        var targetRoom = roomId;
        var targetUser = occupant.UserId;
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Casino.DoorRemoveConfirmTitle, name),
            Message = Loc.T(L.Casino.DoorRemoveConfirmBody),
            ConfirmLabel = Loc.T(L.Casino.DoorRemove),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            Confirm = () => tables.Kick(targetRoom, targetUser),
        });
    }
}
