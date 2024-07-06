using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static System.Collections.Specialized.BitVector32;

public class ServerRpc : Attribute { }
public abstract partial class NetworkDataManager : Node, IListener
{
    protected static NetworkDataManager instance;
    public static NetworkDataManager Instance { get { return instance; } }

    private static readonly string _DataType = "DataType";
    private static readonly string _NetworkNodes = "NetworkNodes";
    public NetworkDataManager() 
    {
        instance = this;
        AddToGroup(Globals.Groups.AutoLoad.ToString());
    }

    public Dictionary<ulong, Node> OwnerIdToPlayer { get; private set; } = new Dictionary<ulong, Node>();
    private Stopwatch UpdateTimer { get; set; } = new Stopwatch();
    public float UpdateIntervalInMil { get; private set; } = 50f;

    #region Flags
    private bool RecievedFullServerData = false;
    #endregion

    #region UniqueId

    private uint nextAvailableSelfUniqueId = uint.MaxValue - 10000000;
    private uint nextAvailableUniqueId = 0;

    private Dictionary<uint, Node> uniqueIdToNode = new Dictionary<uint, Node>();

    public event Action NetworkUpdate_Client;


    public Node UniqueIdToNode(uint id)
    {
        if (uniqueIdToNode.ContainsKey(id))
            return uniqueIdToNode[id];
        else if (!NetworkManager.Instance.IsServer)
            RequestForceUpdate();
        return null;
    }

    public bool HasUniqueIdToNode(uint id)
    {
        return uniqueIdToNode.ContainsKey(id);
    }

    public void ApplyNextAvailableUniqueId(Node node)
    {
        if (!node.HasMeta(Globals.Meta.UniqueId.ToString()))
        {
            uint result = nextAvailableUniqueId;
            nextAvailableUniqueId++;
            node.SetMeta(Globals.Meta.UniqueId.ToString(), result.ToString());

            uniqueIdToNode[result] = node;
        }
        foreach (Node child in node.GetChildren())
        {
            ApplyNextAvailableUniqueId(child);
        }
    }

    public void ApplyNextAvailableSelfUniqueId(Node node)
    {
        node.AddToGroup(Globals.Groups.SelfOnly.ToString());
        if (!node.HasMeta(Globals.Meta.UniqueId.ToString()))
        {
            uint result = nextAvailableSelfUniqueId;
            nextAvailableSelfUniqueId++;
            node.SetMeta(Globals.Meta.UniqueId.ToString(), result.ToString());

            uniqueIdToNode[result] = node;
        }
        foreach (Node child in node.GetChildren())
        {
            ApplyNextAvailableSelfUniqueId(child);
        }
    }

    #endregion

    #region NetworkNodes

    private List<INetworkData> NetworkNodes = new List<INetworkData>();
    public void AddNetworkNodes(Node node)
    {
        if (node is INetworkData nd && !node.IsInGroup(Globals.Groups.SelfOnly.ToString()))
            if (!NetworkNodes.Contains(nd))
                NetworkNodes.Add(nd);

        foreach (Node child in node.GetChildren())
            AddNetworkNodes(child);

    }
    public void RemoveNetworkNodes(Node node)
    {
        if (node is INetworkData nd)
        {
            if (NetworkNodes.Contains(nd))
                NetworkNodes.Remove(nd);
        }
        foreach (Node child in node.GetChildren())
            RemoveNetworkNodes(child);
    }

    #endregion

