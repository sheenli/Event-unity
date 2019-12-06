using System.Threading;
using UnityEngine;

namespace YKFramework.Event.Expample
{
    public class TestQueueEvent : MonoBehaviour
    {
        public void QueueEvent()
        {
            EventCore.Inst.QueueEvent(1);
        }

        public void QueueEventNow()
        {
            EventCore.Inst.QueueEventNow(1);
        }

        public void QueueEvent(string data)
        {
            EventCore.Inst.QueueEvent(1, data);
        }

        public void QueueEventThreadSafe()
        {
            new Thread(() => { EventCoreThreadSafe.Inst.QueueEvent(1); }).Start();
        }
    }
}