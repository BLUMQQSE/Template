using Godot;
using System;
using System.Collections.Generic;

public partial class EventSystem : Node
{
    private static EventSystem instance = null;

    private EventSystem() 
    {
        AddToGroup(Globals.Groups.AutoLoad.ToString());
        instance = this;
    }

    public static EventSystem Instance { get { return instance; } }

    /// <summary>
    /// IMPORTANT! Make sure to UnsubscribeAll on _ExitTree.
    /// </summary>
    public void Subscribe(EventID id, IListener listener) { Subscribe(id.ToString(), listener); }

    /// <summary>
    /// IMPORTANT! Make sure to UnsubscribeAll on _ExitTree.
    /// </summary>
    public void Subscribe(string id, IListener listener)
    {
        //GD.Print(id + " " + (listener as Node).Name);
        if (AlreadyRegistered(id, listener))
            return;

        // Add listener to list of listeners listening to id
        Instance.database.Add(id, listener);
    }
    public void Unsubscribe(EventID id, IListener listener) { Unsubscribe(id.ToString(), listener); }

    public void Unsubscribe(string id, IListener listener)
    {
        Instance.database.Remove(id, listener);
    }

    public void UnsubscribeAll(IListener listener)
    {
        foreach(string id in database.Keys)
        {
            Instance.database.Remove(id, listener);
        }
    }
    public void PushEvent(EventID id, object data = null, object owner = null) 
    {
        PushEvent(id.ToString(), data, owner);
    }
    public void PushEvent(string id, object data = null, object owner = null)
    {
        Event e = new Event(id, data, owner);
        DispatchEvent(e);
    }

    private bool AlreadyRegistered(string id, IListener listener)
    {
        return Instance.database.Contains(id, listener);
    }

    private void DispatchEvent(Event e)
    {
        List<IListener> list = Instance.database[e.ID];
        foreach(IListener listener in list)
        { 
            listener.HandleEvent(e);
        }
    }

    //database of clients and their events
    MultiMap<string, IListener> database = new MultiMap<string, IListener>();

}

public interface IListener
{
    public void HandleEvent(Event e);
}

public enum EventID
{
    OnConnectionRecievedData,
    OnSocketRecievedData,
    OnServerGameLoaded,

    OnConsoleOpen,
    OnConsoleClose,

}
public class Event
{
    public Event(EventID eventID, object parameter)
    {
        this.eventID = eventID.ToString();
        this.parameter = parameter;
    }
    public Event(string eventID, object parameter)
    {
        this.eventID = eventID;
        this.parameter = parameter;
    }
    public Event(EventID eventID, object parameter, object caller)
    {
        this.eventID = eventID.ToString();
        this.parameter = parameter;
        this.caller = caller;
    }
    public Event(string eventID, object parameter, object caller)
    {
        this.eventID = eventID;
        this.parameter = parameter;
        this.caller = caller;
    }

    ~Event() { }

    public string ID { get { return eventID; } }
    public EventID IDAsEvent
    {
        get
        {
            EventID id;
            Enum.TryParse(eventID, out id);
            return id;
        }
    }
    public object Parameter { get { return parameter; } }
    public object Caller { get { return caller; } }


    string eventID;
    object parameter;
    object caller;
};