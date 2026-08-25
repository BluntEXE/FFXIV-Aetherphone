using System.Collections.Concurrent;

namespace Aetherphone.Core.Mods;

internal sealed class ModsDetailStore : IDisposable
{
    private readonly HeliosphereApi api;
    private readonly CancellationTokenSource lifetime = new();
    private readonly ConcurrentDictionary<Guid, PackageDto> packages = new();
    private readonly ConcurrentDictionary<Guid, byte> loading = new();
    private readonly ConcurrentDictionary<Guid, byte> failed = new();

    public ModsDetailStore(HeliosphereApi api)
    {
        this.api = api;
    }

    public PackageDto? Get(Guid packageId) => packages.TryGetValue(packageId, out var package) ? package : null;

    public bool IsLoading(Guid packageId) => loading.ContainsKey(packageId);

    public bool HasFailed(Guid packageId) => failed.ContainsKey(packageId);

    public void Request(Guid packageId, bool force = false)
    {
        if (!force && (packages.ContainsKey(packageId) || failed.ContainsKey(packageId)))
        {
            return;
        }

        if (!loading.TryAdd(packageId, 0))
        {
            return;
        }

        failed.TryRemove(packageId, out _);
        _ = FetchAsync(packageId, lifetime.Token);
    }

    private async Task FetchAsync(Guid packageId, CancellationToken token)
    {
        try
        {
            var package = await Task.Run(() => api.PackageAsync(packageId, token), token).ConfigureAwait(false);
            if (package is null)
            {
                failed.TryAdd(packageId, 0);
                return;
            }

            SortVariants(package);
            packages[packageId] = package;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Mods] package fetch failed");
            failed.TryAdd(packageId, 0);
        }
        finally
        {
            loading.TryRemove(packageId, out _);
        }
    }

    private static void SortVariants(PackageDto package)
    {
        var variants = package.Variants;
        for (var outer = 1; outer < variants.Length; outer++)
        {
            var current = variants[outer];
            var inner = outer - 1;
            while (inner >= 0 && variants[inner].DisplayOrder > current.DisplayOrder)
            {
                variants[inner + 1] = variants[inner];
                inner--;
            }

            variants[inner + 1] = current;
        }

        var images = package.Images;
        for (var outer = 1; outer < images.Length; outer++)
        {
            var current = images[outer];
            var inner = outer - 1;
            while (inner >= 0 && images[inner].DisplayOrder > current.DisplayOrder)
            {
                images[inner + 1] = images[inner];
                inner--;
            }

            images[inner + 1] = current;
        }
    }

    public void Dispose()
    {
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
