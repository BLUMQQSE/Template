using Godot;
using System;

public partial class AppManager : Node
{

	private static AppManager instance;
	public static AppManager Instance { get { return instance; } }
	public bool ActiveGameplay 
	{ 
		get { return instance.CurrentState == AppState.Gameplay || instance.CurrentState == AppState.Console_Unpaused; } 
	}

	public AppManager() 
	{
		instance = this;
		CurrentState = AppState.MainMenu;
		AddToGroup(Globals.Groups.AutoLoad.ToString());
	}

	public enum AppState
	{
		MainMenu,
		Gameplay,
		Console_Paused,
		Console_Unpaused
	}

	public AppState CurrentState { get; private set; }
	public void ChangeState(AppState state)
	{
		if (state == CurrentState)
			return;
		if(CurrentState == AppState.Console_Paused)
		{

		}
		else if (state == AppState.Console_Paused) 
		{

		}

		CurrentState = state;
	}


}
