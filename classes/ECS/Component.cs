using Godot;
using System;
using System.Collections.Generic;
public class Component
{
    public Entity Entity { get; set; }
    public virtual void Init(Entity entity)
    {
        this.Entity = entity;
    }
    public virtual void Destroy()
    {

    }
    public virtual JsonValue SerializeData() { return null; }
    public virtual void DeserializeData(JsonValue data) { }
}
#region Network

public class NetworkTransformComponent : Component
{
    public Vector3 SyncPos { get; set; }
    public Vector3 SyncRot { get; set; }

    public override void Init(Entity entity)
    {
        base.Init(entity);
    }
    private static readonly string _SP = "SP";
    private static readonly string _SR = "SR";
    public override JsonValue SerializeData()
    {
        JsonValue data = new JsonValue();
        data[_SP].Set(SyncPos);
        data[_SR].Set(SyncRot);
        return data;
    }
    public override void DeserializeData(JsonValue data)
    {
        SyncPos = data[_SP].AsVector3();
        SyncRot = data[_SR].AsVector3();
    }
}

#endregion

#region Physics
public class MovementComponent : Component
{
    public float Speed { get; set; }
    public Vector3 MovementDirection { get; set; }
    public MovementComponent() { }
    public MovementComponent(float speed, Vector3 movementDirection)
    {
        Speed = speed;
        MovementDirection = movementDirection;
    }
    public override void DeserializeData(JsonValue data)
    {
        MovementDirection = data["MovementDirection"].AsVector3();
        Speed = data["Speed"].AsFloat();
    }
    public override JsonValue SerializeData()
    {
        JsonValue data = new JsonValue();
        data["MovementDirection"].Set(MovementDirection);
        data["Speed"].Set(Speed);
        return data;
    }
}
#endregion

#region Tags

public class PlayerComponent : Component 
{
    public uint OwnerID { get; private set; }
    public PlayerComponent(uint ownerID) 
    {
        this.OwnerID = ownerID;
    }
}

#endregion

#region Attributes

public class HealthComponent : Component
{
    private static readonly string _Health = "H";
    private static readonly string _MaxHealth = "MH";
    public float Health { get; set; }
    public float MaxHealth {  get; set; }
    public HealthComponent() { }
    public HealthComponent(float health)
    {
        Health = health;
        MaxHealth = health;
    }
    public override JsonValue SerializeData()
    {
        JsonValue data = new JsonValue();
        data[_Health].Set(Health);
        data[_MaxHealth].Set(MaxHealth);
        return data;
    }
    public override void DeserializeData(JsonValue data)
    {
        Health = data[_Health].AsFloat();
        MaxHealth = data[_MaxHealth].AsFloat();
    }
}

#endregion

#region Graphics

public class GLTFComponent : Component 
{
    private PackedScene model { get; set; }
    public AnimationPlayer AnimationPlayer { get; private set; }
    public MeshInstance3D ModelNode { get; private set; }

    private Node3D armature;
    private Skeleton3D skeleton;

    private Dictionary<string, BoneAttachment3D> bonesAttachments = new Dictionary<string, BoneAttachment3D>();

    public void SetModel(PackedScene model)
    {
        // TODO: big issue with this logic, cant check entity
        if (Entity.GetChildCount() > 0)
        {
            // remove model
            NetworkDataManager.Instance.RemoveSelfNode(Entity.GetChild(0));
        }
        // fists for example have no model (other weapons could be modeless)
        if (!model.IsValid())
            return;
        model = model;
        Node3D node3D = model.Instantiate<Node3D>();
        ModelNode = node3D.GetChildOfType<MeshInstance3D>();
        ModelNode.Position = Vector3.Zero;
        // will want to verify these are actually working...
        AnimationPlayer = node3D.GetChildOfType<AnimationPlayer>();
        armature = node3D.GetNodeOrNull<Node3D>("Armature");
        if (armature.IsValid())
            skeleton = armature.GetChildOfType<Skeleton3D>();

        NetworkDataManager.Instance.AddSelfNode(Entity, node3D);
    }

    public void PlayAnimation(string name = "")
    {
        AnimationPlayer?.Play(name);
    }

    public void PauseAnimation() { AnimationPlayer?.Pause(); }

    /// <summary>
    /// Add's a self node of type bone attachment to be used for retrieving location info of a bone.
    /// Will be used for attaching equipment and moving weapons with hands, which only needs controlled server side.
    /// </summary>
    /// <param name="boneName"></param>
    public void AddBoneAttachment(string boneName)
    {
        BoneAttachment3D bone = new BoneAttachment3D();
        bone.BoneName = boneName;
        bone.Name = boneName;
        bone.BoneIdx = skeleton.FindBone(boneName);
        bonesAttachments.Add(boneName, bone);
        NetworkDataManager.Instance.AddSelfNode(skeleton, bone);
    }

