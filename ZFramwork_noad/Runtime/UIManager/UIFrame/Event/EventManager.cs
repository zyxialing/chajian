using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventManager : Singleton<EventManager>, IDomainEvent
{
    private EventManager()
    {

    }

    public static EventDispatcher eventDispatcher { get; private set; } = new EventDispatcher();

    public bool Dispatch<TEvent>(TEvent e, bool recycle = true)
        where TEvent : Event<TEvent>, new()
    {
        return e != null && eventDispatcher.Dispatch(e, recycle);
    }
    public bool AddObserver<TEvent>(Action<TEvent> observer) where TEvent : IEvent, new()
    {
        return observer != null && eventDispatcher.AddObserver(observer);
    }

    public bool RemoveObserver<TEvent>(Action<TEvent> observer) where TEvent : IEvent, new()
    {
        return observer != null && eventDispatcher.RemoveObserver(observer);
    }
}
