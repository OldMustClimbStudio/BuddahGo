using UnityEngine;

namespace BuddahGo.Debugging
{
    // Drop on the Buddah prefab root. References auto-resolve by name if unset.
    // Logs per-frame LateUpdate world positions + deltas for three samples:
    //   vr = VisualRoot transform (what FishNet smoother drives)
    //   fo = fotou transform (FBX armature bone, animation-targeted)
    //   bn = firstBone = SkinnedMeshRenderer.rootBone (actual skeleton root)
    // Also logs motor (Buddah root) delta as mo= for comparison: motor = raw
    // physics position before smoother. If mo oscillates within a tick block but
    // vr is smooth -> smoother is doing its job; if vr oscillates frame-to-frame
    // while mo is monotonic -> smoother output is the jitter source.
    // All logs tagged [VJitter] for grep.
    public class VisualJitterDiagnostic : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform fotou;
        [SerializeField] private Transform firstBone;
        [SerializeField] private int logEveryNFrames = 3;
        [SerializeField] private int heartbeatEveryNFrames = 120;

        private Transform _motorRoot;
        private Vector3 _lastVrPos;
        private Vector3 _lastFoPos;
        private Vector3 _lastBnPos;
        private Vector3 _lastMoPos;
        private bool _hasLast;
        private int _frameCounter;
        private int _lastEnabledLogFrame = -1;

        private void OnEnable()
        {
            TryResolveRefs();
            Debug.Log($"[VJitter] enabled on '{name}' f={Time.frameCount} vr={(visualRoot!=null?visualRoot.name:"NULL")} fo={(fotou!=null?fotou.name:"NULL")} bn={(firstBone!=null?firstBone.name:"NULL")} active={gameObject.activeInHierarchy}");
        }

        private void OnDisable()
        {
            Debug.Log($"[VJitter] disabled on '{name}' f={Time.frameCount}");
        }

        private void TryResolveRefs()
        {
            _motorRoot = transform;
            if (visualRoot == null) visualRoot = FindDeep(transform, "VisualRoot");
            if (fotou == null) fotou = FindDeep(transform, "fotou");
            if (firstBone == null)
            {
                var smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null) firstBone = smr.rootBone;
            }
        }

        private static Transform FindDeep(Transform root, string target)
        {
            if (root.name == target) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), target);
                if (found != null) return found;
            }
            return null;
        }

        private void LateUpdate()
        {
            if (visualRoot == null || fotou == null || firstBone == null)
            {
                TryResolveRefs();
                if ((Time.frameCount - _lastEnabledLogFrame) >= heartbeatEveryNFrames)
                {
                    Debug.Log($"[VJitter] refs-null f={Time.frameCount} vr={visualRoot!=null} fo={fotou!=null} bn={firstBone!=null} active={gameObject.activeInHierarchy}");
                    _lastEnabledLogFrame = Time.frameCount;
                }
                return;
            }

            if ((Time.frameCount - _lastEnabledLogFrame) >= heartbeatEveryNFrames)
            {
                string vrParentName = visualRoot.parent != null ? visualRoot.parent.name : "ROOT";
                string foParentName = fotou.parent != null ? fotou.parent.name : "ROOT";
                Debug.Log($"[VJitter] heartbeat f={Time.frameCount} vrParent={vrParentName} foParent={foParentName}");
                _lastEnabledLogFrame = Time.frameCount;
            }

            Vector3 vr = visualRoot.position;
            Vector3 fo = fotou.position;
            Vector3 bn = firstBone.position;
            Vector3 mo = _motorRoot.position;

            if (_hasLast)
            {
                Vector3 vrd = vr - _lastVrPos;
                Vector3 fod = fo - _lastFoPos;
                Vector3 bnd = bn - _lastBnPos;
                Vector3 mod = mo - _lastMoPos;

                if ((_frameCounter % Mathf.Max(1, logEveryNFrames)) == 0)
                {
                    Debug.Log(
                        $"[VJitter] f={Time.frameCount} dt={Time.deltaTime:F5} " +
                        $"mo=({mo.x:F3},{mo.y:F3},{mo.z:F3}) moMag={mod.magnitude:F5} " +
                        $"vr=({vr.x:F3},{vr.y:F3},{vr.z:F3}) vrMag={vrd.magnitude:F5} " +
                        $"fo=({fo.x:F3},{fo.y:F3},{fo.z:F3}) foMag={fod.magnitude:F5} " +
                        $"bn=({bn.x:F3},{bn.y:F3},{bn.z:F3}) bnMag={bnd.magnitude:F5}");
                }
            }

            _lastVrPos = vr;
            _lastFoPos = fo;
            _lastBnPos = bn;
            _lastMoPos = mo;
            _hasLast = true;
            _frameCounter++;
        }
    }
}
