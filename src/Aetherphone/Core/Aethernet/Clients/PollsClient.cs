using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class PollsClient
{
    private readonly AethernetTransport net;

    public PollsClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<PollPage?> ListAsync(string? cursor, CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        var path = "/polls";
        if (cursor is not null)
        {
            path += $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        return net.GetAsync(path, AethernetJsonContext.Default.PollPage, token, null, onFailure);
    }

    public Task<PollDto?> VoteAsync(string pollId, int option, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, $"/polls/{pollId}/vote", new PollVoteRequest(option), AethernetJsonContext.Default.PollVoteRequest, AethernetJsonContext.Default.PollDto, token, null, onFailure);
    }

    public Task<PollDto?> ClearVoteAsync(string pollId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.RequestAsync(HttpMethod.Delete, $"/polls/{pollId}/vote", AethernetJsonContext.Default.PollDto, token, null, onFailure);
    }
}
