using System.Collections.Concurrent;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Media;

internal sealed class TextureLedger
{
    private sealed class Entry : IDisposable
    {
        public readonly IDalamudTextureWrap? Wrap;
        public readonly AnimatedImage? Animation;
        public readonly long Bytes;
        public long LastAccessTicks;

        public Entry(IDalamudTextureWrap wrap)
        {
            Wrap = wrap;
            Bytes = (long)wrap.Width * wrap.Height * 4;
            LastAccessTicks = Environment.TickCount64;
        }

        public Entry(AnimatedImage animation)
        {
            Animation = animation;
            Bytes = animation.Bytes;
            LastAccessTicks = Environment.TickCount64;
        }

        public void Dispose()
        {
            Wrap?.Dispose();
            Animation?.Dispose();
        }
    }

    private static readonly TimeSpan EvictionIdleFloor = TimeSpan.FromSeconds(5);
    private readonly ConcurrentDictionary<string, Entry> entries = new();
    private readonly long budgetBytes;
    private long totalBytes;

    public TextureLedger(long budgetBytes)
    {
        this.budgetBytes = budgetBytes;
    }

    public IDalamudTextureWrap? Get(string key)
    {
        if (!entries.TryGetValue(key, out var entry))
        {
            return null;
        }

        Volatile.Write(ref entry.LastAccessTicks, Environment.TickCount64);
        return entry.Wrap ?? entry.Animation!.Frames[0];
    }

    public AnimatedImage? GetAnimated(string key)
    {
        if (!entries.TryGetValue(key, out var entry) || entry.Animation is null)
        {
            return null;
        }

        Volatile.Write(ref entry.LastAccessTicks, Environment.TickCount64);
        return entry.Animation;
    }

    public Vector2 SizeOf(string key)
    {
        if (!entries.TryGetValue(key, out var entry))
        {
            return Vector2.Zero;
        }

        return entry.Wrap?.Size ?? entry.Animation!.Frames[0].Size;
    }

    public bool TryAdd(string key, IDalamudTextureWrap wrap)
    {
        return TryAddEntry(key, new Entry(wrap));
    }

    public bool TryAddAnimated(string key, AnimatedImage animation)
    {
        return TryAddEntry(key, new Entry(animation));
    }

    private bool TryAddEntry(string key, Entry entry)
    {
        if (!entries.TryAdd(key, entry))
        {
            return false;
        }

        Interlocked.Add(ref totalBytes, entry.Bytes);
        EvictPastBudget();
        return true;
    }

    public bool TryRemove(string key, out IDisposable disposable)
    {
        if (entries.TryRemove(key, out var entry))
        {
            Interlocked.Add(ref totalBytes, -entry.Bytes);
            disposable = entry;
            return true;
        }

        disposable = null!;
        return false;
    }

    public void RemoveAndDispose(string key)
    {
        if (entries.TryRemove(key, out var entry))
        {
            Interlocked.Add(ref totalBytes, -entry.Bytes);
            entry.Dispose();
        }
    }

    public void DisposeAll()
    {
        foreach (var key in entries.Keys)
        {
            if (entries.TryRemove(key, out var entry))
            {
                Interlocked.Add(ref totalBytes, -entry.Bytes);
                entry.Dispose();
            }
        }
    }

    private void EvictPastBudget()
    {
        if (Interlocked.Read(ref totalBytes) <= budgetBytes)
        {
            return;
        }

        var now = Environment.TickCount64;
        var idleFloorMs = (long)EvictionIdleFloor.TotalMilliseconds;
        var candidates = new List<KeyValuePair<string, Entry>>();
        foreach (var pair in entries)
        {
            if (now - Volatile.Read(ref pair.Value.LastAccessTicks) >= idleFloorMs)
            {
                candidates.Add(pair);
            }
        }

        candidates.Sort(static (left, right) =>
            Volatile.Read(ref left.Value.LastAccessTicks).CompareTo(Volatile.Read(ref right.Value.LastAccessTicks)));

        for (var index = 0; index < candidates.Count; index++)
        {
            if (Interlocked.Read(ref totalBytes) <= budgetBytes)
            {
                return;
            }

            if (entries.TryRemove(candidates[index].Key, out var removed))
            {
                Interlocked.Add(ref totalBytes, -removed.Bytes);
                _ = Plugin.Framework.RunOnFrameworkThread(removed.Dispose);
            }
        }
    }
}
