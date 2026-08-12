using System.Text.Json.Serialization;

namespace Aetherphone.Core.Telephony.Contracts;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CallControl))]
[JsonSerializable(typeof(ParticipantInfo))]
[JsonSerializable(typeof(NearbyStreamInfo))]
[JsonSerializable(typeof(Aethernet.Contracts.ChatMessageDto))]
[JsonSerializable(typeof(CasinoPayload))]
[JsonSerializable(typeof(Aethernet.Contracts.CasinoRoomSnapshotDto))]
[JsonSerializable(typeof(Aethernet.Contracts.CasinoRoomEventDto))]
[JsonSerializable(typeof(Aethernet.Contracts.CasinoPrivateDto))]
internal sealed partial class TelephonyJsonContext : JsonSerializerContext
{
}
