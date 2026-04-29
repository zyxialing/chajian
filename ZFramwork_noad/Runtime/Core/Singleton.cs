using System;
using System.Reflection;

public class Singleton<T> where T : class
{
    protected static T instance;

    public static T Instance => instance ??= CreateInstance();

    protected static T CreateInstance()
    {
        var constructor = typeof(T).GetConstructor(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic,
            null, Type.EmptyTypes, null);

        if (constructor == null)
            throw new Exception("Non-Public Constructor() not found in " + typeof(T));

        return constructor.Invoke(null) as T;
    }

    public static void SetInstance(T newInstance)
    {
        instance = newInstance;
    }

    protected Singleton() { }
}

