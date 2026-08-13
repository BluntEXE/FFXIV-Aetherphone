namespace Aetherphone.Core.GameChat;

internal enum ChatChunkKind : byte
{
    Text,
    Player,
    Item,
    Map,
}

internal readonly struct ChatChunk
{
    public readonly ChatChunkKind Kind;
    public readonly string Text;
    public readonly string World;
    public readonly uint ItemId;
    public readonly uint TerritoryId;
    public readonly uint MapId;
    public readonly int RawX;
    public readonly int RawY;

    private ChatChunk(ChatChunkKind kind, string text, string world, uint itemId, uint territoryId, uint mapId,
        int rawX, int rawY)
    {
        Kind = kind;
        Text = text;
        World = world;
        ItemId = itemId;
        TerritoryId = territoryId;
        MapId = mapId;
        RawX = rawX;
        RawY = rawY;
    }

    public static ChatChunk Plain(string text) =>
        new(ChatChunkKind.Text, text, string.Empty, 0u, 0u, 0u, 0, 0);

    public static ChatChunk Player(string name, string world) =>
        new(ChatChunkKind.Player, name, world, 0u, 0u, 0u, 0, 0);

    public static ChatChunk Item(string name, uint itemId) =>
        new(ChatChunkKind.Item, name, string.Empty, itemId, 0u, 0u, 0, 0);

    public static ChatChunk Map(string text, uint territoryId, uint mapId, int rawX, int rawY) =>
        new(ChatChunkKind.Map, text, string.Empty, 0u, territoryId, mapId, rawX, rawY);

    public bool IsLink => Kind != ChatChunkKind.Text;
}
