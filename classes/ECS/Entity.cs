using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;

public partial class Entity : Node3D, INetworkData
{
    private static uint nextAvailableId = 0;
    public uint ID { get; set; }
    public bool NetworkUpdate { get; set; } = true;

    private List<Component> components = new List<Component>();
    private Dictionary<Type, Component> componentLookup = new Dictionary<Type, Component>();

    public event Action<Entity, Component> ComponentAdded;
    public event Action<Entity, Component> ComponentRemoved;

    public Entity()
    {
        this.Name = "Unnamed";
        ID = nextAvailableId;
        nextAvailableId++;

    }
    public Entity(string name)
    {
        this.Name = name;
        ID = nextAvailableId;
        nextAvailableId++;
    }

    public override void _Ready()
    {
        base._Ready();
        SetProcess(false);
        SetPhysicsProcess(false);
        SetMeta("EntityID", ID);
    }


    public bool HasComponent(Type c)
    {
        if (componentLookup.ContainsKey(c)) return true;
        return false;
    }
    public bool HasComponent<T>() where T : Component
    {
        if (componentLookup.ContainsKey(typeof(T))) return true;
        return false;
    }
    public T GetComponent<T>() where T : Component
    {
        if (HasComponent<T>())
            return (T)componentLookup[typeof(T)];
        return default(T);
    }
    public void AddComponent(Component x)
    {
        RemoveComponent(x.GetType());
        x.Init(this);
        components.Add(x);
        componentLookup.Add(x.GetType(), x);
        ComponentAdded?.Invoke(this, x);
    }
    public void RemoveComponent<T>() where T : Component
    {
        if (!HasComponent<T>())
            return;

        Component c = GetComponent<T>();
        c.Destroy();
        components.Remove(c);
        componentLookup.Remove(typeof(T));
        ComponentRemoved?.Invoke(this, c);
    }
    private void RemoveComponent(Type t)
    {
        if (componentLookup.ContainsKey(t))
        {
            Component c = componentLookup[t];
            c.Destroy();
            components.Remove(c);
            componentLookup.Remove(t);
            ComponentRemoved?.Invoke(this, c);
        }
    }

    public JsonValue SerializeData()
    {
        JsonValue data = new JsonValue();
        
        data["Name"].Set(Name);
        data["Position"].Set(Position);
        data["Rotation"].Set(Rotation);
        foreach (var meta in GetMetaList())
        {
            JsonValue mD = new JsonValue();
            mD["Name"].Set(meta);
            mD["Value"].Set(GetMeta(meta).ToString());
            data["Meta"].Append(mD);
        }
        foreach (var component in components)
        {
            JsonValue cD = new JsonValue();
            cD["Name"].Set(component.GetType().ToString());
            cD["Value"].Set(component.SerializeData());
            data["Comps"].Append(cD);
        }
        return data;
    }
    public void DeserializeData(JsonValue data)
    {
        Name = data["Name"].AsString();
        Position = data["Position"].AsVector3();
        Rotation = data["Rotation"].AsVector3();

        foreach (var meta in data["Meta"].Array)
        {
            SetMeta(meta["Name"].AsString(), meta["Value"].AsString());
        }
        foreach (var component in data["Comps"].Array)
        {
            string compName = component["Name"].AsString();
            Type t = Type.GetType(compName);
            Component c = (Component)Activator.CreateInstance(t);
            c.DeserializeData(component["Value"]);
            AddComponent(c);
        }
    }
    /// <summary>
    /// Used by EntityContainer to add components without initializing them
    /// </summary>
    /// <param name="x"></param>
    protected void AddComponentOnStartup(Component x)
    {
        components.Add(x);
    }

    public JsonValue SerializeNetworkData(bool forceReturn = false, bool ignoreThisUpdateOccurred = false)
    {
        JsonValue data = new JsonValue();

        data["Name"].Set(Name);
        if (!HasComponent<NetworkTransformComponent>())
        {
            data["Position"].Set(Position);
            data["Rotation"].Set(Rotation);
        }
        foreach (var meta in GetMetaList())
        {
            JsonValue mD = new JsonValue();
            mD["Name"].Set(meta);
            mD["Value"].Set(GetMeta(meta).ToString());
            data["Meta"].Append(mD);
        }
        foreach (var component in components)
        {
            JsonValue cD = new JsonValue();
            cD["Name"].Set(component.GetType().ToString());
            cD["Value"].Set(component.SerializeData());
            data["Comps"].Append(cD);
        }
        return data;
    }

    public void DeserializeNetworkData(JsonValue data, bool firstDeserialize)
    {
        Name = data["Name"].AsString();
        if (data["Position"].IsValue)
            Position = data["Position"].AsVector3();
        if (data["Rotation"].IsValue)
            Rotation = data["Rotation"].AsVector3();

        foreach (var meta in data["Meta"].Array)
        {
            SetMeta(meta["Name"].AsString(), meta["Value"].AsString());
        }
        foreach (var component in data["Comps"].Array)
        {
            string compName = component["Name"].AsString();
            Type t = Type.GetType(compName);
            Component c = (Component)Activator.CreateInstance(t);
            c.DeserializeData(component["Value"]);
            AddComponent(c);
        }
    }
}
