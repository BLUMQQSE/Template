using Godot;
using System;

public partial class DeveloperConsole : Window
{
    [Export] LineEdit InputBar { get; set; }
    [Export] RichTextLabel Display { get; set; }

    Node ownerOfCommand;
    bool mouseInBounds = false;
    
    public override void _Ready()
    {
        base._Ready();

        InputBar.TextSubmitted += TextEntered;
        CloseRequested += Close;
        if(!ownerOfCommand.IsValid())
            ownerOfCommand = GetTree().CurrentScene;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        Close();
    }
    private void OnMouseExited() { mouseInBounds = false; }

    private void OnMouseEntered(){ mouseInBounds = true; }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (Input.IsActionJustPressed("~"))
        {
            if (Mode == ModeEnum.Minimized)
            {
                CallDeferred("Open");
            }
            else
            {
                CallDeferred("Close");
            }
        }
        if(Mode == ModeEnum.Windowed)
        {
            if (Input.IsActionJustPressed("up") && Input.IsActionPressed("shift"))
            {
                int index = ownerOfCommand.GetIndex();
                
                if(index != 0)
                {
                    ownerOfCommand = ownerOfCommand.GetParent().GetChild(index-1);
                }
                NodeFocusChange();
            }
            if (Input.IsActionJustPressed("left") && Input.IsActionPressed("shift"))
            {
                if (ownerOfCommand != GetTree().Root)
                    ownerOfCommand = ownerOfCommand.GetParent();
                NodeFocusChange();
            }
            if (Input.IsActionJustPressed("down") && Input.IsActionPressed("shift"))
            {
                int index = ownerOfCommand.GetIndex();
                if (index != ownerOfCommand.GetParent().GetChildCount() - 1 && ownerOfCommand != GetTree().Root)
                {
                    ownerOfCommand = ownerOfCommand.GetParent().GetChild(index + 1);
                }
                NodeFocusChange();
            }
            if (Input.IsActionJustPressed("right") && Input.IsActionPressed("shift"))
            {
                if(ownerOfCommand.GetChildCount() > 0)
                {
                    ownerOfCommand = ownerOfCommand.GetChild(0);
                }
                NodeFocusChange();
            }
        }
    }

    public void SetOwnerOfCommand(Node owner)
    {
        ownerOfCommand = owner;
    }

    private void NodeFocusChange()
    {
        InputBar.Text = "[" + ownerOfCommand.Name + "] ";
        InputBar.CaretColumn = InputBar.Text.Length;
    }

    private void Open()
    {
        AppManager.Instance.ChangeState(AppManager.AppState.Console_Paused);
        EventSystem.Instance.PushEvent(EventID.OnConsoleOpen);
        Visible = true;
        Mode = ModeEnum.Windowed;
        InputBar.GrabFocus();
        mouseInBounds = true;
        NodeFocusChange();
    }

    private void Close()
    {
        AppManager.Instance.ChangeState(AppManager.AppState.Gameplay);
        EventSystem.Instance.PushEvent(EventID.OnConsoleClose);
        Visible = false;
        InputBar.ReleaseFocus();
        Mode = ModeEnum.Minimized;
    }

    private void TextEntered(string newText)
    {
        if (newText.Length == 0)
            return;

        PushMessage(newText);

        InputBar.Clear();
        NodeFocusChange();

    }

    void PushMessage(string message)
    {
        Display.Text += message + "\n";
        if (message.ToLower().Contains("clear"))
            Clear();
        else
            CommandHandler.Instance.HandleCommand(ref ownerOfCommand, message.Substring(message.Find("]") + 1).Trim(), Display);
    }

    private void Clear()
    {
        Display.Text = "";
    }
}
