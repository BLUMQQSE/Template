using Godot;
using System;
using System.Collections.Generic;

public partial class LevelManager : Node
{
    private static LevelManager instance;
    public static LevelManager Instance {  get { return instance; } }

    public LevelManager()
    {
        instance = this;
        AddToGroup(Globals.Groups.AutoLoad.ToString());
        PositionsOccupied = new bool[MaxConcurrentLevels];
    }

    /// <summary> Contains a list of all levels which exist in the current save. </summary>
    private List<string> AllLevels = new List<string>();

    private Dictionary<string, LevelPartition> ActiveLevels = new Dictionary<string, LevelPartition>();

    bool[] PositionsOccupied;
    /// <summary> Max number of levels open at the same time. Limiting this number will improve performance. </summary>
    private int MaxConcurrentLevels { get; set; } = 4;
    private bool UseOffsets { get; set; } = true;

    public event Action<Player> PlayerInstantiated;

    public override void _Ready()
    {
        base._Ready();
        SaveManager.Instance.GameSaved += OnGameSaved;
        SaveManager.Instance.ChangedSave += OnChangedSave;
        SaveManager.Instance.SaveLoaded += OnSaveLoaded;

        CollectAllLevels();

        LoadLevelPartition("MainMenu");
    }


    public override void _ExitTree()
    {
        base._ExitTree();
        SaveManager.Instance.GameSaved -= OnGameSaved;
        SaveManager.Instance.ChangedSave -= OnChangedSave;
        SaveManager.Instance.SaveLoaded -= OnSaveLoaded;
    }

    public bool LevelExists(string name)
    {
        return AllLevels.Contains(name);
    }
    public bool LevelActive(string name)
    {
        return ActiveLevels.ContainsKey(name);
    }

    public void SaveAllLevels()
    {
        foreach (var val in ActiveLevels.Keys)
            SaveLevelPartition(val);
    }


    public void SaveLevelPartition(string levelName)
    {
        if (!ActiveLevels.ContainsKey(levelName)) { return; }

        var level = ActiveLevels[levelName];
        if (level.Root is Node2D r2)
            r2.Position -= new Vector2(level.Offset.X, level.Offset.Y);
        if (level.Root is Node3D r3)
            r3.Position -= level.Offset;


        SaveManager.Instance.Save(level.Root, SaveManager.SaveDest.Level);

        if (level.Root is Node2D r22)
            r22.Position += new Vector2(level.Offset.X, level.Offset.Y);
        if (level.Root is Node3D r33)
            r33.Position += level.Offset;

        foreach (Node p in level.LocalPlayers)
        {
            if (p is Node2D p2)
                p2.Position -= new Vector2(level.Offset.X, level.Offset.Y);
            if (p is Node3D p3)
                p3.Position -= level.Offset;

            SaveManager.Instance.Save(p, SaveManager.SaveDest.Player);

            if (p is Node2D p22)
                p22.Position += new Vector2(level.Offset.X, level.Offset.Y);
            if (p is Node3D p33)
                p33.Position += level.Offset;
        }
    }
    public void LoadLevelPartition(string levelName)
    {
        if (ActiveLevels.ContainsKey(levelName))
            return;

        if (ActiveLevels.Count >= MaxConcurrentLevels)
        {
            GD.Print("Attempting to load too many scenes, reach MaxConcurrentScenes limit of " + MaxConcurrentLevels);
            return;
        }
        Node node = SaveManager.Instance.Load(levelName, SaveManager.SaveDest.Level);

        NetworkDataManager.Instance.ApplyNextAvailableUniqueId(node);
        Vector3 offset = Vector3.Zero;
        // only apply a offset if this scene is not a Control and we want to use offsets
        int offsetIndex = -1;
        if (UseOffsets)
            if (node is not Control)
            {
                for (int i = 0; i < PositionsOccupied.Length; i++)
                {
                    if (PositionsOccupied[i] == false)
                    {
                        PositionsOccupied[i] = true;
                        offset = new Vector3(i * 5000, 0, 0);
                        offsetIndex = i;
                        break;
                    }
                }

            }

        LevelPartition lp = new LevelPartition(node, offset);
        lp.PositionIndex = offsetIndex;

        ActiveLevels.Add(levelName, lp);

        if (node is Node3D n3)
            n3.Position += offset;
        else if (node is Node2D n2)
            n2.Position += new Vector2(offset.X, offset.Z);
        NetworkDataManager.Instance.AddServerNode(GetTree().CurrentScene, node);
    }

