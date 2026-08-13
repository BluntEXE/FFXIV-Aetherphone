using System.Text;
using Aetherphone.Core.Linkpearl;

namespace Aetherphone.Core.GameChat;

internal sealed class PendingSend
{
    public required string ChannelKey { get; init; }
    public required string Target { get; init; }
    public required string Text { get; init; }
    public required long SentAtMilliseconds { get; init; }
    public bool Failed { get; set; }
}

internal sealed class ChatSend
{
    public const int MaxBytes = ChatSender.MaxBytes;
    private const long ResolveWindowMilliseconds = 10000;
    private const int MaxFailed = 8;

    private readonly List<PendingSend> pending = new();

    public IReadOnlyList<PendingSend> Pending => pending;

    public static int Budget(GameChannel channel, string target)
    {
        if (!channel.CanSend)
        {
            return 0;
        }

        var prefix = Encoding.UTF8.GetByteCount(channel.Command) + 1;
        if (channel.NeedsTarget)
        {
            prefix += Encoding.UTF8.GetByteCount(target) + 1;
        }

        return Math.Max(0, MaxBytes - prefix);
    }

    public bool Send(GameChannel channel, string target, string text)
    {
        Tick();
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || !channel.CanSend)
        {
            return false;
        }

        if (channel.NeedsTarget && target.Length == 0)
        {
            return false;
        }

        var line = channel.NeedsTarget
            ? string.Concat(channel.Command, " ", target, " ", trimmed)
            : string.Concat(channel.Command, " ", trimmed);
        if (!ChatSender.TrySend(line))
        {
            return false;
        }

        pending.Add(new PendingSend
        {
            ChannelKey = channel.Key,
            Target = target,
            Text = trimmed,
            SentAtMilliseconds = Environment.TickCount64,
        });
        return true;
    }

    public bool TryResolve(string channelKey, string text)
    {
        Tick();
        for (var index = 0; index < pending.Count; index++)
        {
            var candidate = pending[index];
            if (!string.Equals(candidate.ChannelKey, channelKey, StringComparison.Ordinal) ||
                !string.Equals(candidate.Text, text, StringComparison.Ordinal))
            {
                continue;
            }

            pending.RemoveAt(index);
            return true;
        }

        return false;
    }

    public void Tick()
    {
        var now = Environment.TickCount64;
        var failed = 0;
        for (var index = pending.Count - 1; index >= 0; index--)
        {
            var candidate = pending[index];
            if (!candidate.Failed && now - candidate.SentAtMilliseconds >= ResolveWindowMilliseconds)
            {
                candidate.Failed = true;
            }

            if (!candidate.Failed)
            {
                continue;
            }

            failed++;
            if (failed > MaxFailed)
            {
                pending.RemoveAt(index);
            }
        }
    }

    public void Discard(PendingSend entry) => pending.Remove(entry);

    public void Clear(string channelKey)
    {
        for (var index = pending.Count - 1; index >= 0; index--)
        {
            if (string.Equals(pending[index].ChannelKey, channelKey, StringComparison.Ordinal))
            {
                pending.RemoveAt(index);
            }
        }
    }
}
