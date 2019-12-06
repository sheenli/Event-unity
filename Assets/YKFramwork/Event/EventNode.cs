﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YKFramework.Event
{
    public class EventDispatcherNode : MonoBehaviour
    {
        /// <summary>
        ///     消息回调委托
        /// </summary>
        /// <param name="data">消息内容</param>
        /// <returns>是否中断消息发送</returns>
        public delegate void EventListenerDele(EventData data);

        /// <summary>
        ///     用于保存在逻辑帧开始的时候需要更新的ListenerPack
        /// </summary>
        private readonly Queue m_listenersToUpdate = new Queue();

        /// <summary>
        ///     所有的消息
        /// </summary>
        private readonly Dictionary<string, List<EventInfo>> mEventDic = new Dictionary<string, List<EventInfo>>();

        /// <summary>
        ///     用于在线程安全状态下防止对当前消息队列的同时读写
        /// </summary>
        protected string evtQueueLock = "lock";

        protected Queue evtQueueNext = new Queue();

        /// <summary>
        ///     两个消息队列buffer
        /// </summary>
        protected Queue m_eventQueueNow = new Queue();

        protected bool mIsValid = true;

        /// <summary>
        ///     是否线程安全
        /// </summary>
        public bool threadSafe;

        /// <summary>
        ///     当前节点是不是有效
        /// </summary>
        public virtual bool IsValid => gameObject != null && enabled && gameObject.activeInHierarchy;

        /// <summary>
        ///     是否存在这个监听函数
        /// </summary>
        /// <param name="type">事件类型</param>
        /// <param name="_listener">回调函数</param>
        /// <returns></returns>
        public bool HasListener(string type, EventListenerDele _listener)
        {
            if (mEventDic.ContainsKey(type))
                foreach (var listener in mEventDic[type])
                    if (listener.listener == _listener)
                        return true;

            return false;
        }

        /// <summary>
        ///     挂接一个监听eventKey消息的消息监听器
        ///     监听器将于下一逻辑帧执行前挂接
        /// </summary>
        /// <param name="type">消息id</param>
        /// <param name="_listener">回调函数</param>
        /// <param name="_priority">优先级</param>
        /// <param name="_dispatchOnce">是否只接受一次</param>
        public void AttachListener(int type, EventListenerDele _listener, int _priority = 0, bool _dispatchOnce = false)
        {
            AttachListener(type.ToString(), _listener, _priority, _dispatchOnce);
        }

        /// <summary>
        ///     挂接一个监听eventKey消息的消息监听器
        ///     监听器将于下一逻辑帧执行前挂接
        /// </summary>
        /// <param name="type">消息id</param>
        /// <param name="_listener">回调函数</param>
        /// <param name="_priority">优先级</param>
        /// <param name="_dispatchOnce">是否只接受一次</param>
        public void AttachListener(string type, EventListenerDele _listener, int _priority = 0,
            bool _dispatchOnce = false)
        {
            if (threadSafe)
                lock (m_listenersToUpdate)
                {
                    m_listenersToUpdate.Enqueue(
                        ListenerPack.Get(EventInfo.Get(type, _listener, _priority, _dispatchOnce), type, true));
                }
            else
                m_listenersToUpdate.Enqueue(ListenerPack.Get(EventInfo.Get(type, _listener, _priority, _dispatchOnce),
                    type, true));
        }


        public void DetachListener(int type, EventListenerDele _listener)
        {
            DetachListener(type.ToString(), _listener);
        }

        /// <summary>
        ///     摘除一个监听eventKey消息的消息监听器
        ///     监听器将于下一逻辑帧执行前摘除
        /// </summary>
        /// <param name="listener"></param>
        /// <param name="eventKey"></param>
        public void DetachListener(string type, EventListenerDele _listener)
        {
            if (threadSafe)
                lock (m_listenersToUpdate)
                {
                    m_listenersToUpdate.Enqueue(ListenerPack.Get(EventInfo.Get(type, _listener), type, false));
                }
            else
                m_listenersToUpdate.Enqueue(ListenerPack.Get(EventInfo.Get(type, _listener), type, false));
        }

        /// <summary>
        ///     执行一次针对eventKey的监听器挂接
        /// </summary>
        /// <param name="type">事件类型</param>
        /// <param name="_listener">回调函数</param>
        /// <param name="_priority">优先级</param>
        /// <param name="_dispatchOnce">是否只派发一次</param>
        private void AttachListenerNow(int type, EventListenerDele _listener, int _priority = 0,
            bool _dispatchOnce = false)
        {
            AttachListenerNow(type.ToString(), _listener, _priority, _dispatchOnce);
        }

        /// <summary>
        ///     执行一次针对eventKey的监听器挂接
        /// </summary>
        /// <param name="type">事件类型</param>
        /// <param name="_listener">回调函数</param>
        /// <param name="_priority">优先级</param>
        /// <param name="_dispatchOnce">是否只派发一次</param>
        private void AttachListenerNow(string type, EventListenerDele _listener, int _priority = 0,
            bool _dispatchOnce = false)
        {
            if (null == _listener || string.IsNullOrEmpty(type)) return;
            if (!mEventDic.ContainsKey(type)) mEventDic.Add(type, new List<EventInfo>());

            if (HasListener(type, _listener))
            {
                Debug.Log("LogicNode, AttachListenerNow: " + _listener + " is already in list for event: " +
                          type);
                return;
            }

            var listenerList = mEventDic[type];

            var ev = EventInfo.Get(type, _listener, _priority, _dispatchOnce);

            var pos = 0;
            var countListenerList = listenerList.Count;
            for (var n = 0; n < countListenerList; n++)
            {
                if (ev.priority > listenerList[n].priority) break;

                pos++;
            }

            listenerList.Insert(pos, ev);
        }

        /// <summary>
        ///     执行一次针对eventKey的监听器摘除
        /// </summary>
        /// <param name="type">事件类型</param>
        /// <param name="_listener">监听函数</param>
        /// <param name="_priority">优先级</param>
        /// <param name="_dispatchOnce">是否只执行一次</param>
        private void DetachListenerNow(string type, EventListenerDele _listener)
        {
            //listener == null is valid due to unexpected GameObject.Destroy
            if (string.IsNullOrEmpty(type) || _listener == null)
            {
                Debug.LogError("回调函数或者 事件类型不能为空");
                return;
            }

            if (!mEventDic.ContainsKey(type))
                return;

            var listenerList = mEventDic[type];
            EventInfo ev = null;
            foreach (var ei in listenerList)
                if (ei.listener == _listener)
                    ev = ei;

            if (ev != null)
            {
                listenerList.Remove(ev);
                EventInfo.Return(ev);
            }
        }

        /// <summary>
        ///     更新消息监听器表格
        /// </summary>
        private void UpdateListenerMap()
        {
            if (threadSafe)
                lock (m_listenersToUpdate)
                {
                    _UpdateListenerMap();
                }
            else
                _UpdateListenerMap();
        }

        /// <summary>
        ///     更新消息监听器表格的实现
        /// </summary>
        private void _UpdateListenerMap()
        {
            var countListenerPack = m_listenersToUpdate.Count;
            while (countListenerPack != 0)
            {
                var pack = (ListenerPack) m_listenersToUpdate.Dequeue();
                countListenerPack--;
                if (pack.addOrRemove)
                    AttachListenerNow(pack.eventKey, pack.listener.listener, pack.listener.priority,
                        pack.listener.dispatchOnce);
                else
                    DetachListenerNow(pack.eventKey, pack.listener.listener);

                ListenerPack.Return(pack);
            }
        }

        protected void Update()
        {
            UpdateListenerMap();
            DispatchEvent();
            OnUpdate();
        }

        protected virtual void OnUpdate()
        {
        }


        /// <summary>
        ///     消息对应的信息
        /// </summary>
        private class EventInfo
        {
            private static readonly Queue<EventInfo> mPools = new Queue<EventInfo>();

            /// <summary>
            ///     只发送一次
            /// </summary>
            public bool dispatchOnce;

            /// <summary>
            ///     事件类型
            /// </summary>
            public string eventType;

            /// <summary>
            ///     事件回调
            /// </summary>
            public EventListenerDele listener;

            /// <summary>
            ///     事件派发的优先级
            /// </summary>
            public int priority;

            private EventInfo(string type, EventListenerDele _listener, int _priority = 0, bool _dispatchOnce = false)
            {
                Set(type, _listener, _priority, _dispatchOnce);
            }

            public void Set(string type, EventListenerDele _listener, int _priority = 0, bool _dispatchOnce = false)
            {
                priority = _priority;
                dispatchOnce = _dispatchOnce;
                eventType = type;
                listener = _listener;
            }

            public void Clean()
            {
                listener = null;
                dispatchOnce = false;
                eventType = string.Empty;
                priority = 0;
            }

            public static EventInfo Get(string type, EventListenerDele _listener, int _priority = 0,
                bool _dispatchOnce = false)
            {
                EventInfo info = null;
                if (mPools.Count > 0)
                {
                    info = mPools.Dequeue();
                    info.Set(type, _listener, _priority, _dispatchOnce);
                }
                else
                {
                    info = new EventInfo(type, _listener, _priority, _dispatchOnce);
                }

                return info;
            }

            public static void Return(EventInfo info)
            {
                info.Clean();
                mPools.Enqueue(info);
            }
        }

        /// <summary>
        ///     用于在每逻辑帧开始的时候维护消息监听器序列
        /// </summary>
        private class ListenerPack
        {
            private static readonly Queue<ListenerPack> mListenerPackPools = new Queue<ListenerPack>();
            public bool addOrRemove;
            public string eventKey;
            public EventInfo listener;

            public ListenerPack(EventInfo lis, string eKey, bool addOrRe)
            {
                Set(lis, eKey, addOrRe);
            }

            public void Set(EventInfo lis, string eKey, bool addOrRe)
            {
                listener = lis;
                eventKey = eKey;
                addOrRemove = addOrRe;
            }

            public void Clean()
            {
                listener = null;
                eventKey = string.Empty;
                addOrRemove = false;
            }

            public static ListenerPack Get(EventInfo info, string eKey, bool addOrRe)
            {
                ListenerPack pack = null;
                if (mListenerPackPools.Count > 0)
                {
                    pack = mListenerPackPools.Dequeue();
                    pack.Set(info, eKey, addOrRe);
                }
                else
                {
                    pack = new ListenerPack(info, eKey, addOrRe);
                }

                return pack;
            }

            public static void Return(ListenerPack info)
            {
                info.Clean();
                mListenerPackPools.Enqueue(info);
            }
        }

        #region 派发消息相关

        /// <summary>
        ///     发送一个默认的int类型值
        /// </summary>
        /// <param name="key"></param>
        public void QueueEvent(string key)
        {
            QueueEvent(new EventData(key));
        }

        /// <summary>
        ///     发送一个默认的int类型值
        /// </summary>
        /// <param name="key"></param>
        public void QueueEvent(int key)
        {
            QueueEvent(key.ToString());
        }

        /// <summary>
        ///     发送一个默认的int类型值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void QueueEvent<T>(int key, T value)
        {
            QueueEvent(key.ToString(), value);
        }

        public void QueueEvent<T>(string key, T value)
        {
            var data = new EventData<T>(key, value);
            QueueEvent(data);
        }

        /// <summary>
        ///     线程安全
        ///     抛出消息进入下一逻辑帧
        /// </summary>
        public void QueueEvent(EventData data)
        {
            if (threadSafe)
                lock (evtQueueLock)
                {
                    evtQueueNext.Enqueue(data);
                }
            else
                QueueEventNow(data);
        }

        /// <summary>
        ///     只能在主线程调用 立即派发消息
        /// </summary>
        /// <param name="key">要发送的消息id</param>
        public void QueueEventNow(int key)
        {
            QueueEventNow(new EventData(key.ToString()));
        }

        /// <summary>
        ///     只能在主线程调用 立即派发消息
        /// </summary>
        /// <param name="key">要发送的消息id</param>
        public void QueueEventNow(string key)
        {
            QueueEventNow(new EventData(key));
        }

        /// <summary>
        ///     只能在主线程调用 立即派发消息
        /// </summary>
        /// <typeparam name="T">消息传入的参数</typeparam>
        /// <param name="key">要发送的消息id</param>
        /// <param name="value">要发的消息内容</param>
        public void QueueEventNow<T>(string key, T value)
        {
            QueueEventNow(new EventData<T>(key, value));
        }

        /// <summary>
        ///     只能在主线程调用 立即发送
        /// </summary>
        /// <param name="data"></param>
        public void QueueEventNow(EventData data)
        {
            if (threadSafe)
                lock (evtQueueLock)
                {
                    TriggerEvent(data);
                }
            else
                TriggerEvent(data);
        }


        /// <summary>
        ///     派发消息
        /// </summary>
        private void DispatchEvent()
        {
            if (threadSafe)
                _DispatchEventLock();
            else
                _DispatchEventNoLock();
        }

        private void _DispatchEventLock()
        {
            if (!IsValid) return;
            lock (evtQueueLock)
            {
                EventData evt = null;
                //发布自己的event
                var countEventQueue = m_eventQueueNow.Count;
                while (countEventQueue > 0)
                {
                    evt = m_eventQueueNow.Dequeue() as EventData;
                    countEventQueue--;
                    if (evt != null) TriggerEvent(evt);
                }
            }

            lock (evtQueueLock)
            {
                SwapEventQueue();
            }
        }

        private void _DispatchEventNoLock()
        {
            if (!IsValid) return;

            EventData evt = null;
            var countEventQueue = m_eventQueueNow.Count;
            while (countEventQueue > 0)
            {
                evt = m_eventQueueNow.Dequeue() as EventData;
                countEventQueue--;
                if (evt != null) TriggerEvent(evt);
            }

            //交换event缓冲//
            SwapEventQueue();
        }

        /// <summary>
        ///     交换消息队列
        /// </summary>
        private void SwapEventQueue()
        {
            var temp = m_eventQueueNow;
            m_eventQueueNow = evtQueueNext;
            evtQueueNext = temp;
        }

        /// <summary>
        ///     将消息派发给下携的消息监听器
        /// </summary>
        /// <param name="key"></param>
        /// <param name="param1"></param>
        /// <param name="param2"></param>
        /// <returns></returns>
        private void TriggerEvent(EventData data)
        {
            if (!mEventDic.ContainsKey(data.name)) return;

            var reList = new List<EventInfo>();

            var listenerList = mEventDic[data.name];
            var countListenerList = listenerList.Count;
            for (var n = 0; n < countListenerList; n++)
            {
                if (listenerList[n].dispatchOnce) reList.Add(listenerList[n]);

                if (listenerList[n].listener != null) listenerList[n].listener(data);

                if (data.isBreak) break;
            }

            for (var n = reList.Count - 1; n >= 0; n--) DetachListenerNow(reList[n].eventType, reList[n].listener);
        }

        #endregion
    }

    public class EventListenerMgr
    {
        private readonly List<EventListenerData> mlistener = new List<EventListenerData>();

        public void AddListener(EventDispatcherNode dis, EventDispatcherNode.EventListenerDele dele, int type
            , int _priority = 0, bool _dispatchOnce = false)
        {
            AddListener(dis, dele, type.ToString(), _priority, _dispatchOnce);
        }

        public void AddListener(EventDispatcherNode dis, EventDispatcherNode.EventListenerDele dele, string type
            , int _priority = 0, bool _dispatchOnce = false)
        {
            if (!dis.HasListener(type, dele))
            {
                var data = new EventListenerData(dis, dele, type);
                mlistener.Add(data);
                dis.AttachListener(type, dele, _priority, _dispatchOnce);
            }
            else
            {
                Debug.LogWarning("添加消息失败重复添加消息id=" + type);
            }
        }

        public void DetachListener(EventDispatcherNode dis, EventDispatcherNode.EventListenerDele dele, string type)
        {
            if (dis.HasListener(type, dele))
            {
                dis.DetachListener(type, dele);
                for (var i = 0; i < mlistener.Count; i++)
                {
                    var data = mlistener[i];
                    if (data.dis == dis && data.type == type && data.dele == dele)
                    {
                        mlistener.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        public void DetachListenerAll()
        {
            foreach (var data in mlistener) data.DetachListener();

            mlistener.Clear();
        }

        public class EventListenerData
        {
            public EventDispatcherNode.EventListenerDele dele;
            public EventDispatcherNode dis;
            public bool dispatchOnce;
            public int priority;
            public string type;

            public EventListenerData(EventDispatcherNode _dis,
                EventDispatcherNode.EventListenerDele _dele, string _type,
                int _priority = 0, bool _dispatchOnce = false)
            {
                dis = _dis;
                dele = _dele;
                type = _type;
                priority = _priority;
                dispatchOnce = _dispatchOnce;
            }

            public void DetachListener()
            {
                dis.DetachListener(type, dele);
            }
        }
    }
}