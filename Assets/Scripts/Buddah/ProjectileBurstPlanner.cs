using UnityEngine;

// Projectile hitbox size rule shared by the hand-push and charged projectiles. (The burst spawn planner and its
// ProjectileBurstParameters snapshot served only the retired *_anti reverse burst and were removed 2026-10-31.)
internal static class ProjectileBurstPlanner
{
    internal static Vector3 SanitizeColliderSize(Vector3 size)
    {
        return new Vector3(
            Mathf.Max(0.05f, Mathf.Abs(size.x)),
            Mathf.Max(0.05f, Mathf.Abs(size.y)),
            Mathf.Max(0.05f, Mathf.Abs(size.z)));
    }
}
