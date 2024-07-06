using Godot;
using System;
[GlobalClass]
public partial class BMDirLight3D : DirectionalLight3D, INetworkData, ISaveData
{
    public string LastDataSent {  get; set; }
    public bool NetworkUpdate { get; set; } = true;
    private static readonly string _LightColor = "LC";
    private static readonly string _LightColorAlpha = "LCA";
    private static readonly string _LightEnergy = "LE";
    private static readonly string _ShadowEnabled = "SWE";
    public override void _Process(double delta)
    {
        base._Process(delta);
        if (!Helper.Instance.LocalPlayer.IsValid())
            return;
        //if (GetMeta(Globals.Meta.LevelPartitionName.ToString()).ToString() !=
        //    Helper.Instance.LocalPlayer.GetMeta(Globals.Meta.LevelPartitionName.ToString()).ToString())
        //    Visible = false;
        //else
            Visible = true;
    }

    public JsonValue SerializeNetworkData(bool forceReturn, bool ignoreThisUpdateOccurred)
    {
        if (!this.ShouldUpdate(forceReturn))
            return null;
        JsonValue data = new JsonValue();

        data[_LightColor].Set(new Vector3(LightColor.R, LightColor.G, LightColor.B));
        data[_LightColorAlpha].Set(LightColor.A);
        data[_LightEnergy].Set(LightEnergy);
        data[_ShadowEnabled].Set(ShadowEnabled);

        return this.CalculateNetworkReturn(data, ignoreThisUpdateOccurred);
    }

    public void DeserializeNetworkData(JsonValue data, bool firstDeserialization)
    {
        LightColor = new Color(data[_LightColor]["X"].AsFloat(), data[_LightColor]["Y"].AsFloat(),
            data[_LightColor]["Z"].AsFloat(), data[_LightColorAlpha].AsFloat());
        LightEnergy = data[_LightEnergy].AsFloat();
        ShadowEnabled = data[_ShadowEnabled].AsBool();
    }
    public JsonValue SerializeSaveData()
    {
        JsonValue data = new JsonValue();

        data[_LightColor].Set(new Vector3(LightColor.R, LightColor.G, LightColor.B));
        data[_LightColorAlpha].Set(LightColor.A);
        data[_LightEnergy].Set(LightEnergy);
        data[_ShadowEnabled].Set(ShadowEnabled);

        return data;
    }

    public void DeserializeSaveData(JsonValue data)
    {
        LightColor = new Color(data[_LightColor]["X"].AsFloat(), data[_LightColor]["Y"].AsFloat(), 
            data[_LightColor]["Z"].AsFloat(), data[_LightColorAlpha].AsFloat());
        LightEnergy = data[_LightEnergy].AsFloat();
        ShadowEnabled = data[_ShadowEnabled].AsBool();
    }

}