    public override void _Ready()
    {
        base._Ready();

        UpdateTimer.Start();
        EventSystem.Instance.Subscribe(EventID.OnConnectionRecievedData, this);
        EventSystem.Instance.Subscribe(EventID.OnSocketRecievedData, this);
        ApplyNextAvailableUniqueId(GetTree().Root);
        
    }
    public override void _ExitTree()
    {
        base._ExitTree();
        EventSystem.Instance.UnsubscribeAll(this);
    }
    public override void _Process(double delta)
    {
        base._Process(delta);
        
        if (UpdateTimer.Elapsed.TotalMilliseconds >= UpdateIntervalInMil)
        {
            if (NetworkManager.Instance.IsServer)
            {
                if (NetworkManager.Instance.SocketConnections < 2) return;

                JsonValue scenelessData = new JsonValue();
                scenelessData[_DataType].Set((int)Globals.DataType.ServerUpdate);
                foreach (var n in NetworkNodes)
                {
                    scenelessData[_NetworkNodes][(n as Node).GetMeta(Globals.Meta.UniqueId.ToString()).ToString()]
                    .Set((n).SerializeNetworkData(false));
                }
                if (scenelessData[_NetworkNodes].ToString() != "{}" && scenelessData[_NetworkNodes].ToString() != "null")
                    SendToClients(scenelessData);
            }
            else if(RecievedFullServerData) //only start sending client data after recieving full server data
            {
                NetworkUpdate_Client?.Invoke();
            }

            UpdateTimer.Restart();
        }
    }

    private void ForceUpdateClients()
    {
        GD.Print("Gonna force an update");
        JsonValue scenelessData = new JsonValue();
        scenelessData[_DataType].Set((int)Globals.DataType.ServerUpdate);
        foreach (var n in NetworkNodes)
        {
            scenelessData[_NetworkNodes][(n as Node).GetMeta(Globals.Meta.UniqueId.ToString()).ToString()]
            .Set((n).SerializeNetworkData(true));
        }
        if (scenelessData[_NetworkNodes].ToString() != "{}" && scenelessData[_NetworkNodes].ToString() != "null")
            SendToClients(scenelessData);
    }

    public abstract void SendToServer(JsonValue data, bool reliable = true);
    public abstract void SendToClientId(ulong client, JsonValue data, bool reliable = true);
    public abstract void SendToClients(JsonValue data, bool reliable = true);

    #region AddRemoveNode

    public void AddSelfNode(Node owner, Node newNode)
    {
        ApplyNextAvailableSelfUniqueId(newNode);
        owner.AddChild(newNode, true);
    }

    public void RemoveSelfNode(Node node)
    {
        SafeQueueFree(node);
    }

    private void SafeQueueFree(Node node)
    {
        if (!node.IsValid()) return;

        node.QueueFree();
    }

    public void AddServerNode(Node owner, Node newNode, Vector3 positionOverride = new Vector3(), bool persistent = true)
    {
        if (!NetworkManager.Instance.IsServer)
        {
            throw new Exception("ERROR: CLIENT IS TRYING TO CALL AddServerNode");
        }

        bool levelless = false;
        if (!owner.HasMeta(Globals.Meta.LevelPartitionName.ToString()) && !newNode.HasMeta(Globals.Meta.LevelPartitionName.ToString()))
        {
            if (!LevelManager.Instance.LevelActive(newNode.Name))
                levelless = true;
        }


        if (!persistent || levelless)
        {
            newNode.AddToGroup(Globals.Groups.NotPersistent.ToString());
        }
        if (positionOverride != Vector3.Zero)
        {
            if (newNode is Node2D n2d)
                n2d.Position = new Vector2(positionOverride.X, positionOverride.Y);

            else if (newNode is Control c)
                c.Position = new Vector2(positionOverride.X, positionOverride.Y);
            else if (newNode is Node3D n3d)
                n3d.Position = positionOverride;
        }

        ApplyNextAvailableUniqueId(newNode);
        owner.AddChild(newNode, true);

        LevelManager.Instance.CheckAddNode(owner, newNode);
        AddNetworkNodes(newNode);

        JsonValue data = new JsonValue();

        data[_DataType].Set((int)Globals.DataType.ServerAdd);

        // need to collect all data about the node and send to clients
        data["Owner"].Set(owner.GetMeta(Globals.Meta.UniqueId.ToString()).ToString());

        data["Node"].Set(ConvertNodeToJson(newNode));


        SendToClients(data);
    }

