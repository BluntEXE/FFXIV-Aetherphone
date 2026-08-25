using Aetherphone.Core.Net;

namespace Aetherphone.Core.Mods;

internal sealed class ModsHub : IDisposable
{
    public HeliosphereApi Api { get; }
    public HeliosphereBridge Bridge { get; }
    public ModsSearchStore Search { get; }
    public ModsDetailStore Details { get; }
    public ModsLibraryStore Library { get; }

    public ModsHub(HttpService http)
    {
        Api = new HeliosphereApi(http);
        Bridge = new HeliosphereBridge(http);
        Search = new ModsSearchStore(Api);
        Details = new ModsDetailStore(Api);
        Library = new ModsLibraryStore(Api);
    }

    public void Dispose()
    {
        Search.Dispose();
        Details.Dispose();
        Library.Dispose();
    }
}
