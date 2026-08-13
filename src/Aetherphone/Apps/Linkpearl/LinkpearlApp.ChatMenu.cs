using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const byte MenuCopyText = 0;
    private const byte MenuCopyName = 1;
    private const byte MenuSendTell = 2;
    private const byte MenuLookUp = 3;
    private const byte MenuInviteParty = 4;
    private const byte MenuOpenUrl = 5;
    private const byte MenuTryOn = 6;
    private const byte MenuCompare = 7;
    private const byte MenuRecipes = 8;
    private const byte MenuFindItem = 9;
    private const byte MenuLinkInChat = 10;
    private const byte MenuOpenMarket = 11;
    private const byte MenuOpenMap = 12;

    private readonly DropdownMenu chatMenu = new();
    private readonly List<DropdownMenu.Item> chatMenuItems = new(10);
    private readonly List<byte> chatMenuActions = new(10);
    private string chatMenuText = string.Empty;
    private string chatMenuName = string.Empty;
    private string chatMenuWorld = string.Empty;
    private ChatChunk chatMenuLink;
    private bool chatMenuIsLink;
    private Vector2 chatMenuAnchor;
    private bool chatMenuPending;
    private int chatMenuToken;

    private void OpenChatMenu(string text, string? name)
    {
        chatMenuText = text;
        chatMenuName = name ?? string.Empty;
        chatMenuWorld = string.Empty;
        chatMenuIsLink = false;
        ArmChatMenu();
    }

    private void OpenLinkMenu(ChatEntry entry, ChatChunk link)
    {
        chatMenuText = link.Text;
        chatMenuName = link.Kind == ChatChunkKind.Player ? link.Text : entry.AuthorName;
        chatMenuWorld = link.Kind == ChatChunkKind.Player ? link.World : entry.AuthorWorld;
        chatMenuLink = link;
        chatMenuIsLink = true;
        ArmChatMenu();
    }

    private void ArmChatMenu()
    {
        chatMenuAnchor = ImGui.GetMousePos();
        chatMenuPending = true;
        chatMenuToken++;
    }

    private void DrawChatMenu(Rect area)
    {
        var id = chatMenuToken.ToString(Loc.Culture);
        if (chatMenuPending)
        {
            chatMenuPending = false;
            chatMenu.Toggle(id, new Rect(chatMenuAnchor, chatMenuAnchor + new Vector2(1f, 1f)));
        }

        if (!chatMenu.IsOpenFor(id))
        {
            return;
        }

        chatMenuItems.Clear();
        chatMenuActions.Clear();
        if (chatMenuIsLink)
        {
            BuildLinkItems();
        }
        else
        {
            BuildMessageItems();
        }

        var clicked = chatMenu.Draw(area, frameTheme, CollectionsMarshal.AsSpan(chatMenuItems));
        if (clicked < 0)
        {
            return;
        }

        Run(chatMenuActions[clicked]);
    }

    private void BuildMessageItems()
    {
        Add(L.Messages.CopyMessage, FontAwesomeIcon.Copy, MenuCopyText);
        if (chatMenuName.Length == 0)
        {
            return;
        }

        Add(L.Messages.CopyName, FontAwesomeIcon.User, MenuCopyName);
        Add(L.Linkpearl.SendTell, FontAwesomeIcon.PenAlt, MenuSendTell);
        Add(L.Linkpearl.LookUp, FontAwesomeIcon.Search, MenuLookUp);
        Add(L.Linkpearl.InviteToParty, FontAwesomeIcon.UserPlus, MenuInviteParty);
    }

    private void BuildLinkItems()
    {
        switch (chatMenuLink.Kind)
        {
            case ChatChunkKind.Url:
                Add(L.Common.OpenInBrowser, FontAwesomeIcon.ExternalLinkAlt, MenuOpenUrl);
                Add(L.Linkpearl.CopyLink, FontAwesomeIcon.Copy, MenuCopyText);
                return;
            case ChatChunkKind.Item:
                Add(L.Linkpearl.TryOn, FontAwesomeIcon.Tshirt, MenuTryOn);
                Add(L.Linkpearl.CompareItem, FontAwesomeIcon.BalanceScale, MenuCompare);
                Add(L.Linkpearl.SearchRecipes, FontAwesomeIcon.Hammer, MenuRecipes);
                Add(L.Linkpearl.FindItem, FontAwesomeIcon.BoxOpen, MenuFindItem);
                Add(L.Linkpearl.LinkInChat, FontAwesomeIcon.Comment, MenuLinkInChat);
                Add(L.Linkpearl.OpenInMarket, FontAwesomeIcon.Store, MenuOpenMarket);
                Add(L.Messages.CopyName, FontAwesomeIcon.Copy, MenuCopyText);
                return;
            case ChatChunkKind.Map:
                Add(L.Linkpearl.OpenMap, FontAwesomeIcon.MapMarkedAlt, MenuOpenMap);
                Add(L.Linkpearl.CopyLink, FontAwesomeIcon.Copy, MenuCopyText);
                return;
            case ChatChunkKind.Player:
                Add(L.Linkpearl.SendTell, FontAwesomeIcon.PenAlt, MenuSendTell);
                Add(L.Linkpearl.LookUp, FontAwesomeIcon.Search, MenuLookUp);
                Add(L.Linkpearl.InviteToParty, FontAwesomeIcon.UserPlus, MenuInviteParty);
                Add(L.Messages.CopyName, FontAwesomeIcon.Copy, MenuCopyName);
                return;
            default:
                Add(L.Messages.CopyName, FontAwesomeIcon.Copy, MenuCopyText);
                return;
        }
    }

    private void Add(LocString label, FontAwesomeIcon icon, byte action)
    {
        chatMenuItems.Add(new DropdownMenu.Item(Loc.T(label), icon.ToIconString()));
        chatMenuActions.Add(action);
    }

    private void Run(byte action)
    {
        switch (action)
        {
            case MenuCopyText:
                Copy(chatMenuText);
                break;
            case MenuCopyName:
                Copy(chatMenuName);
                break;
            case MenuSendTell:
                OpenDirectThread(chatMenuName, SendTargetFor(chatMenuName, chatMenuWorld));
                break;
            case MenuLookUp:
                router.Push(LinkpearlRoute.Character(string.Empty, chatMenuName, chatMenuWorld));
                break;
            case MenuInviteParty:
                GameLinkActions.InviteToParty(chatMenuName, chatMenuWorld);
                break;
            case MenuOpenUrl:
                UrlActions.OpenInBrowser(chatMenuText);
                break;
            case MenuTryOn:
                GameLinkActions.TryOn(chatMenuLink.Id);
                break;
            case MenuCompare:
                GameLinkActions.CompareItem(chatMenuLink.Id);
                break;
            case MenuRecipes:
                GameLinkActions.SearchRecipes(chatMenuLink.Id);
                break;
            case MenuFindItem:
                GameLinkActions.FindItem(chatMenuLink.Id);
                break;
            case MenuLinkInChat:
                GameLinkActions.LinkInChat(chatMenuLink.Id);
                break;
            case MenuOpenMarket:
                marketLauncher.RequestItem(chatMenuLink.Id);
                frameNavigation.Open("market");
                break;
            case MenuOpenMap:
                GameLinkActions.OpenMap(chatMenuLink.TerritoryId, chatMenuLink.MapId, chatMenuLink.RawX,
                    chatMenuLink.RawY);
                break;
        }
    }

    private static void Copy(string value)
    {
        if (value.Length == 0)
        {
            return;
        }

        ImGui.SetClipboardText(value);
        CopyToast.Show();
    }

    private static string SendTargetFor(string name, string world) =>
        world.Length > 0 ? string.Concat(name, "@", world) : name;
}