    public void RemoveServerNode(Node removeNode)
    {
        if (!NetworkManager.Instance.IsServer)
        {
            throw new Exception("ERROR: CLIENT IS TRYING TO CALL RemoveNode");
        }
        RemoveNetworkNodes(removeNode);

        JsonValue data = new JsonValue();
        data["UniqueId"].Set(removeNode.GetMeta(Globals.Meta.UniqueId.ToString()).ToString());

        LevelManager.Instance.CheckRemoveNode(removeNode);

        SafeQueueFree(removeNode);
        // tell all clients to queue free this node
        data[_DataType].Set((int)Globals.DataType.ServerRemove);

        SendToClients(data);

    }

    #endregion

    #region Server
    private void OnSocketDataRecieved(JsonValue value)
    {
        Globals.DataType dataType = (Globals.DataType)value[_DataType].AsInt();

        switch (dataType)
        {
            case Globals.DataType.RpcCall:
                HandleRpc(value);
                break;
            case Globals.DataType.ClientInputUpdate:
                {
                    Player player = UniqueIdToNode(uint.Parse(value["O"].AsString())) as Player;
                    InputManager.Instance.HandleClientInputUpdate(player, value);
                }
                break;
            case Globals.DataType.RequestForceUpdate:
                ForceUpdateClients();
                break;
        }
    }

    protected JsonValue FullServerData()
    {
        JsonValue data = new JsonValue();
        data[_DataType].Set((int)Globals.DataType.FullServerData);

        foreach (Node child in GetTree().CurrentScene.GetChildren())
        {
            JsonValue nodeData = ConvertNodeToJson(child);
            data["Nodes"].Append(nodeData);
            AddNodeToUniqueIdDict(child);
        }

        return data;
    }

    #endregion

    #region Client
    private void OnConnectionDataRecieved(JsonValue value)
    {
        Globals.DataType dataType = (Globals.DataType)value[_DataType].AsInt();

        switch (dataType)
        {
            case Globals.DataType.ServerUpdate:
                HandleServerUpdate(value);
                break;
            case Globals.DataType.RpcCall:
                HandleRpc(value);
                break;
            case Globals.DataType.ServerAdd:
                HandleServerAdd(value);
                break;
            case Globals.DataType.ServerRemove:
                // TODO: Add logic to find node of unique

                string uniqueIdStr = value["UniqueId"].AsString();
                uint uniqueId = uint.Parse(uniqueIdStr);
                Node removeNode = uniqueIdToNode[uniqueId];
                SafeQueueFree(removeNode);
                uniqueIdToNode.Remove(uniqueId);
                break;
            case Globals.DataType.FullServerData:
                HandleFullServerData(value);
                break;
        }
    }


    private void HandleServerAdd(JsonValue data)
    {
        if (!RecievedFullServerData)
            return;
        // Client does not know who owner is, so we'll ignore this add for now
        // currently an issue when player first joins
        if (!uniqueIdToNode.ContainsKey(Convert.ToUInt32(data["Owner"].AsString())))
        {
            GD.Print("HandleServerAdd: We dont know ID: " + data["Owner"].AsString());
            return;
        }

        string uniqueIdStr = data["Node"][_Meta][Globals.Meta.UniqueId.ToString()].AsString();
        uint uId = Convert.ToUInt32(uniqueIdStr);
        if (uniqueIdToNode.ContainsKey(uId))
        {
            GD.Print("I already know about this node?");
            // client already knows about this object, dont add again
            return;
        }

        Node node = ConvertJsonToNode(data["Node"]);
        Node owner = uniqueIdToNode[Convert.ToUInt32(data["Owner"].AsString())];

        AddNodeToUniqueIdDict(node);

        owner.CallDeferred(_AddChild, node, true);
    }

