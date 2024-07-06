using System;
using Godot;

[GlobalClass]
public partial class EntityContainer : Entity
{
    [Export] public EntityData EntityData { get; set; }
    [Export(PropertyHint.MultilineText)]
    public Godot.Collections.Dictionary<string, string> Components { get; set; }
        = new Godot.Collections.Dictionary<string, string>();

    public virtual void Initialize()
    {
        if (EntityData.IsValid())
        {
            foreach (var ed in EntityData.Components)
            {
                string compName = ed.Key.ToString();
                Type type = Type.GetType(compName);
                Component c = (Component)Activator.CreateInstance(type);
                JsonValue jsonValue = new JsonValue();
                jsonValue.Parse(ed.Value);
                AddComponent(c);
                c.DeserializeData(jsonValue);
            }
        }
        foreach (var ed in Components)
        {
            string compName = ed.Key.ToString();
            Type type = Type.GetType(compName);
            Component c = (Component)Activator.CreateInstance(type);
            JsonValue jsonValue = new JsonValue();
            jsonValue.Parse(ed.Value);
            AddComponent(c);
            c.DeserializeData(jsonValue);
        }
    }

}