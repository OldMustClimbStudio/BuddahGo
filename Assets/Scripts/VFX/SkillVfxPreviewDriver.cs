#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

namespace BuddahGo.VFX
{
    // Editor-only stand-in for the skill code when reviewing an effect in an empty scene (Play Mode): it reports the
    // same phase events in the same order as ChargedHandProjectileRuntime and Skill_PushProjectileHands, moves the
    // effect like the charged palm (speed falling to 20 % over the lifetime) and can freeze time at a chosen moment
    // so a frame can be captured. Never placed in a game scene.
    public class SkillVfxPreviewDriver : MonoBehaviour
    {
        public GameObject projectilePrefab;
        public GameObject buffPrefab;
        public Transform caster;
        public float buildUpSeconds = 1f;
        public float lifetimeSeconds = 3f;
        public float speed = 600f;
        public Vector3 colliderSize = new Vector3(60f, 90f, 20f);
        public float heightOffset = 30f;
        public float forwardOffset = 7f;
        [Tooltip("> 1: an anti-style burst in a lateral row, flying back through the caster.")]
        public int burstCount = 1;
        public float burstSpacing = 30f;
        [Tooltip("Extra forward spawn offset of a burst (Push table charged_anti forwardOffset).")]
        public float burstForwardExtra = 100f;
        [Tooltip("Seconds after the cast to set Time.timeScale to 0 (negative: never).")]
        public float freezeAt = -1f;
        public float timeScale = 1f;
        public bool repeat = true;
        [Tooltip("Advance exactly 1/60 s per frame (Time.captureDeltaTime), so captures land on the asked moment even when the first frames are slow.")]
        public bool fixedFrames;

        public float Elapsed { get; private set; }
        public bool Finished { get; private set; }

        IEnumerator Start()
        {
            Time.timeScale = timeScale;
            if (fixedFrames)
                Time.captureDeltaTime = 1f / 60f;
            do
            {
                float castTime = Time.time;
                SkillVfxPhaseController buff = null;
                if (buffPrefab != null && caster != null)
                {
                    buff = Instantiate(buffPrefab, caster).GetComponent<SkillVfxPhaseController>();
                    buff.transform.localPosition = Vector3.zero;
                    buff.Configure(new SkillVfxPhaseController.Context { caster = caster });
                    buff.ExtendBuff(buildUpSeconds + lifetimeSeconds + 1f);
                }
                yield return StartCoroutine(RunVolley(castTime));
                if (buff != null)
                    buff.DestroyWhenFinished(0f);
                yield return new WaitForSeconds(1.5f);
            }
            while (repeat && freezeAt < 0f);
            Finished = true;
        }

        void OnDestroy()
        {
            if (fixedFrames)
                Time.captureDeltaTime = 0f;
        }

        IEnumerator RunVolley(float castTime)
        {
            bool burst = burstCount > 1;
            Vector3 forward = caster != null ? caster.forward : Vector3.forward;
            Vector3 origin = caster != null ? caster.position : Vector3.zero;
            Vector3 direction = burst ? -forward : forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 anchor = origin + Vector3.up * heightOffset + forward * (burst ? forwardOffset + burstForwardExtra : forwardOffset);

            var palms = new SkillVfxPhaseController[burstCount];
            var starts = new Vector3[burstCount];
            for (int i = 0; i < burstCount; i++)
            {
                float offset = i - (burstCount - 1) * 0.5f;
                starts[i] = anchor + side * (offset * burstSpacing);
                palms[i] = Instantiate(projectilePrefab, starts[i], Quaternion.LookRotation(direction, Vector3.up)).GetComponent<SkillVfxPhaseController>();
                palms[i].Configure(new SkillVfxPhaseController.Context
                {
                    colliderSize = colliderSize,
                    caster = caster,
                    isLocalCaster = true,
                    burstIndex = i,
                    burstCenterOffsetWorld = anchor - starts[i],
                });
                palms[i].BeginPhase(SkillVfxPhase.Chg, buildUpSeconds);
            }

            float spawnTime = Time.time;
            bool armed = false;
            while (true)
            {
                Elapsed = Time.time - castTime;
                if (freezeAt >= 0f && Elapsed >= freezeAt)
                {
                    Time.timeScale = 0f;
                    yield break;
                }

                float t = Time.time - spawnTime;
                float progress = Mathf.Clamp01(t / Mathf.Max(1e-4f, buildUpSeconds * 0.8f));
                for (int i = 0; i < palms.Length; i++)
                {
                    if (palms[i] == null)
                        continue;
                    palms[i].SetChargeProgress(armed ? 1f : progress);
                }

                if (!armed && t >= buildUpSeconds)
                {
                    armed = true;
                    foreach (SkillVfxPhaseController palm in palms)
                    {
                        palm.EndPhase(SkillVfxPhase.Chg);
                        palm.BeginPhase(SkillVfxPhase.Imp);
                        palm.BeginPhase(SkillVfxPhase.Prj, lifetimeSeconds);
                    }
                }

                if (armed)
                {
                    float flight = Mathf.Clamp01((t - buildUpSeconds) / lifetimeSeconds);
                    float step = speed * Mathf.Lerp(1f, 0.2f, flight) * Time.deltaTime;
                    foreach (SkillVfxPhaseController palm in palms)
                    {
                        if (palm != null)
                            palm.transform.position += direction * step;
                    }
                    if (t >= buildUpSeconds + lifetimeSeconds)
                    {
                        foreach (SkillVfxPhaseController palm in palms)
                        {
                            if (palm != null)
                                palm.BeginDissipation();
                        }
                        // Keep counting so a freeze during the dissipation still happens.
                        while (freezeAt >= 0f && Time.time - castTime < freezeAt)
                        {
                            Elapsed = Time.time - castTime;
                            yield return null;
                        }
                        if (freezeAt >= 0f)
                        {
                            Elapsed = Time.time - castTime;
                            Time.timeScale = 0f;
                        }
                        yield break;
                    }
                }
                yield return null;
            }
        }
    }
}
#endif
