using Godot;
using System;
using System.Collections.Generic;

public partial class AppSettings : Node
{

    private static AppSettings instance = null; 
	public static AppSettings Instance { get { return instance; } }
    

	private AppSettings() 
	{
        instance = this;
        AddToGroup(Globals.Groups.AutoLoad.ToString());
        AddToGroup("ClientConsoleControl");
    }
    #region Gameplay

    #endregion

    #region Audio

    public float MasterVolume { get; private set; } = 1;
    public float MusicVolume { get; private set; } = 1;
    public float SFXVolume { get; private set; } = 1;
    public float UIVolume { get; private set; } = 0.4f;

    #endregion


    public override void _Ready()
	{
        JsonValue root = new JsonValue();
        //FirstInstantiation();
        if (!FileManager.FileExists("AppSettings.json", FileType.Res))
        {
          //  FirstInstantiation();
            FileManager.SaveToFileFormatted(Serialize(), "AppSettings.json", FileType.Res);
        }
        root = FileManager.LoadFromFile("AppSettings.json", FileType.Res);
        Deserialize(root);
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        FileManager.SaveToFileFormatted(Serialize(), "AppSettings.json", FileType.Res);
    }

    private JsonValue Serialize()
    {
        JsonValue data = new JsonValue();
        
        data["Audio"]["Master"].Set(MasterVolume);
        data["Audio"]["Music"].Set(MusicVolume);
        data["Audio"]["SFX"].Set(SFXVolume);
        data["Audio"]["UI"].Set(UIVolume);

        return data;
    }

    private void Deserialize(JsonValue data)
    {
        
        MasterVolume = data["Audio"]["Master"].AsFloat();
        MusicVolume = data["Audio"]["Music"].AsFloat();
        SFXVolume = data["Audio"]["SFX"].AsFloat();
        UIVolume = data["Audio"]["UI"].AsFloat();

    }

    private void ApplySettings()
    {

    }


    [Console]
    private void SetMasterVolume(float volume)
    {
        MasterVolume = volume;
        Mathf.Clamp(MasterVolume, 0, 1);
    }
    [Console]
    private void SetMusicVolume(float volume)
    {
        MusicVolume = volume;
        Mathf.Clamp(MusicVolume, 0, 1);
    }
    [Console]
    private void SetSFXVolume(float volume)
    {
        SFXVolume = volume;
        Mathf.Clamp(SFXVolume, 0, 1);
    }
    private void SetUIVolume(float volume)
    {
        UIVolume = volume;
        Mathf.Clamp(UIVolume, 0, 1);
    }

}
