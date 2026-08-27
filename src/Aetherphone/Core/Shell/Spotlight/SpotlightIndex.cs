using Aetherphone.Apps.AppStore;
using Aetherphone.Apps.Notes;
using Aetherphone.Apps.Settings;
using Aetherphone.Core.Apps;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Core.Telephony;

namespace Aetherphone.Core.Shell.Spotlight;

internal enum SpotlightKind : byte
{
    App,
    Contact,
    SettingsPage,
    Conversation,
    Note,
    MarketItem,
}

internal readonly struct SpotlightResult
{
    public readonly SpotlightKind Kind;
    public readonly string Title;
    public readonly string Subtitle;
    public readonly string Payload;
    public readonly uint ItemId;
    public readonly Guid NoteId;
    public readonly int PageIndex;

    public SpotlightResult(SpotlightKind kind, string title, string subtitle, string payload, uint itemId,
        Guid noteId, int pageIndex)
    {
        Kind = kind;
        Title = title;
        Subtitle = subtitle;
        Payload = payload;
        ItemId = itemId;
        NoteId = noteId;
        PageIndex = pageIndex;
    }
}

internal sealed class SpotlightIndex
{
    private const int MaxApps = 6;
    private const int MaxContacts = 5;
    private const int MaxSettings = 5;
    private const int MaxConversations = 4;
    private const int MaxNotes = 4;
    private const int MaxItems = 5;

    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly AppInstaller installer;
    private readonly ContactBook contacts;
    private readonly DmLauncher dmLauncher;
    private readonly ChatSearch chatSearch = new();
    private readonly ChatInbox chatInbox;
    private readonly ChatLog chatLog;
    private readonly LinkpearlLauncher linkpearlLauncher;
    private readonly MarketItemIndex marketIndex;
    private readonly MarketLauncher marketLauncher;
    private readonly Configuration configuration;
    private readonly SettingsApp? settingsApp;
    private readonly NotesApp? notesApp;
    private readonly List<SpotlightResult> results = new();
    private readonly List<MarketItemRef> marketScratch = new();
    private string lastQuery = string.Empty;

    public SpotlightIndex(IReadOnlyList<IPhoneApp> apps, AppInstaller installer, ContactBook contacts,
        DmLauncher dmLauncher, ChatInbox chatInbox, ChatLog chatLog, LinkpearlLauncher linkpearlLauncher,
        MarketItemIndex marketIndex, MarketLauncher marketLauncher, Configuration configuration)
    {
        this.apps = apps;
        this.installer = installer;
        this.contacts = contacts;
        this.dmLauncher = dmLauncher;
        this.chatInbox = chatInbox;
        this.chatLog = chatLog;
        this.linkpearlLauncher = linkpearlLauncher;
        this.marketIndex = marketIndex;
        this.marketLauncher = marketLauncher;
        this.configuration = configuration;
        for (var index = 0; index < apps.Count; index++)
        {
            settingsApp ??= apps[index] as SettingsApp;
            notesApp ??= apps[index] as NotesApp;
        }
    }

    public IReadOnlyList<SpotlightResult> Results => results;

    public void Clear()
    {
        results.Clear();
        lastQuery = string.Empty;
    }

    public void Search(string query)
    {
        if (string.Equals(query, lastQuery, StringComparison.Ordinal))
        {
            return;
        }

        lastQuery = query;
        results.Clear();
        var trimmed = query.Trim();
        if (trimmed.Length < 2)
        {
            return;
        }

        CollectApps(trimmed);
        CollectContacts(trimmed);
        CollectSettings(trimmed);
        CollectConversations(trimmed);
        CollectNotes(trimmed);
        CollectMarketItems(trimmed);
    }

