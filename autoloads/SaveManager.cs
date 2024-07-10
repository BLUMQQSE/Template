using Godot;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

public partial class SaveManager : Node
{
    private static SaveManager instance;
    public static SaveManager Instance { get { return instance; } }
    public SaveManager()
    {
        instance = this;
        AddToGroup(Globals.Groups.AutoLoad.ToString());
    }

    /// <summary> Game starts up on 'static', this is the save file for no active game. </summary>
    public string CurrentSave { get; private set; } = "Static";
    /// <summary> Level a player spawns into for first time. </summary>
    public string PlayerStartLevel { get; private set; } = "StartUp";

    public event Action<string> SaveCreated;
    public event Action<string> SaveLoaded;
    public event Action GameSaved;
    public event Action<string> SavingSaveComplete;
    public event Action<string> ChangedSave;
    public event Action<Node> LoadedPlayer;
    public event Action<Node> LoadedLevel;
    /// <summary>
    /// Destination folder for different save items. This can be added to for new save locations. The enum will be converted
    /// to a string folder name when saving.
    /// </summary>
    public enum SaveDest
    {
        Level,
        Player,
        ECS
    }

    public override void _Ready()
    {
        base._Ready();
        CreateSave("Static");
    }

    public bool CreateSave(string saveName)
    {
        string priorSave = CurrentSave;
        if (FileManager.DirExists("saves/" + saveName))
        {
            FileManager.RemoveDir("/saves/" + saveName);
            /* could modify to return false, etc
            */    
        }
        CurrentSave = saveName;
        InstantiateScenesDir();
        InstantiateNewPlayerFile(NetworkManager.Instance.PlayerId.ToString(), NetworkManager.Instance.PlayerName);

        if (saveName != "Static")
            SaveCreated?.Invoke(saveName);

        ChangedSave?.Invoke(priorSave);
        return true;
    }

    public void SaveGame()
    {
        GameSaved?.Invoke();  
    }

    public bool LoadSave(string saveName)
    {
        if (!FileManager.DirExists("saves/" + saveName))
            return false;
        string priorSave = CurrentSave;

        CurrentSave = saveName;
        ChangedSave?.Invoke(priorSave);
        SaveLoaded?.Invoke(saveName);  
        return true;
    }

    public void Save(Node rootNode, SaveDest dest)
    {
        JsonValue data = ConvertNodeToJson(rootNode);
        string name = rootNode.Name;
        if (dest == SaveDest.Player)
            name = rootNode.GetMeta(Globals.Meta.OwnerId.ToString()).ToString();

        SaveData(name, data, dest);
    }
    public void SaveData(string saveName, JsonValue data, SaveDest dest)
    {
        string folderName = dest.ToString();
        FileManager.SaveToFileFormattedAsync(data, "saves/" + CurrentSave + "/" + folderName + "/" + saveName);
    }
    public Node Load(string fileName, SaveDest dest)
    {

        JsonValue data = LoadData(fileName, dest);
        Node node = ConvertJsonToNode(data);
        if (dest == SaveDest.Player)
            LoadedPlayer?.Invoke(node);
        else if (dest == SaveDest.Level)
            LoadedLevel?.Invoke(node);

        return node;
    }
    public JsonValue LoadData(string fileName, SaveDest dest)
    {
        string fileHolder = dest.ToString();
        return FileManager.LoadFromFile("saves/" + CurrentSave + "/" + fileHolder + "/" + fileName);
    }

    private void InstantiateNewPlayerFile(string fileName, string playerName)
    {
        Node player = GD.Load<PackedScene>(ResourceManager.Instance.GetScenePath("Player")).Instantiate();
        player.Name = playerName;
        player.SetMeta(Globals.Meta.OwnerId.ToString(), fileName);
        player.SetMeta(Globals.Meta.LevelPartitionName.ToString(), PlayerStartLevel);

        Save(player, SaveDest.Player);
    }

