public class EventDispatcher : BaseEventDispatcher<IEvent>
{
    public bool Dispatch<TEvent>(TEvent e, bool recycle = true)
        where TEvent : Event<TEvent>, new()
    {
        var success = Notify(e);

        if (recycle) Event<TEvent>.Recycle(e);

        return success;
    }
}