using UnityEngine;

/// <summary>
/// One 缘起 body impact as every peer sees it (SkillExecutor.YuanqiImpactObserved, raised on the caster's executor):
/// which 缘起 hit, the victim's NetworkObject id, the world contact point and the impulse routed to the victim.
/// Presentation only; the impulse itself went through BuddahPredictionRouter on the server.
/// </summary>
public struct YuanqiImpactInfo
{
    public string YuanqiId;
    public int VictimObjectId;
    public Vector3 Point;
    public Vector3 Impulse;
}
