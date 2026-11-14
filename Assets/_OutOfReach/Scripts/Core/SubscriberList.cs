using System;
using System.Collections.Generic;

namespace OutOfReach.Core
{
    /// <summary>
    /// Ordered, duplicate-free handler list for one event channel. Dispatch iterates a cached snapshot, isolates
    /// each handler's exceptions and skips handlers that were unsubscribed earlier in the same dispatch.
    /// Main thread only.
    /// </summary>
    internal sealed class SubscriberList<T>
    {
        private readonly List<Action<T>> handlers = new List<Action<T>>();
        private Action<T>[] snapshot = new Action<T>[0];
        private bool changed;

        public int Count => handlers.Count;

        public void Add(Action<T> handler)
        {
            if (handler == null || handlers.Contains(handler))
            {
                return;
            }

            handlers.Add(handler);
            changed = true;
        }

        public void Remove(Action<T> handler)
        {
            if (handler != null && handlers.Remove(handler))
            {
                changed = true;
            }
        }

        public void Clear()
        {
            if (handlers.Count == 0)
            {
                return;
            }

            handlers.Clear();
            changed = true;
        }

        public void Dispatch(T message, Action<Exception> reportFailure)
        {
            if (changed)
            {
                snapshot = handlers.ToArray();
                changed = false;
            }

            Action<T>[] current = snapshot;
            for (int i = 0; i < current.Length; i++)
            {
                Action<T> handler = current[i];
                if (changed && !handlers.Contains(handler))
                {
                    continue;
                }

                try
                {
                    handler(message);
                }
                catch (Exception exception)
                {
                    reportFailure(exception);
                }
            }
        }
    }
}