    public BoneAttachment3D GetBoneAttachment(string boneName)
    {
        if (!bonesAttachments.ContainsKey(boneName))
            return null;
        return bonesAttachments[boneName];
    }
    public override JsonValue SerializeData()
    {
        JsonValue data = new JsonValue();
        if (model.IsValid())
            data["Model"].Set(model.ResourcePath.RemovePathAndFileType());
        // TODO: Issue is model is not valid, therefor need to save the string stored in data

        if (AnimationPlayer.IsValid())
        {
            data["CurrentAnimation"].Set(AnimationPlayer.CurrentAnimation);
        }
        return data;
    }
    public override void DeserializeData(JsonValue data)
    {
        if (data["Model"].IsValue)
        {
            if (model.IsValid())
            {
                if (data["Model"].AsString() != model.ResourcePath.RemovePathAndFileType())
                {
                    SetModel(GD.Load<PackedScene>(ResourceManager.Instance.GetModelPath(data["Model"].AsString())));
                }
            }
            else
            {
                SetModel(GD.Load<PackedScene>(ResourceManager.Instance.GetModelPath(data["Model"].AsString())));
            }
        }

        if (AnimationPlayer.IsValid())
        {
            if (AnimationPlayer.CurrentAnimation != data["CurrentAnimation"].AsString())
            {
                PlayAnimation(data["CurrentAnimation"].AsString());
            }
        }
    }
    
}
public class MeshComponent : Component
{
    private static readonly string _Mesh = "MSH";
    private static readonly string _Radius = "RAD";
    private static readonly string _Height = "HGT";
    private static readonly string _Size = "SZ";
    public Mesh Mesh { get; private set; }
    private MeshInstance3D meshInst { get; set; }
    public MeshComponent() { }
    public MeshComponent(Mesh m)
    {
        Mesh = m;
    }
    public override void Init(Entity entity)
    {
        base.Init(entity);
        meshInst = new MeshInstance3D();
        meshInst.Name = "MeshInstance";
        meshInst.Mesh = Mesh;
        NetworkDataManager.Instance.AddServerNode(entity, meshInst);
    }

    public override void Destroy()
    {
        base.Destroy();
        NetworkDataManager.Instance.RemoveServerNode(meshInst);
    }

    public override JsonValue SerializeData()
    {
        JsonValue data = new JsonValue();
        if (Mesh != null)
        {
            if (Mesh is CapsuleMesh c)
            {
                data[_Mesh].Set("Capsule");
                data[_Radius].Set(c.Radius);
                data[_Height].Set(c.Height);
            }
            else if (Mesh is BoxMesh b)
            {
                data[_Mesh].Set("Box");
                data[_Size].Set(b.Size);
            }
            else if (Mesh is TextMesh)
                data[_Mesh].Set("Text");
            else if (Mesh is PlaneMesh)
                data[_Mesh].Set("Plane");
            else if (Mesh is SphereMesh s)
            {
                data[_Mesh].Set("Sphere");
                data[_Radius].Set(s.Radius);
            }
            else if (Mesh is ArrayMesh a)
            {
                data[_Mesh].Set("Array");
                foreach (var v in a.GetFaces())
                {
                    data["Vert"].Append(v);
                }
            }
            else
                data[_Mesh].Set(Mesh.ResourcePath.RemovePathAndFileType());
        }

        return data;
    }
    public override void DeserializeData(JsonValue data)
    {
        if (data[_Mesh].IsValue)
        {
            ApplyMesh(data);
        }
        else
            Mesh = null;
    }
    private void ApplyMesh(JsonValue data)
    {
        string meshName = data[_Mesh].AsString();
        if (meshName == "Capsule")
        {
            Mesh = new CapsuleMesh();
            (Mesh as CapsuleMesh).Radius = data[_Radius].AsFloat();
            (Mesh as CapsuleMesh).Height = data[_Height].AsFloat();
        }
        else if (meshName == "Box")
        {
            Mesh = new BoxMesh();
            (Mesh as BoxMesh).Size = data[_Size].AsVector3();
        }
        else if (meshName == "Text")
            Mesh = new TextMesh();
        else if (meshName == "Sphere")
        {
            Mesh = new SphereMesh();
            (Mesh as SphereMesh).Radius = data[_Radius].AsFloat();
        }
        else if (meshName == "Plane")
            Mesh = new PlaneMesh();
        else if (meshName == "Array")
        {
            SurfaceTool s = new SurfaceTool();
            ArrayMesh am = new ArrayMesh();
            var array = new Godot.Collections.Array();
            array.Resize((int)Mesh.ArrayType.Max);

            var vertices = new Vector3[data["Vert"].Array.Count];
            for (int i = 0; i < data["Vert"].Array.Count; i++)
            {
                vertices[i] = data["Vert"][i].AsVector3();
            }

            array[(int)Mesh.ArrayType.Vertex] = vertices;
            am.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, array);
            Mesh = am;
        }
        else 
            Mesh = GD.Load<Mesh>(ResourceManager.Instance.GetResourcePath(meshName));
    }
}
#endregion