    public void SaveAndCloseLevelPartition(string levelName)
    {
        if (!ActiveLevels.ContainsKey(levelName)) { return; }
        SaveLevelPartition(levelName);
        CloseLevel(levelName);
    }

    public void CloseLevel(string levelName)
    {
        if (!ActiveLevels.ContainsKey(levelName)) { return; }
        if (ActiveLevels[levelName].PositionIndex != -1)
            PositionsOccupied[ActiveLevels[levelName].PositionIndex] = false;
        NetworkDataManager.Instance.RemoveServerNode(ActiveLevels[levelName].Root);
        ActiveLevels.Remove(levelName);
    }

    public bool HasLocalPlayers(string sceneName)
    {
        if (ActiveLevels.ContainsKey(sceneName))
            return ActiveLevels[sceneName].LocalPlayers.Count > 0;

        return false;
    }

    /// <summary>
    /// Converts a location in local units to scene's true position.
    /// Eg. Local Pos: (0, 20, 0), Scene Pos: (5000, 20, 0)
    /// </summary>
    /// <returns></returns>
    public Vector3 LocalPos2ScenePos(Vector3 position, string scene)
    {
        return position + ActiveLevels[scene].Offset;
    }
    /// <summary>
    /// Moves a player from one level to another. Will remove any positional offsets from the first level and apply the offset of 
    /// the new level.
    /// </summary>
    /// <param name="firstLoad">If true, this is the first time the player is being loaded into the game.</param>
    public void TransferPlayer(Player player, string level, bool firstLoad)
    {
        if (!ActiveLevels.ContainsKey(level))
        {
            GD.Print("Requesting to load " + level);
            LoadLevelPartition(level);
            if (!ActiveLevels.ContainsKey(level))
            {
                GD.Print("Level: " + level + " could not be loaded");
                return;
            }
        }

        string currentLevel = player.GetMeta(Globals.Meta.LevelPartitionName.ToString()).ToString();

        if (firstLoad)
        {
            
            ActiveLevels[level].AddPlayer(player);
            player.Position += ActiveLevels[currentLevel].Offset;

            NetworkDataManager.Instance.AddServerNode(GetTree().CurrentScene, player);

            return;
        }

        player.Position -= ActiveLevels[currentLevel].Offset;
        ActiveLevels[currentLevel].RemovePlayer(player);
        ActiveLevels[level].AddPlayer(player);
        player.Position = ActiveLevels[level].Offset;
        
        if (ActiveLevels[currentLevel].LocalPlayers.Count == 0)
            SaveAndCloseLevelPartition(currentLevel);

    }
    /// <summary>
    /// Instantiates a Player into the game. If the player does not have a save file, a new player will be created and saved.
    /// </summary>
    public Node InstantiatePlayer(ulong ownerId, string name)
    {
        if (FileManager.FileExists("saves/" + SaveManager.Instance.CurrentSave + "/" + SaveManager.SaveDest.Player + "/" + ownerId.ToString()))
        {
            GD.Print("loading " + name + " from file");
            Player myPlayer = SaveManager.Instance.Load(ownerId.ToString(), SaveManager.SaveDest.Player) as Player;
            PlayerInstantiated?.Invoke(myPlayer);
            TransferPlayer(myPlayer, myPlayer.GetLevelName(), true);

            return myPlayer;
        }

        // everything below is only called once on client first join

        GD.Print("loading " + name + " for first time");
        string levelPartition = SaveManager.Instance.PlayerStartLevel;
        Player player = GD.Load<PackedScene>(ResourceManager.Instance.GetScenePath("Player")).Instantiate<Player>();
        player.SetMeta(Globals.Meta.OwnerId.ToString(), ownerId.ToString());
        player.SetMeta(Globals.Meta.LevelPartitionName.ToString(), levelPartition);
        player.Name = name;

        SaveManager.Instance.Save(player, SaveManager.SaveDest.Player);


        if (!player.HasMeta(Globals.Meta.LevelPartitionName.ToString()))
            player.SetMeta(Globals.Meta.LevelPartitionName.ToString(), SaveManager.Instance.PlayerStartLevel);

        NetworkDataManager.Instance.ApplyNextAvailableUniqueId(player);
        string scenePartition = player.GetMeta(Globals.Meta.LevelPartitionName.ToString()).ToString();
        PlayerInstantiated?.Invoke(player); 
        TransferPlayer(player, scenePartition, true);

        return player;
    }

