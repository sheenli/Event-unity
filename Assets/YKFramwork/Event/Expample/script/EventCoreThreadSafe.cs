namespace YKFramework.Event.Expample
{
    public class EventCoreThreadSafe : EventDispatcherNode
    {
        
        public static EventCoreThreadSafe Inst { get; private set; }

        private void Awake()
        {
            Inst = this;
        }
    }
}