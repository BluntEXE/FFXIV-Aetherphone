using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Home;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows;

internal sealed class LinkpearlPopouts : IDisposable
{
    public const int MaxWindows = 6;

    private readonly LinkpearlPopoutWindow[] windows;
    private readonly Configuration configuration;
    private readonly ChatInbox inbox;
    private readonly ChatLog log;
    private readonly TabStore tabs;
    private readonly TellPreferences tellPreferences;
    private readonly LinkpearlNotificationGate gate;
    private readonly PhoneVisibility visibility;
    private readonly AppGate installed;
    private readonly List<LinkpearlPopoutState> restoreQueue = new(MaxWindows);

    public LinkpearlPopouts(Configuration configuration, ChatInbox inbox, ChatLog log, ChatSend send, TabStore tabs,
        TellPreferences tellPreferences, LinkpearlNotificationGate gate, PhoneVisibility visibility,
        AppGate installed, GameData gameData, ThemeProvider themes, LodestoneService lodestone,
        NotificationService notifications)
    {
        this.configuration = configuration;
        this.inbox = inbox;
        this.log = log;
        this.tabs = tabs;
        this.tellPreferences = tellPreferences;
        this.gate = gate;
        this.visibility = visibility;
        this.installed = installed;
        windows = new LinkpearlPopoutWindow[MaxWindows];
        for (var slot = 0; slot < MaxWindows; slot++)
        {
            windows[slot] = new LinkpearlPopoutWindow(this, slot, configuration, inbox, tabs, log, send, gameData,
                themes, lodestone, notifications);
        }

        var saved = configuration.LinkpearlPopouts;
        for (var index = 0; index < saved.Count && index < MaxWindows; index++)
        {
            if (saved[index].Key.Length > 0)
            {
                restoreQueue.Add(saved[index]);
            }
        }

        log.Appended += OnAppended;
        tabs.Changed += ReopenThreads;
    }

    public IReadOnlyList<LinkpearlPopoutWindow> Windows => windows;

    public Action<string>? OpenInPhone { get; set; }

    public Action<string, string>? LookUpInPhone { get; set; }

    public Action<uint>? OpenMarketInPhone { get; set; }

    public bool CanOpenMore => Free() is not null;

    public int OpenCount
    {
        get
        {
            var count = 0;
            for (var index = 0; index < windows.Length; index++)
            {
                if (windows[index].Bound)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool AnyExpanded
    {
        get
        {
            for (var index = 0; index < windows.Length; index++)
            {
                if (windows[index].Bound && !windows[index].IsCollapsed)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void Restore()
    {
        for (var index = 0; index < restoreQueue.Count; index++)
        {
            var state = restoreQueue[index];
            var window = Free();
            if (window is null)
            {
                break;
            }

            window.Bind(state.Key, state);
        }

        restoreQueue.Clear();
    }

    public bool IsOpen(string key) => Bound(key) is not null;

    public bool Open(string key)
    {
        if (!installed.Open || key.Length == 0)
        {
            return false;
        }

        if (Bound(key) is { } existing)
        {
            existing.Focus();
            return true;
        }

        var window = Free();
        if (window is null)
        {
            return false;
        }

        window.Bind(key, null);
        Persist();
        return true;
    }

    public void Close(string key)
    {
        var window = Bound(key);
        if (window is null)
        {
            return;
        }

        window.Unbind();
        Persist();
    }

    public bool Toggle(string key)
    {
        if (IsOpen(key))
        {
            Close(key);
            return true;
        }

        return Open(key);
    }

    public void CloseAll()
    {
        for (var index = 0; index < windows.Length; index++)
        {
            windows[index].Unbind();
        }

        Persist();
    }

    public void SetAllCollapsed(bool collapsed)
    {
        var changed = false;
        for (var index = 0; index < windows.Length; index++)
        {
            changed |= windows[index].SetCollapsed(collapsed);
        }

        if (changed)
        {
            Persist();
        }
    }

    public void OnCollapseChanged() => Persist();

    public bool Suppressed { get; private set; }

    public void SetSuppressed(bool suppressed)
    {
        if (Suppressed == suppressed)
        {
            return;
        }

        Suppressed = suppressed;
        for (var index = 0; index < windows.Length; index++)
        {
            windows[index].SetSuppressed(suppressed);
        }
    }

    public void OpenTell(string name, string world)
    {
        if (name.Length == 0)
        {
            return;
        }

        var row = inbox.EnsureTell(name, world);
        Open(row.Key);
    }

    public void Switch(LinkpearlPopoutWindow window, string key)
    {
        if (Bound(key) is { } other && !ReferenceEquals(other, window))
        {
            other.Focus();
            return;
        }

        window.Rebind(key);
        Persist();
    }

    public void OnWindowClosed(LinkpearlPopoutWindow window)
    {
        window.Unbind();
        Persist();
    }

    public void Persist()
    {
        var states = configuration.LinkpearlPopouts;
        states.Clear();
        for (var index = 0; index < windows.Length; index++)
        {
            if (windows[index].Bound)
            {
                states.Add(windows[index].Snapshot());
            }
        }

        configuration.Save();
    }

    public void Dispose()
    {
        log.Appended -= OnAppended;
        tabs.Changed -= ReopenThreads;
        Persist();
        configuration.SaveNow();
    }

    private void ReopenThreads()
    {
        for (var index = 0; index < windows.Length; index++)
        {
            if (windows[index].Bound)
            {
                windows[index].ReopenThread();
            }
        }
    }

    private LinkpearlPopoutWindow? Bound(string key)
    {
        for (var index = 0; index < windows.Length; index++)
        {
            if (windows[index].Bound && string.Equals(windows[index].Key, key, StringComparison.Ordinal))
            {
                return windows[index];
            }
        }

        return null;
    }

    private LinkpearlPopoutWindow? Free()
    {
        for (var index = 0; index < windows.Length; index++)
        {
            if (!windows[index].Bound)
            {
                return windows[index];
            }
        }

        return null;
    }

    private void OnAppended(ChatEntry entry)
    {
        if (!configuration.LinkpearlPopoutTells || entry.IsSelf || !ChatStreams.IsTell(entry.StreamKey))
        {
            return;
        }

        if (!installed.Open || gate.Paused || configuration.DoNotDisturb || visibility.IsVisible)
        {
            return;
        }

        if (tellPreferences.IsMuted(entry.StreamKey) || IsOpen(entry.StreamKey))
        {
            return;
        }

        inbox.Sync();
        Open(entry.StreamKey);
    }
}
