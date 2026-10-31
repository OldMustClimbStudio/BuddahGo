using UnityEngine;

/// <summary>
/// What observers need to present one 缘起 trigger. Filled by the pair's OnTriggerServer (ctx.Presentation) and sent
/// once per trigger by YuanqiRegistry through SkillExecutor.PlayYuanqiObservers; every peer gets it in
/// YuanqiAction.PlayObservers and SkillExecutor.YuanqiObserved. Each pair fills only the fields it uses.
/// TargetObjectIds are NetworkObject ids (never null on the wire: the sender replaces null with an empty array).
/// </summary>
public struct YuanqiPresentationPayload
{
    public Vector3 Center;
    public float Radius;
    public float Seconds;
    public int[] TargetObjectIds;
    /// <summary>Flat world direction of the effect, zero when it has none (金足破阵: the burst's dash direction).</summary>
    public Vector3 Direction;
}
