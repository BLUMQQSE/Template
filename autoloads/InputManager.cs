using Godot;
using System;
using System.Collections.Generic;
using static System.Collections.Specialized.BitVector32;

public partial class InputManager : Node
{
    private static InputManager instance;
    public static InputManager Instance {  get { return instance; } }
    public InputManager()
    {
        instance = this;
        AddToGroup(Globals.Groups.AutoLoad.ToString());
    }

    private InputState localInputState; 
    private Dictionary<Player, InputState> inputs = new Dictionary<Player, InputState>();

    public override void _Ready()
    {
        base._Ready();
        
        localInputState = new InputState();

        ProcessPriority = 1000;
        foreach (var item in InputMap.GetActions())
        {
            if (!item.ToString().Contains("ui_"))
            {
                localInputState.ActionStates[item] = PressState.NotPressed;
            }
        }

        localInputState.MouseStates[MouseButton.Left] = PressState.NotPressed;
        localInputState.MouseStates[MouseButton.Middle] = PressState.NotPressed;
        localInputState.MouseStates[MouseButton.Right] = PressState.NotPressed;

        LevelManager.Instance.PlayerInstantiated += OnPlayerLoaded;
    }

    private void CreateInputState(Player player)
    {
        GD.Print("Create for "+player.Name);
        if(inputs.ContainsKey(player))
        {
            inputs.Remove(player);
        }
        InputState state = new InputState();
        foreach (var item in InputMap.GetActions())
        {
            if (!item.ToString().Contains("ui_"))
            {
                state.ActionStates[item] = PressState.NotPressed;
            }
        }

        state.MouseStates[MouseButton.Left] = PressState.NotPressed;
        state.MouseStates[MouseButton.Middle] = PressState.NotPressed;
        state.MouseStates[MouseButton.Right] = PressState.NotPressed;

        // NOTE: This requires input manager being instantiated AFTER Helper
        inputs.Add(player, state);
    }


    public override void _Process(double delta)
    {
        bool change = localInputState.Update();
        localInputState.MousePosition = GetTree().Root.GetMousePosition();

        if (!NetworkManager.Instance.IsServer && change)
        {
            // we need to send update to server
            JsonValue inputData = new JsonValue();

            foreach (var action in localInputState.ActionStates)
            {
                inputData["A"][action.Key].Set((int)action.Value);
            }
            foreach (var mouse in localInputState.MouseStates)
            {
                inputData["M"][((int)mouse.Key).ToString()].Set((int)mouse.Value);
            }
            inputData["MP"].Set(localInputState.MousePosition);

            NetworkDataManager.Instance.ClientInputUpdate(inputData);
        }
        if (NetworkManager.Instance.IsServer)
        {
            foreach(var input in  inputs)
            {
                // need to update inputs to their next values
                input.Value.UpdateClientInput();
            }
        }
    }
    public Vector2 GetMousePosition(Player player = null)
    {
        if (player == null)
            return localInputState.MousePosition;
        else if (player.IsLocalOwned())
            return localInputState.MousePosition;
        else if (inputs.ContainsKey(player))
            return inputs[player].MousePosition;

        GD.Print("[Error] Attempting to access input of non-registered player: ");
        return Vector2.Zero;
    }

