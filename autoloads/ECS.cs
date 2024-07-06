using Godot;
using System;
using System.Collections.Generic;

public partial class ECS : Node, INetworkData
{
    private static ECS instance;
    public static ECS Instance {  get { return instance; } }

    public bool NetworkUpdate { get; set; } = true;

    public ECS() { instance = this; }

    public event Action<Entity> EntityAdded;
    public event Action<Entity> EntityRemoved;
    public event Action<Entity, Component> EntityComponentAdded;
    public event Action<Entity, Component> EntityComponentRemoved;

    List<Entity> entities = new List<Entity>(); 
    private List<ISystem> serverSystems = new List<ISystem>();
    private List<ISystem> clientSystems = new List<ISystem>();

    public enum SystemType
    {
        Server,
        Client,
        Both
    }

    public override void _Ready()
    {
        base._Ready();

        // since it's an autload, need to explicitly add to network nodes
        NetworkDataManager.Instance.AddNetworkNodes(this);
        
        AddSystem(new MovementSystem(), SystemType.Server);
        AddSystem(new UpdateNetworkTransforms(), SystemType.Client);
        AddSystem(new VisibleSystem(), SystemType.Both);
        /*
        Entity e = new Entity("Moose");
        AddEntity(e);
        e.AddComponent(new MeshComponent(new BoxMesh()));
        e.AddComponent(new MovementComponent(5, new Vector3(1, 0, 0)));
        Entity x = new Entity("Player");
        AddEntity(x);
        x.AddComponent(new PlayerComponent());
    */
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if(NetworkManager.Instance.IsServer)
            foreach (var system in serverSystems)
                system.Process(delta);
        else
            foreach (var system in clientSystems)
                system.Process(delta);
    }
    /// <summary>
    /// Method used to check the world for entities which contain 'with' Components.
    /// List<Entity> result = World.Instance.Query(new Type[] { typeof(MoveComponent) });
    /// </summary>
    public List<Entity> Query(Type[] with)
    {
        List<Entity> result = new List<Entity>();
        if (with.Length == 0)
        {
            result = entities;
            return result;
        }
        foreach (var ent in this.entities)
        {
            bool add = true;
            foreach (var comp in with)
            {
                if (!ent.HasComponent(comp))
                {
                    add = false;
                    break;
                }
            }
            if (add)
                result.Add(ent);
        }
        return result;
    }
    /// <summary>
    /// Method used to check the world for entities which contain 'with' Components and do not contain 'without' components.
    /// List<Entity> result = World.Instance.Query(new Type[] { typeof(MoveComponent) }, new Type[] { typeof(MoveComponent) });
    /// </summary>
    public List<Entity> Query(Type[] with, Type[] without)
    {
        List<Entity> result = new List<Entity>();
        foreach (var ent in this.entities)
        {
            bool add = true;

            if (with.Length > 0)
            {
                foreach (var comp in with)
                {
                    if (!ent.HasComponent(comp))
                    {
                        add = false;
                        break;
                    }
                }
            }
            if (!add)
                continue;

            foreach (var comp in without)
            {
                if (ent.HasComponent(comp))
                {
                    add = false;
                    break;
                }
            }
            if (add)
                result.Add(ent);
        }
        return result;
    }
    public void AddSystem(ISystem system, SystemType type)
    {
        if(type == SystemType.Server || type == SystemType.Both)
            serverSystems.Add(system);
        if(type == SystemType.Client || type == SystemType.Both)
            clientSystems.Add(system);
        system.Start();
    }
    public void RemoveSystem(ISystem system, SystemType type)
    {
        if (type == SystemType.Server || type == SystemType.Both)
            serverSystems.Remove(system);
        if (type == SystemType.Client || type == SystemType.Both)
            clientSystems.Remove(system);
        system.Stop();
    }
    public void AddEntity(Entity entity)
    {
        if (entity.GetParent().IsValid())
            entity.CallDeferred("reparent", this);
        else
            NetworkDataManager.Instance.AddServerNode(this, entity);
        entities.Add(entity);
        entity.ComponentAdded += ComponentAdded;
        entity.ComponentRemoved += ComponentRemoved;
        EntityAdded?.Invoke(entity);
        NetworkUpdate = true;
    }
    public void RemoveEntity(Entity entity)
    {
        NetworkDataManager.Instance.RemoveServerNode(entity);
        entities.Remove(entity);
        entity.ComponentAdded -= ComponentAdded;
        entity.ComponentRemoved -= ComponentRemoved;
        EntityRemoved?.Invoke(entity);
        NetworkUpdate = true;
    }

    public void SaveECS()
    {
        JsonValue data = new JsonValue();
        foreach (var ent in entities)
        {
            if (ent.HasComponent<PlayerComponent>())
                continue;
            data["E"][ent.Name].Set(uint.Parse(ent.GetMeta(Globals.Meta.UniqueId.ToString()).ToString()));
        }
        SaveManager.Instance.SaveData("ecs", data, SaveManager.SaveDest.ECS);
    }

    public void LoadECS()
    {
        JsonValue data = SaveManager.Instance.LoadData("ecs", SaveManager.SaveDest.ECS);
        foreach (var ent in data["E"].Object)
        {
            Entity e = new Entity();
            e.Name = ent.Key;
            e.DeserializeData(ent.Value);
            AddEntity(e);
        }
    }

    private void ComponentAdded(Entity entity, Component component)
    {
        NetworkUpdate = true;
        EntityComponentAdded?.Invoke(entity, component);
    }
    private void ComponentRemoved(Entity entity, Component component)
    {
        NetworkUpdate = true;
        EntityComponentRemoved?.Invoke(entity, component);
    }

    public JsonValue SerializeNetworkData(bool forceReturn = false, bool ignoreThisUpdateOccurred = false)
    {
        if (!this.ShouldUpdate(forceReturn))
            return null;
        JsonValue data = new JsonValue();
        foreach(var ent in entities)
        {
            data["E"][ent.Name].Set(uint.Parse(ent.GetMeta(Globals.Meta.UniqueId.ToString()).ToString()));
        }
        return data;
    }

    public void DeserializeNetworkData(JsonValue data, bool firstDeserialize)
    {
        // ugly but can be improved
        List<Entity> ents = new List<Entity>(data["E"].Count);
        foreach(var ent in data["E"].Object)
        {
            Entity e = NetworkDataManager.Instance.UniqueIdToNode(ent.Value.AsUInt()) as Entity;
            ents.Add(e);
        }

        List<Entity> entitiesToRemove = new List<Entity>();
        foreach(var ent in entities)
        {
            if(!ents.Contains(ent))
                entitiesToRemove.Add(ent);
        }

        foreach(var ent in entitiesToRemove)
            RemoveEntity(ent);

        foreach(var ent in ents)
        {
            if(!entities.Contains(ent))
                AddEntity(ent);
        }

    }

}
