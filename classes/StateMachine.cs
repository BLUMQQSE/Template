using System;
using System.Collections.Generic;
using Godot;
public class StateMachine
{
    public State CurrentState { get; private set; }
    
    public void Init(State initState)
    {
        CurrentState = initState;
        CurrentState.EnterState();
    }
    public void Process(float delta)
    {
        CurrentState.ProcessState(delta);
    }

    public void PhysicsProcess(float delta)
    {
        CurrentState.PhysicsProcessState(delta);
    }

    public void ChangeState(State newState)
    {
        if (CurrentState == newState)
        {
            return;
        }
        CurrentState.ExitState();
        CurrentState = newState;
        CurrentState.EnterState();
    }

}


public abstract class State
{
    protected StateMachine stateMachine { get; private set; }
    public State(StateMachine stateMachine)
    {
        this.stateMachine = stateMachine;
    }
    public abstract void EnterState();
    public abstract void ProcessState(float delta);
    public abstract void PhysicsProcessState(float delta);
    public abstract void ExitState();

}
