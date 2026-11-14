using System;
using System.Collections.Generic;
using OutOfReach.Contracts;

namespace OutOfReach.Core
{
    /// <summary>
    /// Main-thread typed event bus for one mission session (implements the shared <see cref="IMissionEventBus"/>).
    /// <list type="bullet">
    /// <item>Each subscriber is isolated: its exception goes to the error sink and later subscribers still run.
    /// A failing subscriber never rolls back committed gameplay state.</item>
    /// <item>A message published while another is being dispatched (or inside <see cref="DeferDispatch"/>) is
    /// queued and delivered afterwards, first in, first out, so every subscriber sees events in commit order.</item>
    /// <item>Subscribing the same delegate twice is ignored, so a repeated OnEnable cannot double a listener.</item>
    /// <item>A handler unsubscribed during a dispatch is not invoked later in that dispatch.</item>
    /// <item><see cref="ClearAllSubscriptions"/> (session disposal or restart) drops listeners and undelivered messages.</item>
    /// </list>
    /// </summary>
    public sealed class MissionEventBus : IMissionEventBus
    {
        private readonly SubscriberList<PhaseChanged> phaseChanged = new SubscriberList<PhaseChanged>();
        private readonly SubscriberList<ResourceChanged> resourceChanged = new SubscriberList<ResourceChanged>();
        private readonly SubscriberList<SystemConditionChanged> systemConditionChanged = new SubscriberList<SystemConditionChanged>();
        private readonly SubscriberList<CommunicationChanged> communicationChanged = new SubscriberList<CommunicationChanged>();
        private readonly SubscriberList<CinematicRequested> cinematicRequested = new SubscriberList<CinematicRequested>();
        private readonly SubscriberList<CinematicFinished> cinematicFinished = new SubscriberList<CinematicFinished>();
        private readonly SubscriberList<AirlockStateChanged> airlockStateChanged = new SubscriberList<AirlockStateChanged>();
        private readonly SubscriberList<MissionEnded> missionEnded = new SubscriberList<MissionEnded>();

        private readonly Queue<Action> pending = new Queue<Action>();
        private readonly Action<Exception> errorSink;
        private readonly Action<Exception> reportFailure;
        private bool dispatching;
        private int deferDepth;

        /// <param name="subscriberErrorSink">Receives subscriber exceptions (Unity: Debug.LogException). Optional.</param>
        public MissionEventBus(Action<Exception> subscriberErrorSink = null)
        {
            errorSink = subscriberErrorSink;
            reportFailure = ReportFailure;
        }

        public event Action<PhaseChanged> PhaseChanged
        {
            add { phaseChanged.Add(value); }
            remove { phaseChanged.Remove(value); }
        }

        public event Action<ResourceChanged> ResourceChanged
        {
            add { resourceChanged.Add(value); }
            remove { resourceChanged.Remove(value); }
        }

        public event Action<SystemConditionChanged> SystemConditionChanged
        {
            add { systemConditionChanged.Add(value); }
            remove { systemConditionChanged.Remove(value); }
        }

        public event Action<CommunicationChanged> CommunicationChanged
        {
            add { communicationChanged.Add(value); }
            remove { communicationChanged.Remove(value); }
        }

        public event Action<CinematicRequested> CinematicRequested
        {
            add { cinematicRequested.Add(value); }
            remove { cinematicRequested.Remove(value); }
        }

        public event Action<CinematicFinished> CinematicFinished
        {
            add { cinematicFinished.Add(value); }
            remove { cinematicFinished.Remove(value); }
        }

        public event Action<AirlockStateChanged> AirlockStateChanged
        {
            add { airlockStateChanged.Add(value); }
            remove { airlockStateChanged.Remove(value); }
        }

        public event Action<MissionEnded> MissionEnded
        {
            add { missionEnded.Add(value); }
            remove { missionEnded.Remove(value); }
        }

        /// <summary>True while handlers are running; publishes made now are queued.</summary>
        public bool IsDispatching => dispatching;

        /// <summary>Number of subscriber exceptions caught since construction.</summary>
        public int SubscriberFailureCount { get; private set; }

        public Exception LastSubscriberFailure { get; private set; }