    public void ClientInputUpdate(JsonValue inputData)
    {
        inputData["O"].Set(Helper.Instance.LocalPlayer.GetMeta(Globals.Meta.UniqueId.ToString()).ToString());
       
        inputData[_DataType].Set((int)Globals.DataType.ClientInputUpdate);
        SendToServer(inputData);
    }

    private void HandleServerUpdate(JsonValue data)
    {
        if (!RecievedFullServerData) return;
        // first we verify we have all these nodes in our instance
        foreach (var item in data[_NetworkNodes].Object)
        {
            if (!uniqueIdToNode.ContainsKey(Convert.ToUInt32(item.Key)))
            {
                Node n = null;
                bool found = SearchForNode(item.Key, ref n, GetTree().Root);

                if (!found)
                    return;
                else
                    uniqueIdToNode[Convert.ToUInt32(item.Key)] = n;
            }
            
        }

        foreach (var item in data[_NetworkNodes].Object)
        {
            INetworkData n = uniqueIdToNode[Convert.ToUInt32(item.Key)] as INetworkData;

            if (n == null)
            {
                return;
            }
            n.DeserializeNetworkData(item.Value, false);
        }
    }

    private void HandleFullServerData(JsonValue data)
    {
        RecievedFullServerData = true;
        foreach (var item in data["Nodes"].Array)
        {
            uint id = Convert.ToUInt32(item["Meta"][Globals.Meta.UniqueId.ToString()].AsString());
            if (!uniqueIdToNode.ContainsKey(id))
            {
                Node node = ConvertJsonToNode(item);
                GetTree().CurrentScene.AddChild(node);
                AddNodeToUniqueIdDict(node);
            }
        }
    }

    #endregion

    #region RPC
    public void RpcServer(Node caller, string methodName, params Variant[] param)
    {
        JsonValue message = new JsonValue();
        message[_DataType].Set((int)Globals.DataType.RpcCall);
        message["Caller"].Set((string)caller.GetMeta(Globals.Meta.UniqueId.ToString()));
        message["MethodName"].Set(methodName);

        foreach (Variant variant in param)
        {
            message["Params"].Append(Helper.Instance.VariantToJson(variant));
        }

        SendToServer(message);
    }

    public void RpcClients(Node caller, string methodName, params Variant[] param)
    {
        JsonValue message = new JsonValue();
        message[_DataType].Set((int)Globals.DataType.RpcCall);
        message["Caller"].Set((string)caller.GetMeta(Globals.Meta.UniqueId.ToString()));
        message["MethodName"].Set(methodName);

        foreach (Variant variant in param)
        {
            message["Params"].Append(Helper.Instance.VariantToJson(variant));
        }

        SendToClients(message);
    }

    public void RpcClient(ulong ownerId, Node caller, string methodName, params Variant[] param)
    {
        JsonValue message = new JsonValue();
        message[_DataType].Set((int)Globals.DataType.RpcCall);
        message["Caller"].Set((string)caller.GetMeta(Globals.Meta.UniqueId.ToString()));
        message["MethodName"].Set(methodName);

        foreach (Variant variant in param)
        {
            message["Params"].Append(Helper.Instance.VariantToJson(variant));
        }

        SendToClientId(ownerId, message);
    }

    /// <summary>
    /// Server Handles an Rpc call
    /// </summary>
    private void HandleRpc(JsonValue value)
    {
        Node node = uniqueIdToNode[uint.Parse(value["Caller"].AsString())];
        string methodName = value["MethodName"].AsString();

        List<Variant> args = new List<Variant>();
        foreach (JsonValue variant in value["Params"].Array)
        {
            Variant variantToAdd = new Variant();
            variantToAdd = Helper.Instance.JsonToVariant(variant);
            args.Add(variantToAdd);
        }

        node.Call(methodName, args.ToArray());
    }
    #endregion


    #region HelperFunctions

