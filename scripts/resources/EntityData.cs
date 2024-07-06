using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
[GlobalClass]
public partial class EntityData : Resource
{
    [Export(PropertyHint.MultilineText)]
    public Godot.Collections.Dictionary<string, string> Components { get; private set; } =
        new Godot.Collections.Dictionary<string, string>();
    
}