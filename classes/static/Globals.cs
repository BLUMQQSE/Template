using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using static Globals;

public static class StringExtensions
{
    public static string RemovePath(this string path)
    {
        return path.Substring(path.RFind("/") + 1);
    }
    public static string RemoveFileType(this string path)
    {
        int index = path.LastIndexOf('.');
        return path.Substring(0, index);
    }
    public static string RemovePathAndFileType(this string path)
    {
        return path.RemovePath().RemoveFileType();
    }
}


public static class LayerExtensions
{
    /// <summary>
    /// Converts a layer to its corrisponding bit value. Example BlockLight -> 512
    /// </summary>
    public static uint ConvertToBitMask(this PhysicsLayer layer)
    {
        return (uint)Mathf.Pow(2, (uint)layer - 1);
    }

    /// <summary>
    /// Returns true if a collision layer is active on obj.
    /// </summary>
    public static bool CollisionLayerActive(this PhysicsLayer number, CollisionObject3D obj)
    {
        return obj.GetCollisionLayerValue((int)number);
    }

    public static bool CollisionMaskActive(this PhysicsLayer number, CollisionObject3D obj)
    {
        return obj.GetCollisionMaskValue((int)number);
    }
}

public static class NodeExtensions
{

    public static bool IsValid<T>(this T node) where T : GodotObject
    {
        return GodotObject.IsInstanceValid(node) && node != null;
    }
    /// <summary>
    /// Works for any node in a level, and players which have a LevelPartitionName meta.
    /// </summary>
    /// <returns></returns>
    public static string GetLevelName(this Node node)
    {
        if (node.HasMeta(Globals.Meta.LevelPartitionName.ToString()))
        {
            return node.GetMeta(Globals.Meta.LevelPartitionName.ToString()).ToString();
        }
        node.GetLevel();
        if(node == null)
        {
            return String.Empty;
        }
        return node.Name;
    }
    /// <summary>
    /// Will only find the level node of a node within the level. Will not work on players with a LevelPartitionName tag.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public static Node GetLevel(this Node node)
    {
        return CheckParent(node);
    }


    private static Node CheckParent(Node node)
    {
        if (node == node.GetTree().Root)
            return null;
        if (node.IsInGroup(Globals.Groups.Level.ToString()))
            return node;
        return CheckParent(node.GetParent());
    }

    public static bool IsOwnedBy(this Node node, Node potentialOwner)
    {
        Node temp = node;
        while(temp != potentialOwner.GetTree().Root)
        {
            if (temp.GetParent() == potentialOwner)
                return true;
            temp = node.GetParent();
        }
        return false;
    }
    /// <summary>
    /// Returns true if node is owned by the local machine. Expensive call, should be called in ready and stored in local
    /// bool values.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public static bool IsLocalOwned(this Node node)
    {
        Player p = node.FindParentOfType<Player>();
        if (p.IsValid())
        {
            ulong meta = ulong.Parse(p.GetMeta(Globals.Meta.OwnerId.ToString()).ToString());
            if (meta == NetworkManager.Instance.PlayerId)
                return true;

            return false;

        }
        else if(NetworkManager.Instance.IsServer)
            return true;
        
        return false;
    }

    public static T FindParentOfType<T>(this Node node)
    {
        if (node.GetType() == typeof(T))
        {
            return (T)(object)node;
        }
        if (node == node.GetTree().Root)
        {
            return default(T);
        }
        else
        {
            return FindParentOfType<T>(node.GetParent());
        }
    }

    public static bool Owns(this Node node, Node potentiallyOwned)
    {
        return potentiallyOwned.IsOwnedBy(node);
    }

    public static T IfValid<T>(this T node) where T : GodotObject
        => node.IsValid() ? node : null;

