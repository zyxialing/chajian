
public abstract class Event<T> : TObjectPool<T>, IEvent, IClear where T : Event<T>, new()
{
    protected Event()
    {

    }

    public abstract void Clear();
}
