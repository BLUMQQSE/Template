using System;
using System.Collections.Generic;
using Godot;

public struct Query
{
    public List<Entity> associatedEntities;
    public List<Type> presentComponents;
    public List<Type> absentComponents;
    public Query()
    {
        associatedEntities = new List<Entity>();
        presentComponents = new List<Type>();
        absentComponents = new List<Type>();
    }
    public Query(List<Type> presentComponents)
    {
        associatedEntities = new List<Entity>();
        this.presentComponents = presentComponents;
        absentComponents = new List<Type>();
    }
    public Query(List<Type> presentComponents, List<Type> absentComponents)
    {
        associatedEntities = new List<Entity>();
        this.presentComponents = presentComponents;
        this.absentComponents = absentComponents;
    }
}

public interface ISystem
{
    public Dictionary<string, Query> Queries { get; set; }
    public void Start()
    {
        ECS.Instance.EntityAdded += EntityAdded;
        ECS.Instance.EntityRemoved += EntityRemoved;
        ECS.Instance.EntityComponentAdded += ComponentAdded;
        ECS.Instance.EntityComponentRemoved += ComponentRemoved;

        List<string> keys = new List<string>(Queries.Keys);
        foreach (string key in keys)
        {
            Query q = Queries[key];
            q.associatedEntities = ECS.Instance.Query(q.presentComponents.ToArray(), q.absentComponents.ToArray());
            Queries[key] = q;
        }
    }
    void EntityAdded(Entity entity)
    {
        List<string> keys = new List<string>(Queries.Keys);
        foreach (string key in keys)
        {
            if (!Queries[key].associatedEntities.Contains(entity) && EntityIsCompatible(entity, Queries[key]))
            {
                Query q = Queries[key];
                q.associatedEntities.Add(entity);
                Queries[key] = q;
            }
        }
    }

    void EntityRemoved(Entity entity)
    {
        List<string> keys = new List<string>(Queries.Keys);
        foreach (string key in keys)
        {
            if (Queries[key].associatedEntities.Contains(entity))
            {
                Query q = Queries[key];
                q.associatedEntities.Remove(entity);
                Queries[key] = q;
            }
        }
    }
    public virtual void Stop()
    {
        ECS.Instance.EntityComponentAdded -= ComponentAdded;
        ECS.Instance.EntityComponentRemoved -= ComponentRemoved;
        ECS.Instance.EntityAdded -= EntityAdded;
        ECS.Instance.EntityRemoved -= EntityRemoved;
        Queries.Clear();
    }

    void ComponentAdded(Entity entity, Component component)
    {
        List<string> keys = new List<string>(Queries.Keys);
        foreach (string key in keys)
        {
            if (Queries[key].associatedEntities.Contains(entity) && Queries[key].absentComponents.Contains(component.GetType()))
            {
                Query q = Queries[key];
                q.associatedEntities.Remove(entity);
                Queries[key] = q;
            }
            else if (!Queries[key].associatedEntities.Contains(entity) && Queries[key].presentComponents.Contains(component.GetType()))
            {
                if (EntityIsCompatible(entity, Queries[key]))
                {
                    Query query = Queries[key];
                    query.associatedEntities.Add(entity);
                    Queries[key] = query;
                }
            }
        }
    }
    void ComponentRemoved(Entity entity, Component component)
    {
        List<string> keys = new List<string>(Queries.Keys);
        foreach (string key in keys)
        {
            if (Queries[key].associatedEntities.Contains(entity) && Queries[key].presentComponents.Contains(component.GetType()))
            {
                Query q = Queries[key];
                q.associatedEntities.Remove(entity);
                Queries[key] = q;
            }
            else if (!Queries[key].associatedEntities.Contains(entity) && Queries[key].absentComponents.Contains(component.GetType()))
            {
                if (EntityIsCompatible(entity, Queries[key]))
                {
                    Query q = Queries[key];
                    q.associatedEntities.Add(entity);
                    Queries[key] = q;
                }
            }
        }
    }

    private bool EntityIsCompatible(Entity entity, Query query)
    {
        bool compatible = true;
        foreach (var comp in query.presentComponents)
        {
            if (!entity.HasComponent(comp))
            {
                compatible = false;
                break;
            }
        }
        if (!compatible)
            return false;

        foreach (var comp in query.absentComponents)
        {
            if (entity.HasComponent(comp))
            {
                compatible = false;
                break;
            }
        }
        if (compatible)
            return true;
        return false;
    }
    public abstract void Process(double delta);
}
#region Network

public class UpdateNetworkTransforms : ISystem
{
    private static readonly string _Query = "Q";
    public Dictionary<string, Query> Queries { get; set; } = new Dictionary<string, Query>()
    {
        { "Q", new Query(new List<Type>() { typeof(NetworkTransformComponent) } ) }
    };

    public void Process(double delta)
    {
        foreach (Entity ent in Queries[_Query].associatedEntities)
        {
            NetworkTransformComponent ntc = ent.GetComponent<NetworkTransformComponent>();
            
            ent.Position = ent.Position.Lerp(ntc.SyncPos, 5 * (float)delta);
        }
    }
}

#endregion
#region Physics

public class MovementSystem : ISystem
{
    private static readonly string _MovementQuery = "MQ";
    public Dictionary<string, Query> Queries { get; set; } = new Dictionary<string, Query>()
    {
        { "MQ", new Query(new List<Type>() { typeof(MovementComponent) }) }
    };

    public void Process(double delta)
    {
        foreach (Entity entity in Queries[_MovementQuery].associatedEntities)
        {
            entity.Position +=
                entity.GetComponent<MovementComponent>().MovementDirection.Normalized() *
                entity.GetComponent<MovementComponent>().Speed * (float)delta;
        }
    }
}

#endregion

#region Optimization

public class VisibleSystem : ISystem
{
    private static readonly string _MeshQuery = "MQ";
    private static readonly string _PlayerQuery = "PQ";
    public Dictionary<string, Query> Queries { get; set; } = new Dictionary<string, Query>()
    {
        {"MQ", new Query(new List<Type>() {typeof(MeshComponent)}) },
        {"PQ", new Query(new List<Type> {typeof(PlayerComponent)}) }
    };

    public void Process(double delta)
    {
        foreach(var ent in Queries[_MeshQuery].associatedEntities)
        {
            foreach(var player in Queries[_PlayerQuery].associatedEntities)
            {
                if(ent.GlobalPosition.DistanceSquaredTo(player.GlobalPosition) > Mathf.Pow(20, 2))
                    ent.Visible = false;
                else
                    ent.Visible = true; 
            }
        }
    }
}

#endregion