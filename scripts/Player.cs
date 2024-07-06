using Godot;
using System;

public partial class Player : Node3D
{
    private DeveloperConsole console;
    public override void _Ready()
    {
        base._Ready();

        if (this.IsLocalOwned())
        {
            Helper.Instance.LocalPlayer = this;
            
            console = GD.Load<PackedScene>(ResourceManager.Instance.GetScenePath("DeveloperConsole"))
                .Instantiate<DeveloperConsole>();
            NetworkDataManager.Instance.AddSelfNode(this, console);

        }
        Helper.Instance.AllPlayers.Add(this);

        /* Example for using ECS and having a player entity
        if (NetworkManager.Instance.IsServer)
        {
            Entity playerEntity = new Entity();
            playerEntity.AddComponent(new PlayerComponent(
                uint.Parse(GetMeta(Globals.Meta.OwnerId.ToString()).ToString())));
            playerEntity.AddComponent(new MovementComponent());
            ECS.Instance.AddEntity(playerEntity);

            // then simply store a reference to our playerEntity
        }
        */

    }
    public override void _ExitTree()
    {
        base._ExitTree();
        Helper.Instance.AllPlayers.Remove(this);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        
        if (NetworkManager.Instance.IsServer)
        {

            /* here handle all player actions, based on input

                if (InputManager.Instance.ActionJustPressed("a", this))
                    GD.Print(InputManager.Instance.GetMousePosition(this));
             */
        }
    }
}
