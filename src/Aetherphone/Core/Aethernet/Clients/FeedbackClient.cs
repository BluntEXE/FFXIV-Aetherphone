using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class FeedbackClient
{
    private readonly AethernetTransport net;

    public FeedbackClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<FeedbackDto?> CreateAsync(string text, string[] imageKeys, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/feedback", new CreateFeedbackRequest(text, imageKeys), AethernetJsonContext.Default.CreateFeedbackRequest, AethernetJsonContext.Default.FeedbackDto, token, null, onFailure);
    }
}