    public bool ActionJustPressed(StringName action, Player player = null) 
    { 
        if(player == null)
            return localInputState.ActionStates[action] == PressState.JustPressed;
        else if(player.IsLocalOwned())
            return localInputState.ActionStates[action] == PressState.JustPressed;
        else if(inputs.ContainsKey(player))
            return inputs[player].ActionStates[action] == PressState.JustPressed;

        GD.Print("[Error] Attempting to access input of non-registered player: ");
        return false;
    }
    public bool ActionPressed(StringName action, Player player = null) 
    {
        if (player == null)
            return localInputState.ActionStates[action] == PressState.Pressed;
        else if (player.IsLocalOwned()) 
            return localInputState.ActionStates[action] == PressState.Pressed;
        else if (inputs.ContainsKey(player))
            return inputs[player].ActionStates[action] == PressState.Pressed;

        GD.Print("[Error] Attempting to access input of non-registered player");
        return false;
    }
    public bool ActionJustReleased(StringName action, Player player = null) 
    {
        if (player == null)
            return localInputState.ActionStates[action] == PressState.JustReleased;
        else if (player.IsLocalOwned())
            return localInputState.ActionStates[action] == PressState.JustReleased;
        else if (inputs.ContainsKey(player))
            return inputs[player].ActionStates[action] == PressState.JustReleased;

        GD.Print("[Error] Attempting to access input of non-registered player");
        return false;
    }
    public bool MouseJustPressed(MouseButton mouse, Player player = null) 
    {
        if (player == null)
            return localInputState.MouseStates[mouse] == PressState.JustPressed;
        else if (player.IsLocalOwned())
            return localInputState.MouseStates[mouse] == PressState.JustPressed;
        else if (inputs.ContainsKey(player))
            return inputs[player].MouseStates[mouse] == PressState.JustPressed;

        GD.Print("[Error] Attempting to access input of non-registered player");
        return false;
    }
    public bool MousePressed(MouseButton mouse, Player player = null) 
    {
        if (player == null)
            return localInputState.MouseStates[mouse] == PressState.Pressed;
        else if (player.IsLocalOwned())
            return localInputState.MouseStates[mouse] == PressState.Pressed;
        else if (inputs.ContainsKey(player))
            return inputs[player].MouseStates[mouse] == PressState.Pressed;

        GD.Print("[Error] Attempting to access input of non-registered player");
        return false;
    }
    public bool MouseJustReleased(MouseButton mouse, Player player = null) 
    {
        if (player == null)
            return localInputState.MouseStates[mouse] == PressState.JustReleased;
        else if (player.IsLocalOwned())
            return localInputState.MouseStates[mouse] == PressState.JustReleased;
        else if (inputs.ContainsKey(player))
            return inputs[player].MouseStates[mouse] == PressState.JustReleased;

        GD.Print("[Error] Attempting to access input of non-registered player");
        return false;
    }

    public void ResetActionInput(Player player = null)
    {
        if (player == null)
            foreach (var action in localInputState.ActionStates.Keys)
                localInputState.ActionStates[action] = PressState.Reset;
        else if (player.IsLocalOwned())
            foreach (var action in localInputState.ActionStates.Keys)
                localInputState.ActionStates[action] = PressState.Reset;
        else if (inputs.ContainsKey(player))
            foreach (var action in inputs[player].ActionStates.Keys)
                inputs[player].ActionStates[action] = PressState.Reset;
        else
            GD.Print("[Error] Attempting to reset actions for non-registered player");
    }
    public void ResetMouseInput(Player player = null)
    {
        if (player == null)
            foreach (var mouse in localInputState.MouseStates.Keys)
                localInputState.MouseStates[mouse] = PressState.Reset;
        else if (player.IsLocalOwned())
            foreach (var mouse in localInputState.MouseStates.Keys)
                localInputState.MouseStates[mouse] = PressState.Reset;
        else if (inputs.ContainsKey(player))
            foreach (var mouse in inputs[player].MouseStates.Keys)
                inputs[player].MouseStates[mouse] = PressState.Reset;
        else
            GD.Print("[Error] Attempting to reset mouse for non-registered player");
    }
    public void ResetAllInput(Player player = null)
    {
        ResetActionInput(player);
        ResetMouseInput(player);
    }

