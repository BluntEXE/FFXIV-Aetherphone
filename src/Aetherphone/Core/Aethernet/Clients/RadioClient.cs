using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class RadioClient
{
    private readonly AethernetTransport net;

    public RadioClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<RadioStationPage?> StationsAsync(CancellationToken token)
    {
        return net.GetAsync("/radio/stations", AethernetJsonContext.Default.RadioStationPage, token);
    }

    public Task<RadioStationDto?> StationAsync(string stationId, CancellationToken token)
    {
        return net.GetAsync($"/radio/stations/{Uri.EscapeDataString(stationId)}",
            AethernetJsonContext.Default.RadioStationDto, token);
    }

    public Task<MyRadioStationDto?> MineAsync(CancellationToken token)
    {
        return net.GetAsync("/radio/mine", AethernetJsonContext.Default.MyRadioStationDto, token);
    }

    public Task<MyRadioStationDto?> UpdateMineAsync(UpdateRadioStationRequest request, CancellationToken token,
        Action<int>? statusSink = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, "/radio/mine", request,
            AethernetJsonContext.Default.UpdateRadioStationRequest, AethernetJsonContext.Default.MyRadioStationDto,
            token, statusSink);
    }
}
