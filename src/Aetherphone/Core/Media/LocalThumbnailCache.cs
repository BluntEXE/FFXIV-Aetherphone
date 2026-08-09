using System.Collections.Concurrent;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Media;

internal sealed class LocalThumbnailCache : IDisposable
{
    private const long TextureBudgetBytes = 32L * 1024 * 1024;
    private const int ThumbnailMaxDimension = 256;
    private readonly TextureLedger ready = new(TextureBudgetBytes);
    private readonly ConcurrentDictionary<string, byte> loading = new();
    private readonly ConcurrentDictionary<string, byte> failed = new();
    private readonly CancellationTokenSource cancellation = new();

    public IDalamudTextureWrap? Get(string path)
    {
        if (ready.Get(path) is { } wrap)
        {
            return wrap;
        }

        if (failed.ContainsKey(path) || !loading.TryAdd(path, 0))
        {
            return null;
        }

        _ = LoadAsync(path);
        return null;
    }

    public bool Failed(string path) => failed.ContainsKey(path);

    private async Task LoadAsync(string path)
    {
        try
        {
            var wrap = await ImageProcessor.DecodeThumbnailToTextureAsync(Plugin.TextureProvider, path,
                ThumbnailMaxDimension, cancellation.Token).ConfigureAwait(false);
            if (!ready.TryAdd(path, wrap))
            {
                wrap.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            failed.TryAdd(path, 0);
            AepLog.Warning(exception, $"[Thumbs] failed to load {path}");
        }
        finally
        {
            loading.TryRemove(path, out _);
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        ready.DisposeAll();
        cancellation.Dispose();
    }
}
