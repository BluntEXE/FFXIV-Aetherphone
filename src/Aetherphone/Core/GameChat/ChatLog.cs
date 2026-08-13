using Aetherphone.Core.Game;

namespace Aetherphone.Core.GameChat;

internal sealed class ChatLog
{
    public const int MaxLinesPerChannel = 2000;
    private const int TrimBlock = 128;

    private static readonly ChatEntry[] NoLines = Array.Empty<ChatEntry>();

    private readonly Dictionary<string, List<ChatEntry>> byChannel = new(StringComparer.Ordinal);

    public ChatLog(CharacterWatch characterWatch)
    {
        characterWatch.Changed += _ => Clear();
    }

    public long Revision { get; private set; }

    public event Action<ChatEntry>? Appended;

    public void Append(ChatEntry entry)
    {
        if (!byChannel.TryGetValue(entry.ChannelKey, out var lines))
        {
            lines = new List<ChatEntry>(64);
            byChannel[entry.ChannelKey] = lines;
        }

        lines.Add(entry);
        if (lines.Count > MaxLinesPerChannel + TrimBlock)
        {
            lines.RemoveRange(0, lines.Count - MaxLinesPerChannel);
        }

        Revision++;
        Appended?.Invoke(entry);
    }

    public IReadOnlyList<ChatEntry> Lines(string channelKey) =>
        byChannel.TryGetValue(channelKey, out var lines) ? lines : NoLines;

    public bool HasLines(string channelKey) => byChannel.TryGetValue(channelKey, out var lines) && lines.Count > 0;

    public void Clear()
    {
        if (byChannel.Count == 0)
        {
            return;
        }

        byChannel.Clear();
        Revision++;
    }

    public void Clear(string channelKey)
    {
        if (!byChannel.Remove(channelKey))
        {
            return;
        }

        Revision++;
    }
}
