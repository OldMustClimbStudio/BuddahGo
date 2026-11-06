#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

namespace BuddahGo.VFX
{
    // Editor-only stand-in for 禅足冰封 (缘起 #2, Docs/vfx/yuanqi-frost-lock.md) in VFXtests, Play Mode: plays the
    // frozen ground on the caster and the ice block on every frozen racer the way the game does (SkillVfxReplicator:
    // the slot at the racer, Imp for the freeze seconds), then loops. The race camera option keeps a camera at the
    // game's follow offset for the caster (PlayerCamera: base offset x body scale, fixed rotation, FOV 30), the view the
    // sizes are judged from. Never placed in a game scene.
    public class SkillVfxPreviewFrostLock : MonoBehaviour
    {
        public GameObject groundSlotPrefab;
        public GameObject iceSlotPrefab;
        public Transform caster;
        [Tooltip("Racers frozen by the trigger (a x4 one shows the giant's block).")]
        public Transform[] frozen;
        [Tooltip("YuanqiEffectFrostLock.freezeSeconds: the Imp length of both slots.")]
        public float freezeSeconds = 1.5f;
        [Tooltip("Seconds from one trigger to the next.")]
        public float loopSeconds = 3.5f;
        [Tooltip("Lead-in before each trigger.")]
        public float leadInSeconds = 0.5f;

        [Header("Race camera (optional)")]
        public Camera raceCamera;
        [Tooltip("CM_Gameplay follow offset at body scale 1, in the frame of the race start heading (RaceMap start grid, yaw -110).")]
        public Vector3 raceCameraOffset = new Vector3(77.34f, 60f, 6.6f);
        public Vector3 raceCameraEuler = new Vector3(35f, 258.4f, 0f);
        public float raceCameraHeadingYaw = -110f;
        public float raceCameraFov = 30f;

        public float Elapsed { get; private set; }

        IEnumerator Start()
        {
            if (caster == null)
                yield break;
            // The stand-in racers keep a NetworkObject that FishNet switches off in Play mode without a NetworkManager
            // (as SkillVfxPreviewDash does for its caster).
            caster.gameObject.SetActive(true);
            if (frozen != null)
                foreach (Transform racer in frozen)
                    if (racer != null)
                        racer.gameObject.SetActive(true);
            while (true)
            {
                yield return new WaitForSeconds(leadInSeconds);
                Play(groundSlotPrefab, caster);
                if (frozen != null)
                    foreach (Transform racer in frozen)
                        Play(iceSlotPrefab, racer);
                float t = 0f;
                while (t < loopSeconds - leadInSeconds)
                {
                    yield return null;
                    t += Time.deltaTime;
                    Elapsed = t;
                }
            }
        }

        void Play(GameObject prefab, Transform racer)
        {
            if (prefab == null || racer == null || !racer.gameObject.activeInHierarchy)
                return;
            var slot = Instantiate(prefab, racer.position, racer.rotation).GetComponent<SkillVfxPhaseController>();
            slot.Configure(new SkillVfxPhaseController.Context { caster = racer, isLocalCaster = racer == caster });
            slot.BeginPhase(SkillVfxPhase.Imp, freezeSeconds);
            slot.DestroyWhenFinished(1.5f);
        }

        void LateUpdate()
        {
            if (raceCamera == null || caster == null)
                return;
            float scale = Mathf.Abs(caster.lossyScale.y);
            Quaternion toHeading = Quaternion.Euler(0f, caster.eulerAngles.y - raceCameraHeadingYaw, 0f);
            raceCamera.transform.SetPositionAndRotation(caster.position + toHeading * (raceCameraOffset * scale),
                toHeading * Quaternion.Euler(raceCameraEuler));
            raceCamera.fieldOfView = raceCameraFov;
        }
    }
}
#endif
