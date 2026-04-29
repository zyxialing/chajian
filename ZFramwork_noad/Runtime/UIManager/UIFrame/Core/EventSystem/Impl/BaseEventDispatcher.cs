using System.Collections;
using System;
using System.Collections.Generic;

public class BaseEventDispatcher<T> : IObservable<T>, System.IDisposable
     where T : IEvent
{
    private Dictionary<string, EventObserver<T>> _mapObservers = new Dictionary<string, EventObserver<T>>();

    public bool AddObserver<TEvent>(Action<TEvent> observer)
        where TEvent : T, new()
    {
        return GetEventObserver<TEvent>().AddObserver(observer);
    }

    public bool RemoveObserver<TEvent>(Action<TEvent> observer)
        where TEvent : T, new()
    {
        return GetEventObserver<TEvent>().RemoveObserver(observer);
    }

    protected bool Notify<TEvent>(TEvent e)
        where TEvent : T, new()
    {
        var eventObserver = GetEventObserver<TEvent>();
        return eventObserver != null && eventObserver.Notify(e);
    }

    private EventObserver<T> GetEventObserver<TEvent>()
        where TEvent : T
    {
        var type = typeof(TEvent);

        if (!_mapObservers.TryGetValue(type.FullName, out EventObserver<T> observer))
        {
            observer = new EventObserver<T>();
            _mapObservers[type.FullName] = observer;
        }
        return observer;
    }

    public void Dispose()
    {
        foreach (var item in _mapObservers)
            item.Value?.Clear();
        _mapObservers.Clear();
    }
}
