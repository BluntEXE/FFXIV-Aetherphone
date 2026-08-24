using System.Net.Http.Headers;
using System.Net.WebSockets;
using Aetherphone.Core.Updates;

namespace Aetherphone.Core.Net;

internal sealed class AethernetClientIdentity
{
    public const string SourceHeader = "X-Aep-Source";
    public const string BuildHeader = "X-Aep-Build";
    public const string StatusHeader = "X-Aep-Source-Status";
    public const string StatusWarned = "warned";
    public const string StatusBlocked = "blocked";

    private readonly string host;
    private readonly Action<string> onSourceStatus;

    public AethernetClientIdentity(string baseUrl, Action<string> onSourceStatus)
    {
        host = Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;
        this.onSourceStatus = onSourceStatus;
    }

    public bool Matches(Uri? uri)
    {
        return host.Length > 0 && uri is not null && string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase);
    }

    public void Apply(HttpRequestHeaders headers)
    {
        if (InstallSource.Repository.Length > 0)
        {
            headers.TryAddWithoutValidation(SourceHeader, InstallSource.Repository);
        }

        headers.TryAddWithoutValidation(BuildHeader, InstallSource.Build);
    }

    public static void Apply(ClientWebSocketOptions options)
    {
        if (InstallSource.Repository.Length > 0)
        {
            options.SetRequestHeader(SourceHeader, InstallSource.Repository);
        }

        options.SetRequestHeader(BuildHeader, InstallSource.Build);
    }

    public void Observe(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(StatusHeader, out var values))
        {
            return;
        }

        foreach (var value in values)
        {
            onSourceStatus(value);
            return;
        }
    }
}

internal sealed class AethernetIdentityHandler : DelegatingHandler
{
    private readonly AethernetClientIdentity identity;

    public AethernetIdentityHandler(AethernetClientIdentity identity, HttpMessageHandler inner) : base(inner)
    {
        this.identity = identity;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var aethernet = identity.Matches(request.RequestUri);
        if (aethernet)
        {
            identity.Apply(request.Headers);
        }

        var response = await base.SendAsync(request, token).ConfigureAwait(false);
        if (aethernet)
        {
            identity.Observe(response);
        }

        return response;
    }
}
