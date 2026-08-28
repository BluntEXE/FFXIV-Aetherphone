using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const float PeopleSearchHeight = 44f;
    private const float PeopleScopeHeight = 36f;

    private readonly string[] peopleScopeLabels = new string[2];
    private string peopleSearch = string.Empty;
    private int peopleScope;

    private void ResetPeopleState()
    {
        peopleSearch = string.Empty;
        peopleScope = 0;
        ResetFindState();
    }

    private void DrawPeopleTab(Rect content)
    {
        var scale = UiScale.Current;
        var pad = Metrics.Space.Lg * scale;
        var searchBar = new Rect(new Vector2(content.Min.X + pad, content.Min.Y),
            new Vector2(content.Max.X - pad, content.Min.Y + PeopleSearchHeight * scale));
        UiAnchors.Report("people.search", searchBar);
        if (peopleFocusPending)
        {
            ImGui.SetKeyboardFocusHere();
            peopleFocusPending = false;
        }

        if (SearchField.DrawSubmit(searchBar, "##peopleSearch", Loc.T(L.Common.Search), ref peopleSearch, frameTheme)
            && peopleScope == 1)
        {
            SubmitPeopleSearch();
        }

        var scopeTop = searchBar.Max.Y + Metrics.Space.Sm * scale;
        var scopeRow = new Rect(new Vector2(content.Min.X + pad, scopeTop),
            new Vector2(content.Max.X - pad, scopeTop + PeopleScopeHeight * scale));
        UiAnchors.Report("people.scope", scopeRow);
        peopleScopeLabels[0] = Loc.T(L.Linkpearl.ScopeFriends);
        peopleScopeLabels[1] = Loc.T(L.Linkpearl.ScopeEveryone);
        var scope = SegmentStrip.Draw("people.scope", scopeRow, peopleScopeLabels, peopleScope, frameTheme);
        if (scope != peopleScope)
        {
            peopleScope = scope;
            if (peopleScope == 1 && peopleSearch.Trim().Length > 0)
            {
                SubmitPeopleSearch();
            }
        }

        var body = new Rect(new Vector2(content.Min.X, scopeRow.Max.Y + Metrics.Space.Xs * scale), content.Max);
        if (peopleScope == 0)
        {
            DrawFriendsScope(body);
            return;
        }

        DrawEveryoneScope(body, pad, scale);
    }

    private void DrawFriendsScope(Rect body)
    {
        UiAnchors.Report("people.list", body);
        if (friends.Count == 0)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Contacts.Empty), frameTheme.TextMuted);
            return;
        }

        using (AppSurface.BeginEdgeToEdge(body))
        {
            DrawFriendSection(Loc.T(L.Contacts.Online), true);
            DrawFriendSection(Loc.T(L.Contacts.Offline), false);
            if (!AnyFriendMatches())
            {
                Typography.DrawCentered(
                    new Vector2(body.Center.X, body.Min.Y + 60f * UiScale.Current),
                    Loc.T(L.Linkpearl.NoMatches), frameTheme.TextMuted);
            }
        }
    }

    private void DrawEveryoneScope(Rect body, float pad, float scale)
    {
        var kindRow = new Rect(new Vector2(body.Min.X + pad, body.Min.Y),
            new Vector2(body.Max.X - pad, body.Min.Y + FindSegmentRowHeight * scale));
        UiAnchors.Report("findpeople.kind", kindRow);
        findSegmentLabels[0] = Loc.T(L.FindPeople.Character);
        findSegmentLabels[1] = Loc.T(L.FindPeople.FreeCompany);
        var selected = SegmentStrip.Draw("findpeople.kind", kindRow, findSegmentLabels, (int)findKind, frameTheme);
        if (selected != (int)findKind)
        {
            findKind = (LookupKind)selected;
            if (hasQuery)
            {
                SubmitPeopleSearch();
            }
        }

        var worldTop = kindRow.Max.Y + Metrics.Space.Sm * scale;
        var worldBar = new Rect(new Vector2(body.Min.X + pad, worldTop),
            new Vector2(body.Max.X - pad, worldTop + FindFieldRowHeight * scale));
        UiAnchors.Report("findpeople.name", worldBar);
        if (SubmitField.Draw(worldBar, "##peopleWorldField", Loc.T(L.FindPeople.WorldHint), ref findWorldInput,
                frameTheme))
        {
            SubmitPeopleSearch();
        }

        var results = new Rect(new Vector2(body.Min.X, worldBar.Max.Y + Metrics.Space.Xs * scale), body.Max);
        if (!hasQuery)
        {
            DrawFindPrompt(results, frameTheme, scale);
            return;
        }

        if (findKind == LookupKind.Character)
        {
            DrawCharacterResults(results, frameTheme, scale);
        }
        else
        {
            DrawFreeCompanyResults(results, frameTheme, scale);
        }
    }

    private void SubmitPeopleSearch()
    {
        findNameInput = peopleSearch.Trim();
        SubmitSearch();
    }

    private bool AnyFriendMatches()
    {
        for (var index = 0; index < friends.Count; index++)
        {
            if (MatchesContact(friends[index]))
            {
                return true;
            }
        }

        return false;
    }
}