    public void HandleClientInputUpdate(Player player, JsonValue data)
    {
        
        InputState input = inputs[player];
        foreach(var action in data["A"].Object)
        {
            // here modify to ONLY set value if is JustPressed or JustReleased
            PressState s = (PressState)action.Value.AsInt();
            if(s == PressState.JustPressed || s == PressState.JustReleased)
                input.ActionStates[action.Key] = s;
        }
        foreach(var mouse in data["M"].Object)
        {
            MouseButton m = (MouseButton)int.Parse(mouse.Key);
            PressState s = (PressState)mouse.Value.AsInt();
            if (s == PressState.JustPressed || s == PressState.JustReleased)
                input.MouseStates[m] = s;
        }
        inputs[player].MousePosition = data["MP"].AsVector2();
        inputs[player] = input;
    }

    private void OnPlayerLoaded(Player player)
    {
        if (player.IsLocalOwned())
            return;
        CreateInputState(player);
    }
    
}


public class InputState
{
    public Dictionary<StringName, PressState> ActionStates { get; set; } = new Dictionary<StringName, PressState>();
    public Dictionary<MouseButton, PressState> MouseStates { get; set; } = new Dictionary<MouseButton, PressState>();
    public Vector2 MousePosition { get; set; } = new Vector2();
    public bool Update()
    {
        bool change = false;
        foreach (var key in ActionStates.Keys)
        {
            if (Input.IsActionPressed(key))
            {
                if (ActionStates[key] == PressState.NotPressed)
                {
                    ActionStates[key] = PressState.JustPressed;
                    change = true;
                }
                else if (ActionStates[key] == PressState.JustPressed)
                {
                    ActionStates[key] = PressState.Pressed;
                    change = true;
                }
            }
            else
            {
                if (ActionStates[key] == PressState.Reset)
                {
                    ActionStates[key] = PressState.NotPressed;
                    change = true;
                }
                if (ActionStates[key] == PressState.JustPressed)
                {
                    ActionStates[key] = PressState.JustReleased;
                    change = true;
                }
                else if (ActionStates[key] == PressState.Pressed)
                {
                    ActionStates[key] = PressState.JustReleased;
                    change = true;
                }
                else if (ActionStates[key] == PressState.JustReleased)
                {
                    ActionStates[key] = PressState.NotPressed;
                    change = true;
                }
            }
        }

        foreach (var key in MouseStates.Keys)
        {
            if (Input.IsMouseButtonPressed(key))
            {
                if (MouseStates[key] == PressState.NotPressed)
                {
                    MouseStates[key] = PressState.JustPressed;
                    change = true;
                }
                else if (MouseStates[key] == PressState.JustPressed)
                {
                    MouseStates[key] = PressState.Pressed;
                    change = true;
                }
            }
            else
            {
                if (MouseStates[key] == PressState.Reset)
                {
                    MouseStates[key] = PressState.NotPressed;
                    change = true;
                }
                if (MouseStates[key] == PressState.JustPressed)
                {
                    MouseStates[key] = PressState.JustReleased;
                    change = true;
                }
                else if (MouseStates[key] == PressState.Pressed)
                {
                    MouseStates[key] = PressState.JustReleased;
                    change = true;
                }
                else if (MouseStates[key] == PressState.JustReleased)
                {
                    MouseStates[key] = PressState.NotPressed;
                    change = true;
                }
            }
        }
        return change;
    }

    public void UpdateClientInput()
    {
        foreach (var key in ActionStates.Keys)
        {
            if (ActionStates[key] == PressState.JustPressed)
                ActionStates[key] = PressState.Pressed;
            else if (ActionStates[key] == PressState.JustReleased)
                ActionStates[key] = PressState.NotPressed;
        }
        foreach (var key in MouseStates.Keys)
        {
            if (MouseStates[key] == PressState.JustPressed)
                MouseStates[key] = PressState.Pressed;
            else if (MouseStates[key] == PressState.JustReleased)
                MouseStates[key] = PressState.NotPressed;
        }
    }

}
public enum PressState
{
    NotPressed,
    JustPressed,
    Pressed,
    JustReleased,
    Reset
}