        public void Publish(PhaseChanged message)
        {
            Enqueue(phaseChanged, message);
        }

        public void Publish(ResourceChanged message)
        {
            Enqueue(resourceChanged, message);
        }

        public void Publish(SystemConditionChanged message)
        {
            Enqueue(systemConditionChanged, message);
        }

        public void Publish(CommunicationChanged message)
        {
            Enqueue(communicationChanged, message);
        }

        public void Publish(CinematicRequested message)
        {
            Enqueue(cinematicRequested, message);
        }

        public void Publish(CinematicFinished message)
        {
            Enqueue(cinematicFinished, message);
        }

        public void Publish(AirlockStateChanged message)
        {
            Enqueue(airlockStateChanged, message);
        }

        public void Publish(MissionEnded message)
        {
            Enqueue(missionEnded, message);
        }

        public int GetSubscriberCount(MissionEventKind kind)
        {
            switch (kind)
            {
                case MissionEventKind.PhaseChanged:
                    return phaseChanged.Count;
                case MissionEventKind.ResourceChanged:
                    return resourceChanged.Count;
                case MissionEventKind.SystemConditionChanged:
                    return systemConditionChanged.Count;
                case MissionEventKind.CommunicationChanged:
                    return communicationChanged.Count;
                case MissionEventKind.CinematicRequested:
                    return cinematicRequested.Count;
                case MissionEventKind.CinematicFinished:
                    return cinematicFinished.Count;
                case MissionEventKind.AirlockStateChanged:
                    return airlockStateChanged.Count;
                case MissionEventKind.MissionEnded:
                    return missionEnded.Count;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown event kind.");
            }
        }

        /// <summary>Total listeners across all channels.</summary>
        public int TotalSubscriberCount =>
            phaseChanged.Count + resourceChanged.Count + systemConditionChanged.Count + communicationChanged.Count
            + cinematicRequested.Count + cinematicFinished.Count + airlockStateChanged.Count + missionEnded.Count;

        /// <summary>
        /// Holds delivery until the returned scope is disposed, so a multi-part transaction (for example launch)
        /// commits every mutation before any subscriber observes it. Scopes nest.
        /// </summary>
        public IDisposable DeferDispatch()
        {
            deferDepth++;
            return new DispatchDeferral(this);
        }

        /// <summary>Removes every listener and drops undelivered messages. Used when a session ends.</summary>
        public void ClearAllSubscriptions()
        {
            phaseChanged.Clear();
            resourceChanged.Clear();
            systemConditionChanged.Clear();
            communicationChanged.Clear();
            cinematicRequested.Clear();
            cinematicFinished.Clear();
            airlockStateChanged.Clear();
            missionEnded.Clear();
            pending.Clear();
        }

        private void Enqueue<T>(SubscriberList<T> subscribers, T message)
        {
            pending.Enqueue(() => subscribers.Dispatch(message, reportFailure));
            if (!dispatching && deferDepth == 0)
            {
                Drain();
            }
        }

        private void Drain()
        {
            dispatching = true;
            try
            {
                while (pending.Count > 0)
                {
                    Action next = pending.Dequeue();
                    next();
                }
            }
            finally
            {
                dispatching = false;
            }
        }

        private void EndDeferral()
        {
            if (deferDepth > 0)
            {
                deferDepth--;
            }

            if (deferDepth == 0 && !dispatching)
            {
                Drain();
            }
        }

        private void ReportFailure(Exception exception)
        {
            SubscriberFailureCount++;
            LastSubscriberFailure = exception;
            if (errorSink == null)
            {
                return;
            }

            try
            {
                errorSink(exception);
            }
            catch (Exception)
            {
                // A broken error sink must never interrupt delivery to the remaining subscribers.
            }
        }

        private sealed class DispatchDeferral : IDisposable
        {
            private MissionEventBus bus;

            public DispatchDeferral(MissionEventBus bus)
            {
                this.bus = bus;
            }

            public void Dispose()
            {
                MissionEventBus owner = bus;
                if (owner == null)
                {
                    return;
                }

                bus = null;
                owner.EndDeferral();
            }
        }
    }
}
