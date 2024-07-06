using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

public partial class SoundManager : Node
{

    private static SoundManager instance = null;
    public static SoundManager Instance { get { return instance; } }

    public enum SoundType
    {
        SFX,
        UI
    }

    private SoundManager()
    {
        instance = this;
        AddToGroup(Globals.Groups.AutoLoad.ToString());
    }

    private Dictionary<string, NonPositionalEffect> musicStreams = new Dictionary<string, NonPositionalEffect>();
	private Dictionary<string, SoundEffect> soundStreams = new Dictionary<string, SoundEffect>();
    private Dictionary<string, NonPositionalEffect> nonPosSoundStreams = new Dictionary<string, NonPositionalEffect>();
    private RandomNumberGenerator random = new RandomNumberGenerator();

    public struct NonPositionalEffect
    {
        private static int DESTROY_DELAY = 5;
        public BMAudioStreamPlayer player;
        public bool localOnly = false;
        private Stopwatch sw;

        public NonPositionalEffect(BMAudioStreamPlayer player, bool localOnly)
        {
            this.player = player;
            sw = new Stopwatch();
            this.player.Finished += Finish;
            this.localOnly = localOnly;
        }

        public bool Destroy
        {
            get
            {
                if (!sw.IsRunning)
                    return false;

                if (player.Playing)
                {
                    sw.Stop();
                    return false;
                }

                return sw.Elapsed.TotalSeconds > DESTROY_DELAY;
            }
        }

        private void Finish()
        {
            //player.Playing = false;
            sw.Start();
        }
    }

    public struct SoundEffect
    {
        private static int DESTROY_DELAY = 5;
        public SoundEffect(Node3D owner, BMAudioStreamPlayer3D player, bool localOnly)
        {
            this.owner = owner;
            this.player = player;
            sw = new Stopwatch();
            this.player.Finished += Finish;
            this.localOnly = localOnly;
        }

        public Node3D owner;
        public BMAudioStreamPlayer3D player;
        public bool localOnly = false;
        private Stopwatch sw;

        public bool Destroy
        {
            get
            {
                if (!sw.IsRunning)
                    return false;
                
                if (player.Playing)
                {
                    sw.Stop();
                    return false;
                }

                return sw.Elapsed.TotalSeconds > DESTROY_DELAY;
            }
        }
        
        private void Finish()
        {
            //player.Playing = false;
            sw.Start();
        }
    }


    public override void _Ready()
    {
        base._Ready();
        if (!NetworkManager.Instance.IsServer)
            return;
    }


    public override void _Process(double delta)
    {
        base._Process(delta);

        List<string> removals = new List<string>();

        #region Positional Sound
        foreach (var sound in soundStreams)
        {
            if (sound.Value.player.Playing)
            {
                if (sound.Value.owner.IsValid())
                    sound.Value.player.GlobalPosition = sound.Value.owner.GlobalPosition;
            }
            else if(sound.Value.Destroy)
                removals.Add(sound.Key);
        }
        foreach (var remove in removals)
        {
            bool local = soundStreams[remove].localOnly;
            soundStreams.Remove(remove);
            if(!local)
                NetworkDataManager.Instance.RemoveServerNode(GetNode(remove));
            else
                NetworkDataManager.Instance.RemoveSelfNode(GetNode(remove));
        }
        removals.Clear();
        #endregion

        #region Music
        foreach (var music in musicStreams)
        {
            if (music.Value.Destroy)
                removals.Add(music.Key);
        }
        foreach (var remove in removals)
        {
            bool local = musicStreams[remove].localOnly;
            musicStreams.Remove(remove);
            if (!local)
                NetworkDataManager.Instance.RemoveServerNode(GetNode(remove));
            else
                NetworkDataManager.Instance.RemoveSelfNode(GetNode(remove));
        }
        removals.Clear();
        #endregion

        #region Non Position Sound
        foreach (var nonPos in nonPosSoundStreams)
        {
            if (nonPos.Value.Destroy)
                removals.Add(nonPos.Key);
        }
        foreach (var remove in removals)
        {
            bool local = nonPosSoundStreams[remove].localOnly;
            nonPosSoundStreams.Remove(remove);
            if (!local)
                NetworkDataManager.Instance.RemoveServerNode(GetNode(remove));
            else
                NetworkDataManager.Instance.RemoveSelfNode(GetNode(remove));
        }
        #endregion

    }

