using Aetherphone.Core.GameChat;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal sealed class ChatRunSet
{
    public TextRun[] Runs = Array.Empty<TextRun>();
    public ChatChunk[] Targets = Array.Empty<ChatChunk>();
    public bool HasLinks;
}

internal static class ChatRuns
{
    private const int CacheLimit = 192;

    private static readonly Dictionary<string, ChatRunSet> Cache = new(StringComparer.Ordinal);
    private static readonly List<TextRun> RunScratch = new(16);
    private static readonly List<ChatChunk> TargetScratch = new(8);

    public static void Reset() => Cache.Clear();

    public static ChatRunSet For(ChatEntry entry)
    {
        if (Cache.TryGetValue(entry.Id, out var cached))
        {
            return cached;
        }

        if (Cache.Count > CacheLimit)
        {
            Cache.Clear();
        }

        var set = Build(entry);
        Cache[entry.Id] = set;
        return set;
    }

    public static Vector4 TintFor(ChatChunkKind kind) => kind switch
    {
        ChatChunkKind.Url => LinkTints.Url,
        ChatChunkKind.Item => LinkTints.Item,
        ChatChunkKind.Map => LinkTints.Map,
        ChatChunkKind.Player => LinkTints.Player,
        ChatChunkKind.Status => LinkTints.Status,
        ChatChunkKind.Quest => LinkTints.Quest,
        ChatChunkKind.PartyFinder => LinkTints.PartyFinder,
        _ => LinkTints.Plugin,
    };

    private static ChatRunSet Build(ChatEntry entry)
    {
        RunScratch.Clear();
        TargetScratch.Clear();
        var chunks = entry.Chunks;
        for (var index = 0; index < chunks.Length; index++)
        {
            var chunk = chunks[index];
            if (chunk.IsLink)
            {
                AddLink(chunk);
                continue;
            }

            SplitUrls(chunk.Text);
        }

        var set = new ChatRunSet
        {
            Runs = RunScratch.ToArray(),
            Targets = TargetScratch.ToArray(),
        };
        for (var index = 0; index < set.Runs.Length; index++)
        {
            if (set.Runs[index].Interactive)
            {
                set.HasLinks = true;
                break;
            }
        }

        return set;
    }

    private static void AddLink(ChatChunk chunk)
    {
        RunScratch.Add(TextRun.Link(chunk.Text, TintFor(chunk.Kind), TargetScratch.Count));
        TargetScratch.Add(chunk);
    }

    private static void SplitUrls(string text)
    {
        var cursor = 0;
        while (cursor < text.Length)
        {
            var start = FindUrl(text, cursor, out var length);
            if (start < 0)
            {
                RunScratch.Add(TextRun.Plain(text[cursor..]));
                return;
            }

            if (start > cursor)
            {
                RunScratch.Add(TextRun.Plain(text[cursor..start]));
            }

            AddLink(ChatChunk.Url(text.Substring(start, length)));
            cursor = start + length;
        }
    }

    private static int FindUrl(string text, int from, out int length)
    {
        length = 0;
        for (var index = from; index < text.Length; index++)
        {
            if (text[index] != 'h' && text[index] != 'w' && text[index] != 'H' && text[index] != 'W')
            {
                continue;
            }

            if (index > 0 && char.IsLetterOrDigit(text[index - 1]))
            {
                continue;
            }

            if (!Starts(text, index, "http://") && !Starts(text, index, "https://") && !Starts(text, index, "www."))
            {
                continue;
            }

            var end = index;
            while (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            while (end > index && IsTrailingPunctuation(text[end - 1]))
            {
                end--;
            }

            length = end - index;
            return length > 0 ? index : -1;
        }

        return -1;
    }

    private static bool Starts(string text, int at, string prefix)
    {
        if (at + prefix.Length > text.Length)
        {
            return false;
        }

        return text.AsSpan(at, prefix.Length).Equals(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrailingPunctuation(char value) =>
        value is '.' or ',' or ')' or ']' or '}' or '!' or '?' or ':' or ';' or '"' or '\'';
}
