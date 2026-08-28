namespace Aetherphone.Core.GameChat;

internal sealed class ChannelStyle
{
    public uint IncomingName { get; set; }

    public uint IncomingBody { get; set; }

    public uint OutgoingName { get; set; }

    public uint OutgoingBody { get; set; }

    public bool NeverUnread { get; set; }

    public bool HideOutgoing { get; set; }

    public bool HideFromGameChat { get; set; }
}
