using Godot;
using System;

public partial class NetworkManager : Node
{
    private static NetworkManager instance;
    public static NetworkManager Instance { get { return instance; } }

    public NetworkManager()
    {
        instance = this;
        AddToGroup(Globals.Groups.AutoLoad.ToString()); 
    }

    public string PlayerName { get; protected set; }
    public ulong PlayerId { get; protected set; } = 0;
    public bool ConnectedToNetwork { get; protected set; }
    public bool IsServer { get; protected set; } = true;
    public int SocketConnections { get; set; } = 0;
    public int MaxConnections { get; protected set; } = 5;

}
