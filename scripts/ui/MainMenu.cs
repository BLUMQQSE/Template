using Godot;
using Steamworks.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class MainMenu : Control
{
    
    BMButton HostNewLobby;
    BMButton HostLoadLobby;
    BMButton JoinLobby;
    BMButton RefreshButton;
    Lobby lobbyFound;

    Stopwatch sw = new Stopwatch();
    bool lobbyWasFound = false;

    public override void _Ready()
    {
        base._Ready();
        SteamManager.Instance.GetMultiplayerLobbies();
        SteamManager.Instance.OnLobbyRefreshCompleted += OnLobbyRefresh;

        HostNewLobby = GetNode<BMButton>("HostNewButton");
        HostLoadLobby = GetNode<BMButton>("HostLoadButton");
        JoinLobby = GetNode<BMButton>("JoinButton");

        HostNewLobby.Pressed += CreateNewLobby;
        HostLoadLobby.Pressed += LoadLobby;
        JoinLobby.Pressed += JoinLobbyM;

        sw.Start();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if(sw.Elapsed.TotalSeconds > 1 && !lobbyWasFound)
        {
            SteamManager.Instance.GetMultiplayerLobbies();
            sw.Restart();
        }
    }

    private void JoinLobbyM()
    {
        if (lobbyWasFound)
        {
            LevelManager.Instance.CloseLevel("MainMenu");
            lobbyFound.Join();
        }
    }

    private void OnLobbyRefresh(List<Lobby> list)
    {
        if(list.Count > 0)
        {
            lobbyFound = list[0];
            lobbyWasFound=true;
        }
    }


    private void CreateNewLobby()
    {
        GD.Print("Host new lobby");
        SaveManager.Instance.CreateSave("DefaultSave");
        LevelManager.Instance.CloseLevel("MainMenu");
        AppManager.Instance.ChangeState(AppManager.AppState.Gameplay);
        LevelManager.Instance.InstantiatePlayer(SteamManager.Instance.PlayerId, SteamManager.Instance.PlayerName);


        SteamManager.Instance.CreateLobby("Default");
    }

    private void LoadLobby()
    {
        LevelManager.Instance.CloseLevel("MainMenu");
        AppManager.Instance.ChangeState(AppManager.AppState.Gameplay);
        SaveManager.Instance.LoadSave("DefaultSave");

        LevelManager.Instance.InstantiatePlayer(SteamManager.Instance.PlayerId, SteamManager.Instance.PlayerName);

        SteamManager.Instance.CreateLobby("Default");
    }

}
