/// <summary>
/// One skill execution on a caster's SkillStateBoard. Written by the board only; everyone else reads.
/// The window is [StartTick, UntilTick) in the server's LocalTick. EntryId increases per board, so the pair
/// (EntryIdA, EntryIdB) identifies one overlap ("一次重叠只触发一次").
/// </summary>
public sealed class SkillStateEntry
{
    public int EntryId;
    public string SkillId;
    public int Slot;
    public uint StartTick;
    public uint UntilTick;
    public ISkillRuntimeHandle Handle;

    /// <summary>The window started (Activated fired). False while a start offset is pending.</summary>
    public bool IsActivated;

    /// <summary>Ended fired (expiry, End, a recast superseding it, or Clear). Never active again.</summary>
    public bool IsEnded;

    /// <summary>Why the entry ended (log only); null while live.</summary>
    public string EndReason;

    public override string ToString() => $"{SkillId}#{EntryId} slot {Slot} [{StartTick}, {UntilTick})";
}
