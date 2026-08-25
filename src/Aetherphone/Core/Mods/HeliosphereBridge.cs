using Aetherphone.Core.Net;

namespace Aetherphone.Core.Mods;

internal enum InstallOutcome : byte
{
    Sent,
    PluginNotRunning,
    Failed,
}

internal sealed class HeliosphereBridge
{
    private const string InstallPath = "/install";

    private readonly HttpService http;

    public HeliosphereBridge(HttpService http)
    {
        this.http = http;
    }

    public static bool IsPluginLoaded()
    {
        foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
        {
            if (string.Equals(plugin.InternalName, ModsContent.PluginInternalName, StringComparison.Ordinal)
                && plugin.IsLoaded)
            {
                return true;
            }
        }

        return false;
    }

    public static string InstalledPluginVersion()
    {
        foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
        {
            if (string.Equals(plugin.InternalName, ModsContent.PluginInternalName, StringComparison.Ordinal))
            {
                return plugin.Version.ToString();
            }
        }

        return string.Empty;
    }

    public async Task<InstallOutcome> InstallAsync(Guid packageId, Guid variantId, Guid versionId, string? password,
        CancellationToken token)
    {
        var request = new BridgeInstallRequest
        {
            PackageId = packageId,
            VariantId = variantId,
            VersionId = versionId,
            OneClickPassword = string.IsNullOrWhiteSpace(password) ? null : password.Trim(),
        };
        var status = 0;
        var accepted = await http.SendJsonForStatusAsync(HttpMethod.Post, ModsContent.BridgeUrl + InstallPath,
                request, HeliosphereJsonContext.Default.BridgeInstallRequest, null, token,
                code => status = code)
            .ConfigureAwait(false);
        if (accepted)
        {
            return InstallOutcome.Sent;
        }

        return status == 0 ? InstallOutcome.PluginNotRunning : InstallOutcome.Failed;
    }

}
