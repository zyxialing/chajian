using System.Collections;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class EventObserver<T> : IObservable<T>, IClear where T : IEvent
{
    private Dictionary<Delegate, MethodInfo> _observers;
    private Dictionary<Delegate, MethodInfo> Observers
        => _observers ?? (_observers = new Dictionary<Delegate, MethodInfo>(1));
    private object[] _args;
    private object[] Args => _args ?? (_args = new object[1]);

    public bool AddObserver<TEvent>(Action<TEvent> observer)
      where TEvent : T, new()
    {
        if (observer == null) return false;

        if (CheckAddObserver(observer))
        {
            Observers.Add(observer, observer.Method);
            return true;
        }
        return false;

    }

    public bool RemoveObserver<TEvent>(Action<TEvent> observer) where TEvent : T, new()
    {
        if (observer == null) return false;

        if (CheckRemoveObserver(observer))
        {
            Observers.Remove(observer);
            return true;
        }
        return false;
    }


    public bool Notify<TEvent>(TEvent eve) where TEvent : T, new()
    {
        if (_observers == null || _observers.Count <= 0) return false;

        object[] dto;
        if (Args[0] == null)
        {
            Args[0] = eve;
            dto = Args;
        }
        else
        {
            dto = new object[1] { eve };
        }

        var enumerator = Observers.GetEnumerator();
        while (enumerator.MoveNext())
        {
            enumerator.Current.Value.Invoke(enumerator.Current.Key.Target, dto);
        }
        enumerator.Dispose();

        dto[0] = null;
        return true;
    }

    public void Clear()
    {
        _observers?.Clear();
        _observers = null;
        _args = null;
    }

    public bool CheckAddObserver<TEvent>(Action<TEvent> observer) where TEvent : T, new()
    {
        if (Observers.ContainsKey(observer))
        {
            Debug.LogError($"Observer Allready Exists!! {observer.Method.DeclaringType.FullName}::{observer.Method.Name}");
            return false;
        }
        return true;
    }

    public bool CheckRemoveObserver<TEvent>(Action<TEvent> observer) where TEvent : T, new()
    {
        if (!Observers.ContainsKey(observer))
        {
            Debug.LogError($"Observer Doesn't Exists!! {observer.Method.DeclaringType.FullName}::{observer.Method.Name}");
            return false;
        }
        return true;
    }
}