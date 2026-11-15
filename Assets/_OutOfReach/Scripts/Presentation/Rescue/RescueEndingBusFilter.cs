using System;
using System.Collections.Generic;
using OutOfReach.Contracts;

namespace OutOfReach.Presentation.Rescue
{
    /// <summary>
    /// The view of the mission bus handed to the existing cinematic presenter. Everything passes through unchanged,
    /// except a RecoveryArrival request that the rescue presenter has taken over: that one request is not shown to
    /// the downstream listener, so only one presenter answers it. If the rescue presenter is missing, disabled,
    /// unconfigured or cannot present (no views, wrong phase/outcome), the request passes through and the
    /// existing tableau plays (fail open). Publishing is forwarded untouched; this filter never publishes.
    /// </summary>
    public sealed class RescueEndingBusFilter : IMissionEventBus
    {
        private readonly IMissionEventBus inner;
        private readonly Func<CinematicRequested, bool> takeOver;
        private readonly Dictionary<Action<CinematicRequested>, Action<CinematicRequested>> wrapped =
            new Dictionary<Action<CinematicRequested>, Action<CinematicRequested>>();

        public RescueEndingBusFilter(IMissionEventBus inner, Func<CinematicRequested, bool> takeOver)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.takeOver = takeOver ?? throw new ArgumentNullException(nameof(takeOver));
        }

        public IMissionEventBus Inner => inner;

        /// <summary>Requests withheld from downstream listeners so far (one per taken-over sequence and listener).</summary>
        public int WithheldRequests { get; private set; }

        public event Action<CinematicRequested> CinematicRequested
        {
            add
            {
                if (value == null || wrapped.ContainsKey(value)) return;
                Action<CinematicRequested> filter = request =>
                {
                    bool withhold;
                    try { withhold = request.Cinematic == CinematicId.RecoveryArrival && takeOver(request); }
                    catch { withhold = false; }
                    if (withhold) { WithheldRequests++; return; }
                    value(request);
                };
                wrapped.Add(value, filter);
                inner.CinematicRequested += filter;
            }
            remove
            {
                if (value == null || !wrapped.TryGetValue(value, out Action<CinematicRequested> filter)) return;
                wrapped.Remove(value);
                inner.CinematicRequested -= filter;
            }
        }

        public event Action<PhaseChanged> PhaseChanged { add => inner.PhaseChanged += value; remove => inner.PhaseChanged -= value; }
        public event Action<ResourceChanged> ResourceChanged { add => inner.ResourceChanged += value; remove => inner.ResourceChanged -= value; }
        public event Action<SystemConditionChanged> SystemConditionChanged { add => inner.SystemConditionChanged += value; remove => inner.SystemConditionChanged -= value; }
        public event Action<CommunicationChanged> CommunicationChanged { add => inner.CommunicationChanged += value; remove => inner.CommunicationChanged -= value; }
        public event Action<CinematicFinished> CinematicFinished { add => inner.CinematicFinished += value; remove => inner.CinematicFinished -= value; }
        public event Action<AirlockStateChanged> AirlockStateChanged { add => inner.AirlockStateChanged += value; remove => inner.AirlockStateChanged -= value; }
        public event Action<MissionEnded> MissionEnded { add => inner.MissionEnded += value; remove => inner.MissionEnded -= value; }

        public void Publish(PhaseChanged message) => inner.Publish(message);
        public void Publish(ResourceChanged message) => inner.Publish(message);
        public void Publish(SystemConditionChanged message) => inner.Publish(message);
        public void Publish(CommunicationChanged message) => inner.Publish(message);
        public void Publish(CinematicRequested message) => inner.Publish(message);
        public void Publish(CinematicFinished message) => inner.Publish(message);
        public void Publish(AirlockStateChanged message) => inner.Publish(message);
        public void Publish(MissionEnded message) => inner.Publish(message);
    }
}