    public void Activate(in SpotlightResult result, INavigator navigation)
    {
        switch (result.Kind)
        {
            case SpotlightKind.App:
                navigation.Open(result.Payload);
                break;
            case SpotlightKind.Contact:
                dmLauncher.RequestUser(result.Payload);
                navigation.Open("message");
                break;
            case SpotlightKind.SettingsPage:
                if (settingsApp is not null && result.PageIndex < settingsApp.SearchablePages.Count)
                {
                    settingsApp.RequestPage(settingsApp.SearchablePages[result.PageIndex]);
                }

                navigation.Open("settings");
                break;
            case SpotlightKind.Conversation:
                linkpearlLauncher.Request(result.Payload);
                navigation.Open("messages");
                break;
            case SpotlightKind.Note:
                notesApp?.RequestNote(result.NoteId);
                navigation.Open("notes");
                break;
            case SpotlightKind.MarketItem:
                marketLauncher.RequestItem(result.ItemId);
                navigation.Open("market");
                break;
        }
    }

    private void CollectApps(string query)
    {
        var added = 0;
        for (var index = 0; index < apps.Count && added < MaxApps; index++)
        {
            var app = apps[index];
            if (!installer.IsInstalled(app.Id) || !app.IsAvailable)
            {
                continue;
            }

            if (!AppStoreApp.Matches(app.Id, app.DisplayName, query))
            {
                continue;
            }

            var entry = AppStoreCatalog.For(app.Id);
            results.Add(new SpotlightResult(SpotlightKind.App, app.DisplayName, Loc.T(entry.Subtitle), app.Id,
                0, Guid.Empty, 0));
            added++;
        }
    }

    private void CollectContacts(string query)
    {
        var added = 0;
        var list = contacts.Contacts;
        for (var index = 0; index < list.Length && added < MaxContacts; index++)
        {
            var contact = list[index];
            if (!MatchesContact(contact, query))
            {
                continue;
            }

            results.Add(new SpotlightResult(SpotlightKind.Contact, ContactBook.DisplayLabel(contact),
                ContactBook.Format(contact.PhoneNumber), contact.UserId, 0, Guid.Empty, 0));
            added++;
        }
    }

    private static bool MatchesContact(Aethernet.Contracts.ContactDto contact, string query)
    {
        return contact.Alias.Contains(query, StringComparison.OrdinalIgnoreCase)
               || contact.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
               || contact.Handle.Contains(query, StringComparison.OrdinalIgnoreCase)
               || contact.PhoneNumber.Contains(query, StringComparison.Ordinal);
    }

    private void CollectSettings(string query)
    {
        if (settingsApp is null)
        {
            return;
        }

        var added = 0;
        var pages = settingsApp.SearchablePages;
        for (var index = 0; index < pages.Count && added < MaxSettings; index++)
        {
            var page = pages[index];
            if (!page.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            results.Add(new SpotlightResult(SpotlightKind.SettingsPage, page.Title, string.Empty, string.Empty,
                0, Guid.Empty, index));
            added++;
        }
    }

    private void CollectConversations(string query)
    {
        chatSearch.Run(query, chatInbox, chatLog);
        var hits = chatSearch.Hits;
        var added = 0;
        for (var index = 0; index < hits.Count && added < MaxConversations; index++)
        {
            var hit = hits[index];
            results.Add(new SpotlightResult(SpotlightKind.Conversation, hit.Title, hit.Entry.Text,
                hit.ConversationKey, 0, Guid.Empty, 0));
            added++;
        }
    }

    private void CollectNotes(string query)
    {
        var added = 0;
        var notes = configuration.Notes;
        for (var index = 0; index < notes.Count && added < MaxNotes; index++)
        {
            var note = notes[index];
            if (!note.Body.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            results.Add(new SpotlightResult(SpotlightKind.Note, note.Title(), note.Preview(), string.Empty,
                0, note.Id, 0));
            added++;
        }
    }

    private void CollectMarketItems(string query)
    {
        if (!marketIndex.Ready || !installer.IsInstalled("market"))
        {
            return;
        }

        marketScratch.Clear();
        marketIndex.Search(query, marketScratch, MaxItems);
        for (var index = 0; index < marketScratch.Count; index++)
        {
            results.Add(new SpotlightResult(SpotlightKind.MarketItem, marketScratch[index].Name, string.Empty,
                string.Empty, marketScratch[index].Id, Guid.Empty, 0));
        }
    }
}
