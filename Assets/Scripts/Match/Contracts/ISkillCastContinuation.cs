namespace BuddahGo.Match
{
    // Optional input-source cancellation at confirmation. Receives no pending variant or random draw.
    // Cooldown/lock remain spent when cancelled; normal player RPC rules are unchanged.
    public interface ISkillCastContinuation
    {
        bool CanContinueCast(int slotIndex);
    }
}
