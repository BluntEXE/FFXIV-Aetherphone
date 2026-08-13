using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;

namespace Aetherphone.Core.GameChat;

internal sealed class InboxRow
{
    public required string Key { get; init; }
    public ChatTab? Tab { get; set; }
    public string StreamKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public string PreviewSender { get; set; } = string.Empty;
    public string PreviewText { get; set; } = string.Empty;
    public string PreviewChannel { get; set; } = string.Empty;
    public DateTime LastActivity { get; set; }
    public int Unread { get; set; }
    public Vector4 Tint { get; set; }

    public bool IsTell => Tab is null;
}

internal sealed class ChatInbox : IDisposable
{
    private static readonly Comparison<InboxRow> ByActivity = static (left, right) =>
        right.LastActivity.CompareTo(left.LastActivity);

    private readonly ChatLog log;
    private readonly TabStore tabs;
    private readonly Configuration configuration;
    private readonly List<InboxRow> rows = new(16);
    private readonly List<InboxRow> pinned = new(6);
    private readonly List<string> streamScratch = new(32);
    private InboxRow? transient;
    private long expectedRevision = -1;
    private bool stale = true;
    private bool seenDirty;

    public ChatInbox(ChatLog log, TabStore tabs, Configuration configuration)
    {
        this.log = log;
        this.tabs = tabs;
        this.configuration = configuration;
        log.Appended += OnAppended;
        tabs.Changed += Invalidate;
    }

    public IReadOnlyList<InboxRow> Rows => rows;

    public IReadOnlyList<InboxRow> Pinned => pinned;

    public int TotalUnread { get; private set; }

    public string Viewing { get; set; } = string.Empty;

    public void Invalidate() => stale = true;

    public InboxRow EnsureTell(string display, string world)
    {
        var target = world.Length > 0 ? string.Concat(display, "@", world) : display;
        var key = ChatStreams.ForTell(target);
        if (Find(key) is { } existing)
        {
            return existing;
        }

        transient = new InboxRow
        {
            Key = key,
            StreamKey = key,
            Title = display,
            World = world,
            Tint = ChannelTints.Tell,
        };
        rows.Insert(0, transient);
        return transient;
    }

    public void ClearTransient()
    {
        if (transient is null)
        {
            return;
        }

        transient = null;
        stale = true;
    }

    public void Sync()
    {
        if (!stale && expectedRevision == log.Revision)
        {
            return;
        }

        Rebuild();
    }

    public InboxRow? Find(string key)
    {
        for (var index = 0; index < pinned.Count; index++)
        {
            if (string.Equals(pinned[index].Key, key, StringComparison.Ordinal))
            {
                return pinned[index];
            }
        }

        for (var index = 0; index < rows.Count; index++)
        {
            if (string.Equals(rows[index].Key, key, StringComparison.Ordinal))
            {
                return rows[index];
            }
        }

        return null;
    }

    public void MarkRead(InboxRow row)
    {
        Stamp(row.Key);
        if (row.Unread == 0)
        {
            return;
        }

        TotalUnread -= row.Unread;
        row.Unread = 0;
        if (TotalUnread < 0)
        {
            TotalUnread = 0;
        }
    }

    public void FlushSeen()
    {
        if (!seenDirty)
        {
            return;
        }

        seenDirty = false;
        configuration.Save();
    }

    public void Dispose()
    {
        log.Appended -= OnAppended;
        tabs.Changed -= Invalidate;
    }

    public static string KeyForTab(ChatTab tab) => string.Concat("tab:", tab.Id);

    private void OnAppended(ChatEntry entry)
    {
        expectedRevision = log.Revision;
        if (stale)
        {
            return;
        }

        if (ChatStreams.IsTell(entry.StreamKey))
        {
            var row = Find(entry.StreamKey);
            if (row is null)
            {
                stale = true;
                return;
            }

            Touch(row, entry, !entry.IsSelf);
            Resort();
            return;
        }

        var touched = false;
        for (var index = 0; index < rows.Count; index++)
        {
            touched |= TouchTab(rows[index], entry);
        }

        for (var index = 0; index < pinned.Count; index++)
        {
            touched |= TouchTab(pinned[index], entry);
        }

        if (touched)
        {
            Resort();
        }
    }

