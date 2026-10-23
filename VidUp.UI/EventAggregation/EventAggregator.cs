using System;
using System.Collections.Generic;
using Drexel.VidUp.Utils;

namespace Drexel.VidUp.UI.EventAggregation
{
    public class EventAggregator
    {
        private static EventAggregator instance;
        private static object instanceLocker = new object();

        private List<(Type eventType, Delegate method)> eventRegister = new List<(Type eventType, Delegate method)>();
        private object eventRegisterLocker = new object();

        static EventAggregator()
        {
        }

        private EventAggregator()
        {
        }

        public static EventAggregator Instance
        {
            get
            {
                lock (EventAggregator.instanceLocker)
                {
                    if (EventAggregator.instance == null)
                    {
                        EventAggregator.instance = new EventAggregator();
                    }
                    return EventAggregator.instance;
                }
            }
        
        }
        public Subscription Subscribe<T>(Action<T> action)
        {
            if (action != null)
            {
                lock (eventRegisterLocker)
                {
                    this.eventRegister.Add((typeof(T), action));
                }

                return new Subscription(() =>
                    {
                        //the removal must take the same lock as Subscribe and the
                        //snapshot creation in Publish, otherwise the register can be
                        //corrupted by concurrent modification (BUG-14)
                        lock (this.eventRegisterLocker)
                        {
                            this.eventRegister.Remove((typeof(T), action));
                        }
                    }
                );
            }

            return null;
        }
        public void Publish<T>(T data)
        {
            List<(Type eventType, Delegate method)> eventRegister = null;
            lock (eventRegisterLocker)
            {
                eventRegister= new List<(Type eventType, Delegate method)>(this.eventRegister);
            }

            foreach ((Type eventType, Delegate method) tEvent in eventRegister)
            {
                if (tEvent.eventType == typeof(T))
                {
                    try
                    {
                        ((Action<T>)tEvent.method)(data);
                    }
                    catch (Exception e)
                    {
                        //one failing handler must not stop the delivery to the
                        //remaining handlers nor propagate to the publisher (BUG-14)
                        Tracer.Write($"EventAggregator.Publish: A handler for {typeof(T).Name} threw an exception: {e.ToString()}");
                    }
                }
            }
        }
    }
}
