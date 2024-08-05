using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public partial class CommandHandler : Node
{
    static CommandHandler instance;
    public static CommandHandler Instance { get { return instance; } }
    public override void _Ready()
    {
        base._Ready();
        instance = this;
        AddToGroup(Globals.Groups.AutoLoad.ToString());
    }

    public void HandleCommand(ref Node ownerOfMessage, string message, RichTextLabel display)
    {
        List<string> args = new List<string>();
        args = message.Split(" ").ToList();
        
        string commandName = args[0];
        args.RemoveAt(0);

        switch (commandName.ToLower())
        {
            case "getnode":
                GetNodeCommand(ref ownerOfMessage, args);
                break;
            case "free":
                if (!NetworkManager.Instance.IsServer) return;
                FreeCommand(ref ownerOfMessage, args);
                break;
            case "ls":
                ListCommand(ref ownerOfMessage, args, display);
                break;
            case "pause":
                if (!NetworkManager.Instance.IsServer) return;
                AppManager.Instance.ChangeState(AppManager.AppState.Console_Paused);
                break;
            case "unpause":
                if (!NetworkManager.Instance.IsServer) return;
                AppManager.Instance.ChangeState(AppManager.AppState.Console_Unpaused);
                break;
            case "save":
                if (!NetworkManager.Instance.IsServer) return;
                SaveCommand();
                break;
            default:
                if(!ownerOfMessage.IsInGroup("ClientConsoleControl"))
                    if (!NetworkManager.Instance.IsServer) return;
                CallCommand(ref ownerOfMessage, commandName,  ConvertToVariants(args));
                break;
                
        }
    }

    private void SaveCommand()
    {
        SaveManager.Instance.SaveGame();
    }

    private void ListCommand(ref Node ownerOfMessage, List<string> args, RichTextLabel display)
    {
        if(args.Count == 1)
        {
            // local
            display.Text += ListHelper(ownerOfMessage, 0);
        }
        else
        {
            display.Text += ListHelper(GetTree().Root, 0);
        }
    }

    private string ListHelper(Node node, int tabCount)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < tabCount; i++)
            sb.Append("    ");

        sb.Append("["+node.GetMeta(Globals.Meta.UniqueId.ToString()) + "] "+node.Name+"\n");

        foreach (Node n in node.GetChildren())
        {
            sb.Append(ListHelper(n, tabCount+1));
        }

        return sb.ToString();
    }

    public List<Variant> ConvertToVariants(List<string> args)
    {
        List<Variant> variants = new List<Variant>();

        for(int i = 0; i < args.Count; i++)
        {
            if (args[i].StartsWith('\"'))
            {
                string result = args[i].TrimStart('\"');
                if (!args[i].EndsWith('\"'))
                {
                    while (!args[i + 1].EndsWith('\"') && i + 1 < args.Count - 1)
                    {
                        result += " " + args[i + 1];
                        args.RemoveAt(i + 1);
                    }
                    result += " " + args[i + 1].Trim('\"');
                    args.RemoveAt(i + 1);
                }
                else
                    result = result.TrimEnd('\"');
                variants.Add(result);

                

            }
            else if (args[i].StartsWith('('))
            {
                string value = args[i].TrimStart('(');
                value = value.TrimEnd(')');
                string[] ar = value.Split(',');
                
                if(ar.Length == 2)
                    variants.Add(new Vector2((float)Convert.ToDouble(ar[0]), (float)Convert.ToDouble(ar[1])));
                else
                    variants.Add(new Vector3((float)Convert.ToDouble(ar[0]), (float)Convert.ToDouble(ar[1]), (float)Convert.ToDouble(ar[2])));
                
            }
            else if (args[i].StartsWith('['))
            {
                string s = args[i].TrimStart('[');
                s = s.TrimEnd(']');
                uint id = Convert.ToUInt32(s);
                variants.Add(NetworkDataManager.Instance.UniqueIdToNode(id));
            }
            else if (args[i].StartsWith('#'))
            {
                string path = args[i].Trim('#');
                variants.Add(GD.Load<Resource>(ResourceManager.Instance.GetResourcePath(path)));
            }
            else if (args[i].Contains('.'))
            {
                variants.Add(Convert.ToDouble(args[i]));
            }
            else if(args[i].ToLower() == "false" || args[i].ToLower() == "true")
            {
                if (args[i].ToLower() == "false")
                    variants.Add(false);
                else
                    variants.Add(true);
            }
            else
            {
                variants.Add(Convert.ToInt32(args[i]));
            }
        }

        return variants;
    }

    private void CallCommand(ref Node ownerOfMessage, string methodName, List<Variant> args)
    {
        if (!ownerOfMessage.HasMethod(methodName))
        {
            return;
        }
        ownerOfMessage.Call(methodName, args.ToArray());
    }

    private void FreeCommand(ref Node ownerOfMessage, List<string> args)
    {
        NetworkDataManager.Instance.RemoveServerNode(ownerOfMessage);
        ownerOfMessage = ownerOfMessage.GetParent();
    }

    private void GetNodeCommand(ref Node ownerOfMessage, List<string> args)
    {
        if (!ownerOfMessage.HasNode(args[0]))
            return;
        ownerOfMessage = ownerOfMessage.GetNode(args[0]);

    }


}
