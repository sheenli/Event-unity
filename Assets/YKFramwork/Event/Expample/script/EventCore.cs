namespace YKFramework.Event.Expample
{
    public class EventCore : EventDispatcherNode
    {
        public static EventCore Inst { get; private set; }

        private void Awake()
        {
            Inst = this;
        }
    }
}