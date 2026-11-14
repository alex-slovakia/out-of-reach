namespace OutOfReach.Contracts
{
    /// <summary>Single-player target. Player checks line of sight and reach before invoking; target rechecks prerequisites.</summary>
    public interface IInteractable
    {
        string InteractionId { get; }
        string Prompt { get; }
        bool CanInteract { get; }
        CommandResult TryInteract();
    }
}
