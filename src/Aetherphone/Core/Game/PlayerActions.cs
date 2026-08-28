using Aetherphone.Core.GameChat;
using Aetherphone.Core.Muster;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace Aetherphone.Core.Game;

internal readonly record struct PlayerActionAvailability(bool FriendRequest, bool Blacklist, bool AdventurerPlate,
    bool Target)
{
    public static readonly PlayerActionAvailability None = default;
}

internal static unsafe class PlayerActions
{
    private const string FriendRequestCommand = "/friendlist add ";
    private const string BlacklistCommand = "/blacklist add ";

    public static PlayerActionAvailability Resolve(string name, string world)
    {
        if (!PlayerTarget.TrySplit(name, world, out var playerName, out var worldName))
        {
            return PlayerActionAvailability.None;
        }

        try
        {
            if (!Plugin.ClientState.IsLoggedIn)
            {
                return PlayerActionAvailability.None;
            }

            var worldId = WorldId(worldName);
            if (IsLocalPlayer(playerName, worldId))
            {
                return PlayerActionAvailability.None;
            }

            var nearby = FindNearby(playerName, worldId);
            var plate = AgentCharaCard.Instance() != null &&
                        (nearby is not null || FriendContentId(playerName, worldId) != 0ul);
            var target = nearby is not null && nearby.IsTargetable && TargetSystem.Instance() != null;
            return new PlayerActionAvailability(true, true, plate, target);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[PlayerActions] availability failed");
            return PlayerActionAvailability.None;
        }
    }

    public static bool SendFriendRequest(string name, string world)
    {
        return SendPlayerCommand(FriendRequestCommand, name, world, "[PlayerActions] friend request failed");
    }

    public static bool AddToBlacklist(string name, string world)
    {
        return SendPlayerCommand(BlacklistCommand, name, world, "[PlayerActions] blacklist failed");
    }

    public static bool OpenAdventurerPlate(string name, string world)
    {
        if (!PlayerTarget.TrySplit(name, world, out var playerName, out var worldName))
        {
            return false;
        }

        try
        {
            var agent = AgentCharaCard.Instance();
            if (agent == null)
            {
                return false;
            }

            var worldId = WorldId(worldName);
            var nearby = FindNearby(playerName, worldId);
            if (nearby is not null)
            {
                agent->OpenCharaCard((GameObjectStruct*)nearby.Address);
                return true;
            }

            var contentId = FriendContentId(playerName, worldId);
            if (contentId == 0ul)
            {
                return false;
            }

            agent->OpenCharaCard(contentId);
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[PlayerActions] adventurer plate failed");
            return false;
        }
    }

    public static bool TargetPlayer(string name, string world)
    {
        if (!PlayerTarget.TrySplit(name, world, out var playerName, out var worldName))
        {
            return false;
        }

        try
        {
            var nearby = FindNearby(playerName, WorldId(worldName));
            if (nearby is null || !nearby.IsTargetable)
            {
                return false;
            }

            var targetSystem = TargetSystem.Instance();
            if (targetSystem == null)
            {
                return false;
            }

            targetSystem->Target = (GameObjectStruct*)nearby.Address;
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[PlayerActions] target failed");
            return false;
        }
    }

    private static bool SendPlayerCommand(string command, string name, string world, string failureMessage)
    {
        if (!PlayerTarget.TryFormat(name, world, out var target))
        {
            return false;
        }

        try
        {
            return ChatSender.TrySend(string.Concat(command, target));
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, failureMessage);
            return false;
        }
    }

    private static ushort WorldId(string worldName)
    {
        return MusterWorlds.TryResolve(worldName, out var worldId, out _) ? worldId : (ushort)0;
    }

    private static bool IsLocalPlayer(string name, ushort worldId)
    {
        var local = Plugin.ObjectTable.LocalPlayer;
        if (local is null || !string.Equals(local.Name.TextValue, name, StringComparison.Ordinal))
        {
            return false;
        }

        return worldId == 0 || local.HomeWorld.RowId == worldId;
    }

    private static IPlayerCharacter? FindNearby(string name, ushort worldId)
    {
        var table = Plugin.ObjectTable;
        for (var index = 0; index < table.Length; index++)
        {
            if (table[index] is not IPlayerCharacter player)
            {
                continue;
            }

            if (!string.Equals(player.Name.TextValue, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (worldId != 0 && player.HomeWorld.RowId != worldId)
            {
                continue;
            }

            return player;
        }

        return null;
    }

    private static ulong FriendContentId(string name, ushort worldId)
    {
        var proxy = InfoProxyFriendList.Instance();
        if (proxy == null)
        {
            return 0ul;
        }

        var count = proxy->EntryCount;
        for (uint index = 0; index < count; index++)
        {
            var entry = proxy->GetEntry(index);
            if (entry == null || !string.Equals(entry->NameString, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (worldId != 0 && entry->HomeWorld != worldId)
            {
                continue;
            }

            return entry->ContentId;
        }

        return 0ul;
    }
}