    private void RequestForceUpdate()
    {
        JsonValue data = new JsonValue();
        data[_DataType].Set((int)Globals.DataType.RequestForceUpdate);
        SendToServer(data);
    }

    public void AddNodeToUniqueIdDict(Node node)
    {
        string uniqueStr = node.GetMeta(Globals.Meta.UniqueId.ToString()).ToString();
        uint id = uint.Parse(uniqueStr);
        uniqueIdToNode[id] = node;
        foreach (Node n in node.GetChildren())
            AddNodeToUniqueIdDict(n);
    }
    private bool SearchForNode(string id, ref Node reference, Node searchPoint)
    {
        if (searchPoint.GetMeta(Globals.Meta.UniqueId.ToString()).ToString() == id)
        {
            reference = searchPoint;
            return true;
        }
        foreach (Node child in searchPoint.GetChildren())
        {
            if (SearchForNode(id, ref reference, child))
                return true;
        }

        return false;
    }

    #endregion

    #region NodeJsonConversion
    private static readonly string _Name = "Name";
    private static readonly string _Type = "Type";
    private static readonly string _DerivedType = "DerivedType";
    private static readonly string _Position = "Position";
    private static readonly string _Rotation = "Rotation";
    private static readonly string _Scale = "Scale";
    private static readonly string _Size = "Size";
    private static readonly string _ZIndex = "ZIndex";
    private static readonly string _ZIsRelative = "ZIsRelative";
    private static readonly string _YSortEnabled = "YSortEnabled";
    private static readonly string _Meta = "Meta";
    private static readonly string _Group = "Group";
    private static readonly string _Children = "Children";
    private static readonly string _INetworkData = "INetworkData";
    private static readonly StringName _AddChild = "add_child";
    public static JsonValue ConvertNodeToJson(Node node)
    {
        JsonValue val = CollectNodeData(node);
        return val;
    }

    private static JsonValue CollectNodeData(Node node)
    {
        JsonValue jsonNode = new JsonValue();

        if (node.IsInGroup(Globals.Groups.SelfOnly.ToString()))
            return new JsonValue();
        if (node.GetParent().IsValid())
            if (node.GetParent().IsInGroup(Globals.Groups.IgnoreChildren.ToString()) ||
                node.GetParent().IsInGroup(Globals.Groups.IgnoreChildrenNetwork.ToString()))
                return new JsonValue();

        jsonNode[_Name].Set(node.Name);
        jsonNode[_Type].Set(RemoveNamespace(node.GetType().ToString()));
        jsonNode[_DerivedType].Set(node.GetClass());
        if (node is Node2D)
        {
            Node2D node2d = (Node2D)node;
            jsonNode[_ZIsRelative].Set(node2d.ZAsRelative);
            jsonNode[_YSortEnabled].Set(node2d.YSortEnabled);
            jsonNode[_ZIndex].Set(node2d.ZIndex);

            jsonNode[_Position].Set(node2d.Position);
            jsonNode[_Rotation].Set(node2d.Rotation);
            jsonNode[_Scale].Set(node2d.Scale);
        }
        else if (node is Control c)
        {
            jsonNode[_Position].Set(c.Position);
            jsonNode[_Rotation].Set(c.Rotation);
            jsonNode[_Scale].Set(c.Scale);
            jsonNode[_Size].Set(c.Size);
        }
        else if (node is Node3D)
        {
            Node3D node3d = (Node3D)node;

            jsonNode[_Position].Set(node3d.Position);
            jsonNode[_Rotation].Set(node3d.Rotation);
            jsonNode[_Scale].Set(node3d.Scale);

        }

        foreach (string meta in node.GetMetaList())
        {
            jsonNode[_Meta][meta].Set((string)node.GetMeta(meta));
        }
        foreach (string group in node.GetGroups())
            jsonNode[_Group].Append(group);

        for (int i = 0; i < node.GetChildCount(); i++)
            jsonNode[_Children].Append(CollectNodeData(node.GetChild(i)));

        if (node is INetworkData)
            jsonNode[_INetworkData].Set((node as INetworkData).SerializeNetworkData(true, true));

        return jsonNode;
    }

