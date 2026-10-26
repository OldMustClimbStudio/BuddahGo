using UnityEngine;

/// <summary>
/// What observers need to draw one 缘起 trigger. Filled by the pair's OnTriggerServer (ctx.Presentation) and sent once
/// per trigger by YuanqiRegistry through SkillExecutor.PlayYuanqiObservers; each pair reads only the fields it uses.
/// TargetObjectIds are NetworkObject ids (never null on the wire: the sender replaces null with an empty array).
/// </summary>
public struct YuanqiPresentationPayload
{
    public Vector3 Center;
    public float Radius;
    public float Seconds;
    public int[] TargetObjectIds;
}