    private void InstantiateScenesDir()
    {
        foreach (KeyValuePair<string, string> filePath in ResourceManager.Instance.LevelPaths)
        {
            if (filePath.Key == "Main")
                continue;
            // useful for when making removals of levels while developing
            if (!ResourceLoader.Exists(filePath.Value))
                continue;
            Node root = GD.Load<PackedScene>(filePath.Value).Instantiate() as Node;
            root.AddToGroup(Globals.Groups.Level.ToString());

            NetworkDataManager.Instance.ApplyNextAvailableUniqueId(root);

            JsonValue sceneData = SaveManager.ConvertNodeToJson(root);
            AddHash(ref sceneData);
            FileManager.SaveToFileFormatted(sceneData, "saves/" + CurrentSave + "/"+SaveDest.Level+"/" + root.Name);

            root.QueueFree();
        }
    }

    #region HASH
    static string GetHash(string inputString)
    {
        byte[] hashBytes;
        using (HashAlgorithm algorithm = SHA256.Create())
            hashBytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));

        return BitConverter
                .ToString(hashBytes)
                .Replace("-", String.Empty);
    }

    static private void AddHash(ref JsonValue obj)
    {
        obj.Remove("hash");
        string hash = GetHash(obj.ToString());
        obj["hash"].Set(hash);
    }
    private bool HashMatches(JsonValue obj)
    {
        string hashStored = obj["hash"].AsString();
        obj.Remove("hash");

        return hashStored == GetHash(obj.ToString());
    }

    #endregion


    #region Converting Data

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
    private static readonly string _ISaveData = "ISaveData";

    public static JsonValue ConvertNodeToJson(Node node)
    {
        JsonValue val = CollectNodeData(node);
        return val;
    }

    private static JsonValue CollectNodeData(Node node)
    {
        JsonValue jsonNode = new JsonValue();

        if (node.IsInGroup(Globals.Groups.NotPersistent.ToString()) || node.IsInGroup(Globals.Groups.SelfOnly.ToString()))
            return new JsonValue();
        if (node.GetParent().IsValid())
            if (node.GetParent().IsInGroup(Globals.Groups.IgnoreChildren.ToString()) ||
                node.GetParent().IsInGroup(Globals.Groups.IgnoreChildrenSave.ToString()))
                return new JsonValue();

        jsonNode[_Name].Set(node.Name);
        jsonNode[_Type].Set(Globals.RemoveNamespace(node.GetType().ToString()));
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
            if (meta == Globals.Meta.UniqueId.ToString())
                continue;
            jsonNode[_Meta][meta].Set((string)node.GetMeta(meta));
        }
        foreach (string group in node.GetGroups()) // not accessible outside main
            jsonNode[_Group].Append(group);

        for (int i = 0; i < node.GetChildCount(); i++) // not accessible outside main
            jsonNode[_Children].Append(CollectNodeData(node.GetChild(i)));

        if (node is ISaveData)
            jsonNode[_ISaveData].Set((node as ISaveData).SerializeSaveData());

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
            c.Scale = data[_Scale].AsVector2();
            c.Rotation = data[_Rotation].AsFloat();
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
        }

        node = GodotObject.InstanceFromId(nodeID) as Node;

        foreach (KeyValuePair<string, JsonValue> meta in data[_Meta].Object)
            node.SetMeta(meta.Key, meta.Value.AsString());
        foreach (JsonValue group in data[_Group].Array)
            node.AddToGroup(group.AsString());

        foreach (JsonValue child in data[_Children].Array)
            node.AddChild(ConvertJsonToNode(child));

        if (node is ISaveData)
            (node as ISaveData).DeserializeSaveData(data[_ISaveData]);
        return node;
    }

    #endregion

}

public interface ISaveData
{
    public JsonValue SerializeSaveData();
    public void DeserializeSaveData(JsonValue data);
}
