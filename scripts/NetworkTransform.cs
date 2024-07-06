using Godot;
using System;

public partial class NetworkTransform : Node, INetworkData
{
    public NetworkTransformComponent NetworkTransformComponent { get; set; } = new NetworkTransformComponent();
    public bool NetworkUpdate { get; set; }

    public override void _Process(double delta)
    {
        base._Process(delta);
        NetworkUpdate = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (!NetworkManager.Instance.IsServer)
        {
            GetParent<Node3D>().Position = GetParent<Node3D>().Position.Lerp(NetworkTransformComponent.SyncPos, 5 * (float)delta);
            GetParent<Node3D>().Rotation = NetworkTransformComponent.SyncRot;
        }
        else
        {
            NetworkTransformComponent.SyncPos = GetParent<Node3D>().Position;
            NetworkTransformComponent.SyncRot = GetParent<Node3D>().Rotation;
        }
    }

    public JsonValue SerializeNetworkData(bool forceReturn = false, bool ignoreThisUpdateOccurred = false)
    {
        if (!this.ShouldUpdate(forceReturn))
            return null;

        JsonValue data = new JsonValue();
        data["NTC"].Set(NetworkTransformComponent.SerializeData());

        return this.CalculateNetworkReturn(data, ignoreThisUpdateOccurred);
    }
    public void DeserializeNetworkData(JsonValue data, bool firstDeserialize)
    {
        NetworkTransformComponent.DeserializeData(data["NTC"]);
    }

}
