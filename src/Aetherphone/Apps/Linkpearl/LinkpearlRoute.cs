using Aetherphone.Core.Contacts;

namespace Aetherphone.Apps.Linkpearl;

internal enum LinkpearlScreen : byte
{
    Root,
    Conversation,
    TabEditor,
    Settings,
    FriendDetail,
    CharacterDetail,
    FreeCompanyDetail,
}

internal readonly struct LinkpearlRoute
{
    public static readonly LinkpearlRoute Root = new(LinkpearlScreen.Root, string.Empty, null, string.Empty,
        string.Empty, string.Empty);

    public static readonly LinkpearlRoute Settings = new(LinkpearlScreen.Settings, string.Empty, null,
        string.Empty, string.Empty, string.Empty);

    public readonly LinkpearlScreen Screen;
    public readonly string ConversationKey;
    public readonly FriendEntry? Friend;
    public readonly string LookupId;
    public readonly string LookupName;
    public readonly string LookupWorld;

    private LinkpearlRoute(LinkpearlScreen screen, string conversationKey, FriendEntry? friend, string lookupId,
        string lookupName, string lookupWorld)
    {
        Screen = screen;
        ConversationKey = conversationKey;
        Friend = friend;
        LookupId = lookupId;
        LookupName = lookupName;
        LookupWorld = lookupWorld;
    }

    public static LinkpearlRoute Conversation(string key) =>
        new(LinkpearlScreen.Conversation, key, null, string.Empty, string.Empty, string.Empty);

    public static LinkpearlRoute TabEditor(string tabId) =>
        new(LinkpearlScreen.TabEditor, tabId, null, string.Empty, string.Empty, string.Empty);

    public static LinkpearlRoute Detail(FriendEntry friend) =>
        new(LinkpearlScreen.FriendDetail, string.Empty, friend, string.Empty, string.Empty, string.Empty);

    public static LinkpearlRoute Character(string id, string name, string world) =>
        new(LinkpearlScreen.CharacterDetail, string.Empty, null, id, name, world);

    public static LinkpearlRoute FreeCompany(string id, string name, string world) =>
        new(LinkpearlScreen.FreeCompanyDetail, string.Empty, null, id, name, world);
}
