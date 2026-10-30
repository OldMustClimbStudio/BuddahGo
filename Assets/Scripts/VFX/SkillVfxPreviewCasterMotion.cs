#if UNITY_EDITOR
using UnityEngine;

namespace BuddahGo.VFX
{
    // Editor-only companion of SkillVfxPreviewDriver for effects that move with the caster (trails, buffs, zones):
    // drives the stand-in caster round a circle at racing speed, so a trail is drawn out and every facing is covered,
    // and keeps a chase camera behind it. Also switches the caster back on: the VFXtests player still carries a FishNet
    // NetworkObject, which FishNet disables in a scene without a NetworkManager. Never placed in a game scene.
    public class SkillVfxPreviewCasterMotion : MonoBehaviour
    {
        public Transform caster;
        [Tooltip("Units per second along the circle.")]
        public float speed = 150f;
        public float radius = 200f;
        [Tooltip("Optional chase camera, kept at chaseOffset in the caster's yaw frame.")]
        public Transform chaseCamera;
        public Vector3 chaseOffset = new Vector3(-40f, 35f, -70f);

        Vector3 _center;
        Vector3 _startRight;
        Vector3 _startForward;
        float _angle;

        void Start()
        {
            if (caster == null)
                return;
            caster.gameObject.SetActive(true);
            _startForward = Vector3.ProjectOnPlane(caster.forward, Vector3.up).normalized;
            _startRight = Vector3.Cross(Vector3.up, _startForward);
            _center = caster.position + _startRight * radius;
        }

        void Update()
        {
            if (caster == null)
                return;
            if (!caster.gameObject.activeSelf)
                caster.gameObject.SetActive(true);
            _angle += speed / Mathf.Max(1f, radius) * Time.deltaTime * Mathf.Rad2Deg;
            Quaternion turn = Quaternion.AngleAxis(_angle, Vector3.up);
            caster.SetPositionAndRotation(_center + turn * (-_startRight * radius), Quaternion.LookRotation(turn * _startForward, Vector3.up));
        }

        void LateUpdate()
        {
            if (caster == null || chaseCamera == null)
                return;
            Quaternion yaw = Quaternion.Euler(0f, caster.eulerAngles.y, 0f);
            float scale = Mathf.Abs(caster.lossyScale.y);
            chaseCamera.position = caster.position + yaw * (chaseOffset * scale);
            chaseCamera.LookAt(caster.position + Vector3.up * 8f * scale);
        }
    }
}
#endif
