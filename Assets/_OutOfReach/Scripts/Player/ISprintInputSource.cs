namespace OutOfReach.Player
{
    /// <summary>Optional held sprint intent. Existing IPlayerInputSource implementations need not support it.</summary>
    public interface ISprintInputSource
    {
        bool SprintHeld { get; }
    }
}
