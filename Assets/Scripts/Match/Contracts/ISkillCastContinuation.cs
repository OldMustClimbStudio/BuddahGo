namespace BuddahGo.Match
{
    // Optional input-source cancellation at confirmation. Receives no pending backfire draw.
    // The slot cooldown remains spent when cancelled (only a freeze interruption refunds it; there is no
    // global cast lock any more); normal player RPC rules are unchanged.
    public interface ISkillCastContinuation
    {
        bool CanContinueCast(int slotIndex);
    }
}
