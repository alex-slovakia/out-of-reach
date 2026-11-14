using System;
using System.Collections.Generic;
using OutOfReach.Contracts;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>Records every message delivered on a bus, in delivery order. Acts as a presentation listener.</summary>
    internal sealed class EventRecorder : IDisposable
    {
        private readonly IMissionEventBus bus;

        public EventRecorder(IMissionEventBus bus)
        {
            this.bus = bus;
            bus.PhaseChanged += OnPhaseChanged;
            bus.ResourceChanged += OnResourceChanged;
            bus.SystemConditionChanged += OnSystemConditionChanged;
            bus.CommunicationChanged += OnCommunicationChanged;
            bus.CinematicRequested += OnCinematicRequested;
            bus.CinematicFinished += OnCinematicFinished;
            bus.AirlockStateChanged += OnAirlockStateChanged;
            bus.MissionEnded += OnMissionEnded;
        }

        public List<string> Order { get; } = new List<string>();

        public List<PhaseChanged> Phases { get; } = new List<PhaseChanged>();

        public List<ResourceChanged> Resources { get; } = new List<ResourceChanged>();

        public List<CinematicRequested> CinematicRequests { get; } = new List<CinematicRequested>();

        public List<CinematicFinished> CinematicFinishes { get; } = new List<CinematicFinished>();

        public List<MissionEnded> Endings { get; } = new List<MissionEnded>();

        public int Total => Order.Count;

        public void Dispose()
        {
            bus.PhaseChanged -= OnPhaseChanged;
            bus.ResourceChanged -= OnResourceChanged;
            bus.SystemConditionChanged -= OnSystemConditionChanged;
            bus.CommunicationChanged -= OnCommunicationChanged;
            bus.CinematicRequested -= OnCinematicRequested;
            bus.CinematicFinished -= OnCinematicFinished;
            bus.AirlockStateChanged -= OnAirlockStateChanged;
            bus.MissionEnded -= OnMissionEnded;
        }

        private void OnPhaseChanged(PhaseChanged message)
        {
            Order.Add(nameof(PhaseChanged));
            Phases.Add(message);
        }

        private void OnResourceChanged(ResourceChanged message)
        {
            Order.Add(nameof(ResourceChanged));
            Resources.Add(message);
        }

        private void OnSystemConditionChanged(SystemConditionChanged message)
        {
            Order.Add(nameof(SystemConditionChanged));
        }

        private void OnCommunicationChanged(CommunicationChanged message)
        {
            Order.Add(nameof(CommunicationChanged));
        }

        private void OnCinematicRequested(CinematicRequested message)
        {
            Order.Add(nameof(CinematicRequested));
            CinematicRequests.Add(message);
        }

        private void OnCinematicFinished(CinematicFinished message)
        {
            Order.Add(nameof(CinematicFinished));
            CinematicFinishes.Add(message);
        }

        private void OnAirlockStateChanged(AirlockStateChanged message)
        {
            Order.Add(nameof(AirlockStateChanged));
        }

        private void OnMissionEnded(MissionEnded message)
        {
            Order.Add(nameof(MissionEnded));
            Endings.Add(message);
        }
    }
}
