namespace BuddahGo.Match
{
    // Consumed only by the authoritative input writer. Returning false preserves human input.
    public interface ISteeringOverride
    {
        bool TryGetOverride(out int steering, out bool drive);
    }
}
