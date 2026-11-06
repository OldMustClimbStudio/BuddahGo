#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

namespace BuddahGo.VFX
{
    // Editor-only stand-in for a dash 缘起 (金足破阵, Docs/vfx/yuanqi-giant-charge.md) in VFXtests, Play Mode: puts the
    // caster back at its start, sets its body scale, plays the slot on it like SkillVfxReplicator (Imp for the burst),
    // then drives the caster straight ahead with the burst's speed profile (cap + extra, easing back over the burst as
    // remaining share squared, as BuddahPredictedMotor) and loops. The race camera option keeps a camera at the game's
    // follow offset (PlayerCamera: base offset x body scale, fixed rotation, FOV 30). Never placed in a game scene.
    public class SkillVfxPreviewDash : MonoBehaviour
    {
        public GameObject slotPrefab;
        public Transform caster;
        [Tooltip("Body scale during the dash (法相金身 x4).")]
        public float bodyScale = 4f;
        [Tooltip("Body scale at the trigger: 4 when 法相金身 was cast first, 1 when 神足通 was (the body then grows over growSeconds).")]
        public float bodyScaleAtTrigger = 4f;
        public float growSeconds = 0.35f;
        [Tooltip("Base max speed (units/s) before and after the burst.")]
        public float baseSpeed = 80f;
        [Tooltip("YuanqiEffectGiantCharge.burstExtraMaxSpeed.")]
        public float burstExtraSpeed = 60f;
        [Tooltip("YuanqiEffectGiantCharge.burstSeconds: the slot's Imp length and the speed decay.")]
        public float burstSeconds = 0.8f;
        [Tooltip("Seconds from one trigger to the next.")]
        public float loopSeconds = 3.5f;
        [Tooltip("Lead-in before each trigger, caster standing.")]
        public float leadInSeconds = 0.5f;

        [Header("Race camera (optional)")]
        public Camera raceCamera;
        [Tooltip("CM_Gameplay follow offset at body scale 1, in the frame of the race start heading (RaceMap start grid, yaw -110).")]
        public Vector3 raceCameraOffset = new Vector3(77.34f, 60f, 6.6f);
        public Vector3 raceCameraEuler = new Vector3(35f, 258.4f, 0f);
        public float raceCameraHeadingYaw = -110f;
        public float raceCameraFov = 30f;

        public float Elapsed { get; private set; }

        Vector3 _start;
        Quaternion _startRotation;
        float _scale = 1f;

        IEnumerator Start()
        {
            if (caster == null || slotPrefab == null)
                yield break;
            caster.gameObject.SetActive(true);
            _start = caster.position;
            _startRotation = Quaternion.Euler(0f, caster.eulerAngles.y, 0f);
            while (true)
            {
                caster.SetPositionAndRotation(_start, _startRotation);
                SetScale(bodyScaleAtTrigger);
                yield return new WaitForSeconds(leadInSeconds);

                var slot = Instantiate(slotPrefab, caster.position, caster.rotation).GetComponent<SkillVfxPhaseController>();
                slot.Configure(new SkillVfxPhaseController.Context { caster = caster, isLocalCaster = true });
                slot.BeginPhase(SkillVfxPhase.Imp, burstSeconds);
                slot.DestroyWhenFinished(1.5f);

                Vector3 forward = _startRotation * Vector3.forward;
                float t = 0f;
                while (t < loopSeconds - leadInSeconds)
                {
                    yield return null;
                    t += Time.deltaTime;
                    Elapsed = t;
                    SetScale(Mathf.Lerp(bodyScaleAtTrigger, bodyScale, Mathf.Clamp01(t / Mathf.Max(1e-3f, growSeconds))));
                    caster.position = _start + forward * DashDistance(t);
                }
            }
        }

        // Distance after t seconds: speed = base + extra x (1 - t / burst)^2 during the burst, then base.
        public float DashDistance(float t)
        {
            float u = Mathf.Min(t, burstSeconds);
            float burst = burstSeconds > 0f
                ? burstExtraSpeed * burstSeconds / 3f * (1f - Mathf.Pow(1f - u / burstSeconds, 3f))
                : 0f;
            return baseSpeed * t + burst;
        }

        void SetScale(float scale)
        {
            _scale = scale;
            caster.localScale = Vector3.one * scale;
        }

        void LateUpdate()
        {
            if (raceCamera == null || caster == null)
                return;
            // The game camera's rotation is fixed in the world; keep the same relation to the dash heading.
            Quaternion toHeading = Quaternion.Euler(0f, caster.eulerAngles.y - raceCameraHeadingYaw, 0f);
            raceCamera.transform.SetPositionAndRotation(caster.position + toHeading * (raceCameraOffset * _scale),
                toHeading * Quaternion.Euler(raceCameraEuler));
            raceCamera.fieldOfView = raceCameraFov;
        }
    }
}
#endif