    public void PlayMusic(string name, AudioData data, bool restartIfPaused = false)
    {
        if(!NetworkManager.Instance.IsServer)
            throw new Exception("Client is attempting to call PlayMusic()");
        
        if (musicStreams.ContainsKey(name))
        {
            if (musicStreams[name].player.Playing)
                return;
            else if(!restartIfPaused)
                musicStreams[name].player.StreamPaused = false;
            else
                musicStreams[name].player.Seek(0);
                musicStreams[name].player.Play();
            return;
        }

        BMAudioStreamPlayer player = new BMAudioStreamPlayer();
        player.Name = name;
        player.ApplyAudioData(data);
        player.VolumeDb = player.VolumeDb = 
            Mathf.LinearToDb(AppSettings.Instance.MasterVolume * AppSettings.Instance.MusicVolume);
        NetworkDataManager.Instance.AddServerNode(this, player, Vector3.Zero, false);
        musicStreams.Add(name, new NonPositionalEffect(player, false));

    }

    public string PlaySound(Node3D owner, AudioData data, SoundType type, bool localOnly = false)
    {
        if (!localOnly && !NetworkManager.Instance.IsServer)
            throw new Exception("Client attempting to call server side PlaySound(owner, data)");

        int num = 1;
        string name = owner.Name;
        string altName = name;

        while(soundStreams.ContainsKey(altName))
        {
            if (soundStreams[altName].player.AudioData == data)
            {
                soundStreams[altName].player.PlaySound();
                return altName;
            }
            altName = name + num;
            num++;
        }

        BMAudioStreamPlayer3D player3D = new BMAudioStreamPlayer3D();
        player3D.Name = altName;
        player3D.ApplyAudioData(data);
        if(type == SoundType.SFX)
            player3D.VolumeDb = Mathf.LinearToDb(AppSettings.Instance.MasterVolume * AppSettings.Instance.SFXVolume);
        else if(type == SoundType.UI)
            player3D.VolumeDb = Mathf.LinearToDb(AppSettings.Instance.MasterVolume * AppSettings.Instance.UIVolume);
        if (!localOnly)
        {
            NetworkDataManager.Instance.AddServerNode(this, player3D, owner.GlobalPosition, false);
        }
        else
        {
            player3D.Position = owner.GlobalPosition;
            NetworkDataManager.Instance.AddSelfNode(this, player3D);
        }
        soundStreams.Add(altName, new SoundEffect(owner, player3D, localOnly));
        return name;
    }

    public string PlaySound(string name, AudioData data, SoundType type, bool localOnly = false)
    {
        if (!localOnly && !NetworkManager.Instance.IsServer)
            throw new Exception("Client attempting to call server side PlaySound(string, data)");
        int num = 1;
        string altName = name;

        while (nonPosSoundStreams.ContainsKey(altName))
        {
            if (nonPosSoundStreams[altName].player.AudioData == data)
            {
                nonPosSoundStreams[altName].player.PlaySound();
                return altName;
            }
            altName = name + num;
            num++;
        }

        BMAudioStreamPlayer player = new BMAudioStreamPlayer();
        player.Name = altName;
        player.ApplyAudioData(data);
        if (type == SoundType.SFX)
            player.VolumeDb = Mathf.LinearToDb(AppSettings.Instance.MasterVolume * AppSettings.Instance.SFXVolume);
        else if (type == SoundType.UI)
            player.VolumeDb = Mathf.LinearToDb(AppSettings.Instance.MasterVolume * AppSettings.Instance.UIVolume);
        if (!localOnly)
        {
            NetworkDataManager.Instance.AddServerNode(this, player, Vector3.Zero, false);
        }
        else
        {
            NetworkDataManager.Instance.AddSelfNode(this, player);
        }
        nonPosSoundStreams.Add(altName, new NonPositionalEffect(player, localOnly));
        
        return name;
    }

    
}
