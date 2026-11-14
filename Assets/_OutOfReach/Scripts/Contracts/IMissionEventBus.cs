using System;

namespace OutOfReach.Contracts
{
    /// <summary>
    /// Main-thread typed bus, owned by Core. Publish after committed mutations; subscribers must not
    /// re-enter gameplay commands during dispatch. Presentation subscribes on enable and unsubscribes
    /// on disable. Subscriber failures must not roll back gameplay or stop later subscribers.
    /// Only Core publishes authoritative state events and CinematicRequested. Presentation may publish
    /// CinematicFinished for its active request. Scene loading must not duplicate the bus.
    /// </summary>
    public interface IMissionEventBus
    {
        event Action<PhaseChanged> PhaseChanged;
        event Action<ResourceChanged> ResourceChanged;
        event Action<SystemConditionChanged> SystemConditionChanged;
        event Action<CommunicationChanged> CommunicationChanged;
        event Action<CinematicRequested> CinematicRequested;
        event Action<CinematicFinished> CinematicFinished;
        event Action<AirlockStateChanged> AirlockStateChanged;
        event Action<MissionEnded> MissionEnded;

        void Publish(PhaseChanged message);
        void Publish(ResourceChanged message);
        void Publish(SystemConditionChanged message);
        void Publish(CommunicationChanged message);
        void Publish(CinematicRequested message);
        void Publish(CinematicFinished message);
        void Publish(AirlockStateChanged message);
        void Publish(MissionEnded message);
    }
}