    private bool TouchTab(InboxRow row, ChatEntry entry)
    {
        if (row.Tab is not { } tab || !tab.Includes(entry.ChannelKey))
        {
            return false;
        }

        Touch(row, entry, Counts(tab, entry));
        return true;
    }

    private void Touch(InboxRow row, ChatEntry entry, bool counts)
    {
        row.LastActivity = entry.At;
        row.PreviewSender = entry.IsSelf ? string.Empty : entry.AuthorName;
        row.PreviewText = entry.Text;
        row.PreviewChannel = row.Tab is { Channels.Count: > 1 } ? entry.ChannelKey : string.Empty;
        if (string.Equals(row.Key, Viewing, StringComparison.Ordinal))
        {
            Stamp(row.Key);
            return;
        }

        if (!counts)
        {
            return;
        }

        row.Unread++;
        TotalUnread++;
    }

    private void Rebuild()
    {
        rows.Clear();
        pinned.Clear();
        TotalUnread = 0;
        var all = tabs.Tabs;
        for (var index = 0; index < all.Count; index++)
        {
            var row = BuildTab(all[index]);
            if (all[index].Pinned)
            {
                pinned.Add(row);
            }
            else
            {
                rows.Add(row);
            }
        }

        log.CollectStreams(streamScratch);
        for (var index = 0; index < streamScratch.Count; index++)
        {
            var streamKey = streamScratch[index];
            if (ChatStreams.IsTell(streamKey))
            {
                rows.Add(BuildTell(streamKey));
            }
        }

        if (transient is not null)
        {
            if (log.HasLines(transient.Key))
            {
                transient = null;
            }
            else
            {
                rows.Add(transient);
            }
        }

        Resort();
        expectedRevision = log.Revision;
        stale = false;
    }

    private InboxRow BuildTab(ChatTab tab)
    {
        var palette = ChannelTints.TabPalette;
        var row = new InboxRow
        {
            Key = KeyForTab(tab),
            Tab = tab,
            Title = tab.Name,
            Tint = palette[Math.Clamp(tab.Tint, 0, palette.Length - 1)],
        };
        var watermark = Watermark(row.Key);
        var multi = tab.Channels.Count > 1;
        for (var index = 0; index < tab.Channels.Count; index++)
        {
            var lines = log.Lines(tab.Channels[index]);
            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var entry = lines[lineIndex];
                if (entry.At > row.LastActivity)
                {
                    row.LastActivity = entry.At;
                    row.PreviewSender = entry.IsSelf ? string.Empty : entry.AuthorName;
                    row.PreviewText = entry.Text;
                    row.PreviewChannel = multi ? entry.ChannelKey : string.Empty;
                }

                if (UnixMilliseconds(entry.At) > watermark && Counts(tab, entry))
                {
                    row.Unread++;
                }
            }
        }

        TotalUnread += row.Unread;
        return row;
    }

    private InboxRow BuildTell(string streamKey)
    {
        var lines = log.Lines(streamKey);
        var row = new InboxRow
        {
            Key = streamKey,
            StreamKey = streamKey,
            Tint = ChannelTints.Tell,
        };
        var watermark = Watermark(streamKey);
        for (var index = 0; index < lines.Count; index++)
        {
            var entry = lines[index];
            row.Title = entry.AuthorName;
            row.World = entry.AuthorWorld;
            if (entry.At > row.LastActivity)
            {
                row.LastActivity = entry.At;
                row.PreviewSender = string.Empty;
                row.PreviewText = entry.Text;
            }

            if (!entry.IsSelf && UnixMilliseconds(entry.At) > watermark)
            {
                row.Unread++;
            }
        }

        TotalUnread += row.Unread;
        return row;
    }

    private void Resort() => rows.Sort(ByActivity);

    private long Watermark(string key) =>
        configuration.LinkpearlSeen.TryGetValue(key, out var value) ? value : 0L;

    private void Stamp(string key)
    {
        configuration.LinkpearlSeen[key] = Now();
        seenDirty = true;
    }

    private static bool Counts(ChatTab tab, ChatEntry entry)
    {
        if (entry.IsSelf || tab.Alerts == AlertPolicy.Off || tab.IsMuted(entry.ChannelKey))
        {
            return false;
        }

        return tab.Alerts == AlertPolicy.All || entry.IsMention;
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static long UnixMilliseconds(DateTime at) => new DateTimeOffset(at).ToUnixTimeMilliseconds();
}
