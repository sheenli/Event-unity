using UnityEngine;

namespace YKFramework.Event.Expample
{
    public class EventListener : MonoBehaviour
    {
        public bool braeak;

        // Start is called before the first frame update
        private void Start()
        {
            EventCore.Inst.AttachListener(1, EventFirst, 99);
            EventCore.Inst.AttachListener(1, EventOnce, 98, true);
            EventCore.Inst.AttachListener(1, EventBreak, 10);
            EventCore.Inst.AttachListener(1, Event2);
            EventCoreThreadSafe.Inst.AttachListener(1, EventThreadSafe);
        }

        private void EventThreadSafe(EventData ev)
        {
            transform.localPosition = new Vector3(1, 0, 0);
            Debug.Log("EventThreadSafe");
        }

        private void EventFirst(EventData ev)
        {
            Debug.Log("EventFirst");
        }

        private void EventBreak(EventData ev)
        {
            Debug.Log("EventBreak");
            // if braeak Event2 dont listener
            if (braeak) ev.Break();
        }


        private void Event2(EventData ev)
        {
            Debug.Log("Event2");
        }

        private void EventOnce(EventData ev)
        {
            Debug.Log("EventOnce");
        }
    }
}