    /// <summary>
    /// Function for searching for child node of Type T. Removes need for searching for a
    /// specific name of a node, reducing potential errors in name checking being inaccurate.
    /// Supports checking 5 layers of nodes. This method is ineffecient, and should never be used repetitively 
    /// in _process.
    /// </summary>
    /// <returns>First instance of Type T</returns>
    public static T GetChildOfType<T>(this Node node)
    {
        if (node == null)
            return default(T);

        foreach (Node child in node.GetChildren())
            if (child is T)
                return (T)(object)child;

        return default(T);
    }

    /// <summary>
    /// Function for searching for children nodes of Type T.
    /// </summary>
    /// <returns>List of all instances of Type T</returns>
    public static List<T> GetChildrenOfType<T>(this Node node, bool recursive = false)
    {
        if (!recursive)
        {
            List<T> list = new List<T>();
            if (node == null)
                return list;

            foreach (Node child in node.GetChildren())
                if (child is T)
                    list.Add((T)(object)child);

            return list;
        }
        else
        {
            List<T> list = new List<T>();
            if (node == null)
                return list;

            foreach (Node child in node.GetChildren())
            {
                if (child is T)
                    list.Add((T)(object)child);
                GetChildrenOfType<T>(child, recursive);
            }
            return list;
        }
    }

    /// <summary>
    /// Function for searching for children nodes of Type T.
    /// </summary>
    /// <returns>List of all instances of Type T that are children or lower.</returns>
    public static List<T> GetAllChildrenOfType<T>(this Node node)
    {
        List<T> list = new List<T>();
        list.AddRange(GetChildrenOfType<T>(node));

        foreach (Node child in node.GetChildren())
        {
            list.AddRange(GetAllChildrenOfType<T>(child));
        }

        return list;
    }

    /// <summary>
    /// Function for searching for sibling node of Type T. Removes need for searching for a
    /// specific name of a node, reducing potential errors in name checking being inaccurate.
    /// </summary>
    /// <returns>First instance of Type T</returns>
    public static T GetSiblingOfType<T>(this Node node)
    {
        return node.GetParent().GetChildOfType<T>();
    }
    /// <summary>
    /// Function for searching for sibling nodes of Type T.
    /// </summary>
    /// <returns>List of all instances of Type T</returns>
    public static List<T> GetSiblingsOfType<T>(this Node node)
    {
        return node.GetParent().GetChildrenOfType<T>();
    }

}

public class ConsoleAttribute : Attribute
{

}

public static class Globals
{
    public enum Groups
    {
        AutoLoad,
        SelfOnly,
        NotPersistent,
        IgnoreChildren, // both save and network
        IgnoreChildrenSave,
        IgnoreChildrenNetwork,
        Outside,
        Level
    }

    public enum Meta
    {
        UniqueId,
        OwnerId,
        LevelPartitionName
    }
    public enum DataType
    {
        RpcCall,
        ClientInputUpdate,
        ServerUpdate,
        FullServerData,
        ServerAdd,
        ServerRemove,
        RequestForceUpdate
    }

    public enum PhysicsLayer
    {
        Neutral = 1,
        Player,
        Building,
        Ground
    }

        /// <summary>
       /// Returns true if every layer of mask2 is also on mask.
       /// </summary>
    public static bool LayersUnion(uint mask, uint mask2)
    {
        if (mask == mask2) return true;

        for (int i = 0; i < 32; i++)
        {
            if (((mask2 >> i) & 1) == 1)
            {
                if (((mask >> i) & 1) == 0)
                {
                    return false;
                }
            }
        }

        return true;
    }
    /// <summary>
    /// Returns true if any layer of mask2 is also on mask
    /// </summary>
    public static bool LayersIntersect(uint mask, uint mask2)
    {
        if (mask == mask2) return true;

        for (int i = 0; i < 32; i++)
        {
            if (((mask2 >> i) & 1) == 1)
            {
                if (((mask >> i) & 1) == 1)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static string RemoveNamespace(string name)
    {
        int index = name.RFind(".");
        if (index < 0)
            return name;
        else
            return name.Substring(index + 1, name.Length - (index + 1));
    }
}