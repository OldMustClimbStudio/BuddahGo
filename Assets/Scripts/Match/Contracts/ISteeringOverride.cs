namespace BuddahGo.Match
{
    // Consumed only by the authoritative input writer. Returning false preserves human input.
    // drive is the push switch the owner's W sets for a human (BuddahDriveInput): true pushes forward, false coasts.
    public interface ISteeringOverride
    {
        bool TryGetOverride(out int steering, out bool drive);
    }
}