    public void CheckAddNode(Node owner, Node node)
    {
        if (node.HasMeta(Globals.Meta.OwnerId.ToString()))
        {
            if (!node.HasMeta(Globals.Meta.LevelPartitionName.ToString()))
                node.SetMeta(Globals.Meta.LevelPartitionName.ToString(), SaveManager.Instance.PlayerStartLevel);

            string sceneName = node.GetMeta(Globals.Meta.LevelPartitionName.ToString()).ToString();

            if (node is Node2D n2)
                n2.Position += new Vector2(ActiveLevels[sceneName].Offset.X, ActiveLevels[sceneName].Offset.Y);
            if (node is Node3D n3)
                n3.Position += ActiveLevels[sceneName].Offset;

            ActiveLevels[sceneName].AddPlayer(node);
        }
    }

    public void CheckRemoveNode(Node node)
    {
        if (node.HasMeta(Globals.Meta.OwnerId.ToString()))
        {
            string sceneName = node.GetMeta(Globals.Meta.LevelPartitionName.ToString()).ToString();

            if (node is Node2D n2)
                n2.Position -= new Vector2(ActiveLevels[sceneName].Offset.X, ActiveLevels[sceneName].Offset.Y);
            if (node is Node3D n3)
                n3.Position -= ActiveLevels[sceneName].Offset;

            SaveManager.Instance.Save(node, SaveManager.SaveDest.Player);

            ActiveLevels[sceneName].RemovePlayer(node);
        }
    }

    private void CollectAllLevels()
    {
        AllLevels.Clear();
        List<string> list = FileManager.GetFiles("saves/" + SaveManager.Instance.CurrentSave +"/"+ SaveManager.SaveDest.Level);
        for (int i = 0; i < list.Count; i++)
        {
            int lastIndex = list[i].Find(".");
            list[i] = list[i].Substring(0, lastIndex);
        }
        AllLevels = list;
    }


    private void OnChangedSave(string obj)
    {
        List<string> activeLevels = new List<string>(ActiveLevels.Keys.Count);
        foreach(var level in ActiveLevels.Keys)
            activeLevels.Add(level);

        foreach(var level in activeLevels)
            SaveAndCloseLevelPartition(level);
    }

    private void OnSaveLoaded(string obj)
    {
        CollectAllLevels();
    }

    private void OnGameSaved()
    {
        SaveAllLevels();
    }

}

public class LevelPartition
{
    public LevelPartition(Node root, Vector3 offset)
    {
        Root = root;
        Offset = offset;
        PositionIndex = (int)(offset.X / 5000f);
    }

    public Node Root { get; private set; }
    public Vector3 Offset { get; private set; }
    public List<Node> LocalPlayers { get; private set; } = new List<Node>();

    public void AddPlayer(Node player)
    {
        LocalPlayers.Add(player);
        player.SetMeta(Globals.Meta.LevelPartitionName.ToString(), Root.Name);
    }
    public void RemovePlayer(Node player)
    {
        LocalPlayers.Remove(player);
    }
    public int PositionIndex { get; set; } = -1;

}