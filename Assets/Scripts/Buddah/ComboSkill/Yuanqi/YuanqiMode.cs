/// <summary>How a 缘起 lasts once triggered.</summary>
public enum YuanqiMode
{
    /// <summary>One-shot effect at the trigger (#2 禅足冰封, #4 金足破阵).</summary>
    Instant,

    /// <summary>Holds while both entries stay active; OnEndServer runs when either ends (#1 金身巨掌).</summary>
    Sustained
}

/// <summary>When the same pair may trigger again.</summary>
public enum YuanqiRetrigger
{
    /// <summary>Once per overlap: only a new state-board entry of either skill can trigger the pair again (已定).</summary>
    OncePerOverlap
}