    public static Node ConvertJsonToNode(JsonValue data)
    {

        Node node = (Node)ClassDB.Instantiate(data[_DerivedType].AsString());

        // Set Basic Node Data
        node.Name = data[_Name].AsString();
        if (node is Node2D)
        {
            Node2D node2d = (Node2D)node;

            node2d.Position = data[_Position].AsVector2();
            node2d.Rotation = data[_Rotation].AsFloat();
            node2d.Scale = data[_Scale].AsVector2();

            node2d.ZIndex = data[_ZIndex].AsInt();
            node2d.ZAsRelative = data[_ZIsRelative].AsBool();
            node2d.YSortEnabled = data[_YSortEnabled].AsBool();
        }
        else if (node is Control c)
        {
            c.Position = data[_Position].AsVector2();
            c.Rotation = data[_Rotation].AsFloat();
            c.Scale = data[_Scale].AsVector2();
            c.Size = data[_Size].AsVector2();
        }
        else if (node is Node3D)
        {
            Node3D node3d = (Node3D)node;

            node3d.Position = data[_Position].AsVector3();
            node3d.Rotation = data[_Rotation].AsVector3();
            node3d.Scale = data[_Scale].AsVector3();

        }


        // Save node instance id to re-reference after setting script
        ulong nodeID = node.GetInstanceId();
        // if type != derived-type, a script is attached
        if (!data[_Type].AsString().Equals(data[_DerivedType].AsString()))
        {
            node.SetScript(GD.Load<Script>(ResourceManager.Instance.GetScriptPath(data[_Type].AsString())));
            // Retrive node after losing it from SetScript

        }

        node = GodotObject.InstanceFromId(nodeID) as Node;

        foreach (KeyValuePair<string, JsonValue> meta in data[_Meta].Object)
            node.SetMeta(meta.Key, meta.Value.AsString());
        foreach (JsonValue group in data[_Group].Array)
            node.AddToGroup(group.AsString());

        foreach (JsonValue child in data[_Children].Array)
            node.AddChild(ConvertJsonToNode(child));

        if (node is INetworkData ind)
            ind.DeserializeNetworkData(data[_INetworkData], true);


        return node;
    }


    private static string RemoveNamespace(string name)
    {
        int index = name.RFind(".");
        if (index < 0)
            return name;
        else
            return name.Substring(index + 1, name.Length - (index + 1));
    }

    #endregion

    public void HandleEvent(Event e)
    {
        switch (e.IDAsEvent)
        {
            case EventID.OnConnectionRecievedData:
                CallDeferred(nameof(OnConnectionDataRecieved), (JsonValue)e.Parameter);
                break;
            case EventID.OnSocketRecievedData:
                CallDeferred(nameof(OnSocketDataRecieved), (JsonValue)e.Parameter);
                break;
        }
    }

}
public interface INetworkData
{
    public bool NetworkUpdate { get; set; }
    public JsonValue SerializeNetworkData(bool forceReturn = false, bool ignoreThisUpdateOccurred = false);
    public void DeserializeNetworkData(JsonValue data, bool firstDeserialize);
}

public static class NetworkExtensions
{
    public static bool ShouldUpdate(this INetworkData data, bool forceUpdate)
    {
        if(forceUpdate) return true;
        if (data.NetworkUpdate) return true;
        return false;
    }

    public static JsonValue CalculateNetworkReturn(this INetworkData data, JsonValue newData, bool ignoreThisUpdateOccured)
    {
        if(newData == null) return null;

        if (!ignoreThisUpdateOccured)
        {
            data.NetworkUpdate = false;
        }
        return newData;
    }       
}