using Godot;
using System;

[GlobalClass]
public partial class BMButton : Button, INetworkData, ISaveData
{
    
    public bool NetworkUpdate { get; set; } = true;

    public JsonValue SerializeNetworkData(bool forceReturn, bool ignoreThisUpdateOccurred)
    {
        if (!this.ShouldUpdate(forceReturn))
            return null;
        JsonValue data = new JsonValue();

        data["SZ"].Set(Size);
        data["TX"].Set(Text);

        return this.CalculateNetworkReturn(data, ignoreThisUpdateOccurred);
    }

    public void DeserializeNetworkData(JsonValue data, bool firstDeserialization)
    {
        Size = data["SZ"].AsVector2();
        Text = data["TX"].AsString();
    }

    public JsonValue SerializeSaveData()
    {
        JsonValue data = new JsonValue();

        data["Text"].Set(Text);
        data["Size"].Set(Size);
        
        return data;
    }
    public void DeserializeSaveData(JsonValue data)
    {
        Text = data["Text"].AsString();
        Size = data["Size"].AsVector2();
    }
     
}
