using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const int StationNameMaxLength = 40;
    private const int StationDescriptionMaxLength = 300;
    private const int StationLinkMaxLength = 200;
    private const float FieldHeight = 44f;
    private const float DescriptionHeight = 92f;
    private const float CredentialRowHeight = 34f;
    private const float CopiedNoticeSeconds = 1.6f;

    private readonly string[] linkDrafts = new string[LinkLabels.Length];
    private string stationNameDraft = string.Empty;
    private string stationDescriptionDraft = string.Empty;
    private bool stationDraftLoaded;
    private volatile bool stationSaving;
    private volatile bool stationSaveFailed;
    private volatile bool stationSaveDone;
    private float stationCopiedClock = float.NegativeInfinity;
    private int stationCopiedRow = -1;

    private void OpenMyStation()
    {
        LoadStationDrafts();
        router.Push(View.MyStation);
    }

    private void LoadStationDrafts()
    {
        if (community.Mine is not { } mine)
        {
            return;
        }

        stationNameDraft = mine.Station.Name;
        stationDescriptionDraft = mine.Station.Description;
        for (var index = 0; index < linkDrafts.Length; index++)
        {
            linkDrafts[index] = string.Empty;
        }

        for (var index = 0; index < mine.Station.Links.Length; index++)
        {
            var link = mine.Station.Links[index];
            if (link.Kind >= 0 && link.Kind < linkDrafts.Length)
            {
                linkDrafts[link.Kind] = link.Url;
            }
        }

        stationDraftLoaded = true;
        stationSaveFailed = false;
        stationSaveDone = false;
    }

    private void DrawMyStation(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var content = context.Content;
        DrawTopBar(context, Loc.T(L.Music.MyStation), PopToCommunity);
        if (community.Mine is not { } mine)
        {
            return;
        }

        if (!stationDraftLoaded)
        {
            LoadStationDrafts();
        }

        var body = ScrollBody(content, scale);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            DrawStationStatusLine(scale, mine.Station);
            DrawFieldLabel(scale, Loc.T(L.Music.StationNameLabel));
            DrawStationField(scale, "##stationName", ref stationNameDraft, StationNameMaxLength);
            DrawFieldLabel(scale, Loc.T(L.Music.StationDescriptionLabel));
            DrawStationDescription(scale);
            DrawFieldLabel(scale, Loc.T(L.Music.StationLinksLabel));
            for (var index = 0; index < linkDrafts.Length; index++)
            {
                DrawLinkField(scale, index);
            }

            DrawStationSaveRow(scale, mine.Station);
            DrawCredentials(scale, mine.Credentials);
            ImGui.Dummy(new Vector2(0f, 14f * scale));
        }
    }

    private void DrawStationStatusLine(float scale, CommunityStationDto station)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var status = station.IsLive
            ? $"{Loc.T(L.Music.OnAir)} · {string.Format(Loc.T(L.Music.ListeningCount), station.Listeners)}"
            : Loc.T(L.Music.OffAir);
        var fitted = Typography.FitText(status, width - 32f * scale, TextStyles.Callout);
        Typography.Draw(drawList, new Vector2(origin.X + 16f * scale, origin.Y), fitted,
            station.IsLive ? ui.Accent : ui.MutedInk, TextStyles.Callout);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 26f * scale));
    }

    private void DrawFieldLabel(float scale, string label)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + 16f * scale, origin.Y + 8f * scale), label,
            ui.MutedInk, TextStyles.Caption1);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 28f * scale));
    }

    private void DrawStationField(float scale, string id, ref string draft, int maxLength)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var fieldMin = new Vector2(origin.X + 16f * scale, origin.Y);
        var fieldRect = new Rect(fieldMin, new Vector2(origin.X + width - 16f * scale,
            origin.Y + FieldHeight * scale));
        SubmitField.Draw(fieldRect, id, string.Empty, ref draft, theme, maxLength);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (FieldHeight + 8f) * scale));
    }

    private void DrawLinkField(float scale, int kind)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var labelWidth = 74f * scale;
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(origin.X + 16f * scale, origin.Y + 13f * scale), LinkLabels[kind], ui.BodyInk,
            TextStyles.Caption1);
        var fieldMin = new Vector2(origin.X + 16f * scale + labelWidth, origin.Y);
        var fieldRect = new Rect(fieldMin, new Vector2(origin.X + width - 16f * scale,
            origin.Y + FieldHeight * scale));
        var draft = linkDrafts[kind];
        SubmitField.Draw(fieldRect, "##stationLink" + kind, string.Empty, ref draft, theme, StationLinkMaxLength);
        linkDrafts[kind] = draft;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (FieldHeight + 6f) * scale));
    }

    private void DrawStationDescription(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var fieldWidth = width - 32f * scale;
        ImGui.SetCursorScreenPos(new Vector2(origin.X + 16f * scale, origin.Y));
        SoftWrapField.Multiline("##stationDescription", ref stationDescriptionDraft, StationDescriptionMaxLength,
            new Vector2(fieldWidth, DescriptionHeight * scale), fieldWidth - 16f * scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (DescriptionHeight + 10f) * scale));
    }

    private void DrawStationSaveRow(float scale, CommunityStationDto station)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var buttonWidth = MathF.Min(width - 32f * scale, 220f * scale);
        var buttonMin = new Vector2(origin.X + (width - buttonWidth) * 0.5f, origin.Y + 6f * scale);
        var buttonRect = new Rect(buttonMin, buttonMin + new Vector2(buttonWidth, 40f * scale));
        var label = stationSaving ? Loc.T(L.Common.Loading) : Loc.T(L.Music.StationSave);
        if (ui.PillButton(buttonRect, label, true, "music.station.save") && !stationSaving)
        {
            SaveStation(station);
        }

        var noticeY = buttonRect.Max.Y + 6f * scale;
        if (stationSaveDone || stationSaveFailed)
        {
            var notice = stationSaveFailed ? Loc.T(L.Music.StationSaveFailed) : Loc.T(L.Music.StationSaved);
            var fitted = Typography.FitText(notice, width - 32f * scale, TextStyles.Caption1);
            var noticeSize = Typography.Measure(fitted, TextStyles.Caption1);
            Typography.Draw(ImGui.GetWindowDrawList(),
                new Vector2(origin.X + (width - noticeSize.X) * 0.5f, noticeY), fitted,
                stationSaveFailed ? theme.Danger : ui.MutedInk, TextStyles.Caption1);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 74f * scale));
    }

    private void SaveStation(CommunityStationDto station)
    {
        var links = new List<CommunityLinkDto>(linkDrafts.Length);
        for (var index = 0; index < linkDrafts.Length; index++)
        {
            var url = linkDrafts[index].Trim();
            if (url.Length > 0)
            {
                links.Add(new CommunityLinkDto(index, url));
            }
        }

        var request = new UpdateCommunityStationRequest(stationNameDraft.Trim(), stationDescriptionDraft.Trim(),
            station.Tags, links.ToArray(), null);
        stationSaving = true;
        stationSaveFailed = false;
        stationSaveDone = false;
        _ = SaveStationAsync(request);
    }

    private async Task SaveStationAsync(UpdateCommunityStationRequest request)
    {
        var ok = await community.SaveMineAsync(request).ConfigureAwait(false);
        stationSaveFailed = !ok;
        stationSaveDone = ok;
        stationSaving = false;
        if (ok)
        {
            LoadStationDrafts();
        }
    }

    private void DrawCredentials(float scale, CommunityCredentialsDto credentials)
    {
        DrawFieldLabel(scale, Loc.T(L.Music.StationBroadcast));
        DrawCredentialRow(scale, 0, Loc.T(L.Music.StationServer), credentials.Host);
        DrawCredentialRow(scale, 1, Loc.T(L.Music.StationPort), credentials.Port.ToString());
        DrawCredentialRow(scale, 2, Loc.T(L.Music.StationMount), credentials.Mount);
        DrawCredentialRow(scale, 3, Loc.T(L.Music.StationUser), credentials.Username);
        DrawCredentialRow(scale, 4, Loc.T(L.Music.StationPassword), credentials.Password);
        DrawCredentialRow(scale, 5, Loc.T(L.Music.StationFormat),
            $"{credentials.Format} · {credentials.Bitrate}kbps · {credentials.SampleRate}Hz");

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var wrapWidth = width - 32f * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + 16f * scale, origin.Y + 8f * scale),
            Loc.T(L.Music.StationHelp), ui.MutedInk, TextStyles.Caption1, wrapWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 16f * scale));
    }

    private void DrawCredentialRow(float scale, int row, string label, string value)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = CredentialRowHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        var labelLeft = origin.X + 16f * scale;
        Typography.Draw(drawList, new Vector2(labelLeft, origin.Y + 9f * scale), label, ui.MutedInk,
            TextStyles.Caption1);

        var copyRadius = 13f * scale;
        var copyCenter = new Vector2(origin.X + width - 16f * scale - copyRadius, origin.Y + rowHeight * 0.5f);
        var valueLeft = labelLeft + 80f * scale;
        var valueWidth = copyCenter.X - copyRadius - 8f * scale - valueLeft;
        var justCopied = stationCopiedRow == row && clock - stationCopiedClock < CopiedNoticeSeconds;
        var shown = justCopied ? Loc.T(L.Music.StationCopied) : value;
        var fitted = Typography.FitText(shown, valueWidth, TextStyles.Callout);
        Typography.Draw(drawList, new Vector2(valueLeft, origin.Y + 7f * scale), fitted,
            justCopied ? ui.Accent : ui.TitleInk, TextStyles.Callout);

        if (value.Length > 0 && ui.IconButton(copyCenter, copyRadius, FontAwesomeIcon.Copy.ToIconString(),
                ui.MutedInk, AppSkin.Transparent, 0.75f, label))
        {
            ImGui.SetClipboardText(value);
            stationCopiedClock = clock;
            stationCopiedRow = row;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }
}
