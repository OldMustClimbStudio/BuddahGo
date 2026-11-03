using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Prayer bell rope (鈴緒) in front of the hall door: a red-and-white twisted rope down to the floor,
    /// hanging from the bell cluster, which itself swings from the hook. Simulated as a verlet chain
    /// whose first link is the (heavy) bell cluster. In the door view the rope can be clicked (a tug) or
    /// grabbed and swung; the bells ring when they swing hard. The camera walking in pushes it aside.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MenuBellRope : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private MenuCameraDirector director;
        [SerializeField] private MenuRoomView roomView;

        [Header("Bells")]
        [Tooltip("Bell cluster; its origin is the hook it swings from.")]
        [SerializeField] private Transform bells;
        [Tooltip("Where the rope hangs from, in the bell cluster's local space.")]
        [SerializeField] private Vector3 anchorLocal;
        [SerializeField, Min(1f)] private float bellWeight = 2.5f;

        [Header("Rope")]
        [SerializeField, Min(0.2f)] private float length = 2.1f;
        [SerializeField, Range(8, 48)] private int segments = 28;
        [SerializeField, Min(0.005f)] private float strandRadius = 0.022f;
        [SerializeField, Min(0f)] private float twistRadius = 0.02f;
        [Tooltip("Twists per metre.")]
        [SerializeField, Min(0f)] private float twistRate = 7f;
        [SerializeField] private float floorY = 2.26f;
        [Tooltip("The rope stays in front of this plane (the door and wall face).")]
        [SerializeField] private Vector3 wallPoint;
        [SerializeField] private Vector3 wallNormal = Vector3.forward;
        [SerializeField, Range(0.9f, 1f)] private float damping = 0.996f;
        [SerializeField, Range(0f, 1f)] private float floorFriction = 0.35f;

        [Header("Interaction")]
        [SerializeField, Min(2f)] private float pickPixels = 22f;
        [SerializeField, Min(1f)] private float clickSlopPixels = 6f;
        [SerializeField, Min(0f)] private float tugSpeed = 2.6f;
        [Tooltip("Top speed of any rope point (m/s); keeps a hard pull from flinging the rope.")]
        [SerializeField, Min(0.1f)] private float maxSpeed = 2.2f;
        [Tooltip("Share of the rope's speed kept when the hand lets go.")]
        [SerializeField, Range(0f, 1f)] private float releaseKeep = 0.35f;

        [Header("Pull (rings the bells, then fires onPulled)")]
        [Tooltip("How far (pixels) the pointer must travel down once the rope is taut.")]
        [SerializeField, Min(5f)] private float pullPixels = 90f;
        [Tooltip("The grabbed point counts as taut beyond this share of its reach from the hook.")]
        [SerializeField, Range(0.5f, 1f)] private float tautShare = 0.96f;
        [Tooltip("Seconds the bells ring before onPulled fires.")]
        [SerializeField, Min(0f)] private float pulledDelay = 0.6f;
        [SerializeField] private UnityEngine.Events.UnityEvent onPulled = new UnityEngine.Events.UnityEvent();

        [Header("Pull feel")]
        [Tooltip("Metres the bell cluster is drawn down at a full pull (its cord gives a little).")]
        [SerializeField, Min(0f)] private float bellDip = 0.09f;
        [SerializeField, Min(1f)] private float dipSpring = 90f;
        [SerializeField, Min(0f)] private float dipDamping = 7f;
        [Tooltip("Hint shown next to the pointer over the rope; its fill shows how far the rope is pulled.")]
        [SerializeField] private RectTransform hint;
        [SerializeField] private UnityEngine.UI.Image hintFill;
        [SerializeField] private CanvasGroup hintGroup;
        [SerializeField] private Vector2 hintOffset = new Vector2(150f, 10f);
        [Tooltip("Spheres that push the rope aside (the camera walking through the door).")]
        [SerializeField] private Transform[] pushers;
        [SerializeField, Min(0f)] private float pusherRadius = 0.35f;

        [Header("Sound")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Bell swing (degrees per second) that rings at full volume.")]
        [SerializeField, Min(1f)] private float fullRingSpeed = 120f;
        [SerializeField, Min(0f)] private float ringThreshold = 18f;
        [SerializeField, Min(0.02f)] private float ringInterval = 0.16f;

        private const float Step = 1f / 120f;
        private const int Iterations = 14;
        private const int RingSides = 6;
        private const int SamplesPerLink = 3;

        private Vector3[] _pos, _prev;
        private float[] _invMass;
        private float _link, _bellLink;
        private Quaternion _restRotation;
        private Vector3 _restDir;
        private float _accumulator;
        private Mesh _mesh;
        private Vector3[] _verts, _normals;
        private Vector3[] _samples;
        private int _grabbed = -1;
        private float _grabDepth;
        private Vector2 _pressPosition;
        private bool _dragged;
        private float _tautY = float.NaN;   // pointer height where the rope first went taut during this grab
        private bool _pulled;
        private float _pullProgress;        // 0..1 of the way from taut to a full pull
        private int _pullStage;
        private float _dip, _dipVelocity;
        private Vector3 _bellScale;
        private float _hintAlpha;
        private Vector3 _lastBellDir;
        private float _ringCooldown;
        private AudioClip[] _clips;

        /// <summary>True while the rope can be clicked or grabbed (door view, doors closed, camera still).</summary>
        public bool IsInteractive =>
            Application.isPlaying && director != null && director.InRoomView && !director.IsMoving
            && (roomView == null || !roomView.IsInside) && !_pulled;

        /// <summary>Fired a moment after the rope has been pulled down (the bells have rung): creates the room.</summary>
        public UnityEngine.Events.UnityEvent Pulled => onPulled;

        public void SetHint(RectTransform panel, UnityEngine.UI.Image fill, CanvasGroup group)
        {
            hint = panel; hintFill = fill; hintGroup = group;
        }

        public void Configure(Camera cam, MenuCameraDirector dir, MenuRoomView room, Transform bellCluster, Vector3 anchor,
                              float ropeLength, float floor, Vector3 wallP, Vector3 wallN, AudioSource source, Transform[] push)
        {
            targetCamera = cam; director = dir; roomView = room; bells = bellCluster; anchorLocal = anchor;
            length = ropeLength; floorY = floor; wallPoint = wallP; wallNormal = wallN; audioSource = source; pushers = push;
        }

        private void OnEnable()
        {
            if (bells == null) return;
            _restRotation = bells.rotation;
            _bellScale = bells.localScale;
            _restDir = (bells.TransformPoint(anchorLocal) - bells.position).normalized;
            _ringCooldown = 1f;   // no ringing while the rope settles
            BuildMesh();
            ResetChain();
            UpdateMesh();
        }

        private void OnDisable()
        {
            if (bells != null && _pos != null) { bells.rotation = _restRotation; bells.localScale = _bellScale; }
            if (_mesh != null) { DestroyImmediate(_mesh); _mesh = null; }
        }

        private Vector3 Hook => bells.position;

        /// <summary>Hangs the rope straight down from the bells, the slack lying on the floor towards the viewer.</summary>
        private void ResetChain()
        {
            int n = segments + 2;   // hook, bell anchor, rope nodes
            _pos = new Vector3[n]; _prev = new Vector3[n]; _invMass = new float[n];
            Vector3 hook = Hook, anchor = bells.TransformPoint(anchorLocal);
            _bellLink = Vector3.Distance(hook, anchor);
            _link = length / segments;
            _pos[0] = hook; _invMass[0] = 0f;
            _pos[1] = anchor; _invMass[1] = 1f / bellWeight;
            Vector3 n0 = wallNormal.sqrMagnitude > 0f ? wallNormal.normalized : Vector3.forward;
            for (int i = 2; i < n; i++)
            {
                Vector3 p = _pos[i - 1] + Vector3.down * _link;
                if (p.y < floorY + strandRadius) p = new Vector3(_pos[i - 1].x, floorY + strandRadius, _pos[i - 1].z) + n0 * _link;
                _pos[i] = p; _invMass[i] = 1f;
            }
            System.Array.Copy(_pos, _prev, n);
            _lastBellDir = (anchor - hook).normalized;
        }

        private void Update()
        {
            if (bells == null || _pos == null) return;
            if (!Application.isPlaying) { UpdateMesh(); return; }

            HandlePointer();
            UpdateDip(Time.deltaTime);
            UpdateHint(Time.deltaTime);
            _accumulator = Mathf.Min(_accumulator + Time.deltaTime, Step * 8);
            while (_accumulator >= Step)
            {
                Simulate(Step);
                _accumulator -= Step;
            }
            SwingBells();
            UpdateMesh();
        }

        private void Simulate(float dt)
        {
            Vector3 g = Physics.gravity * dt * dt;
            _pos[0] = Hook;
            for (int i = 1; i < _pos.Length; i++)
            {
                if (i == _grabbed) continue;
                Vector3 v = Vector3.ClampMagnitude((_pos[i] - _prev[i]) * damping, maxSpeed * dt);
                _prev[i] = _pos[i];
                _pos[i] += v + g;
            }
            if (_grabbed > 0)
            {
                Vector2 pointer = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                Vector3 target = targetCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, _grabDepth));
                // The hand cannot pull the rope longer than it is: past its reach the rope would stretch
                // and then snap back up when released.
                float reach = BellLink + (_grabbed - 1) * _link;
                target = _pos[0] + Vector3.ClampMagnitude(target - _pos[0], reach * 0.995f);
                _prev[_grabbed] = _pos[_grabbed];
                _pos[_grabbed] = Vector3.MoveTowards(_pos[_grabbed], Vector3.Lerp(_pos[_grabbed], target, 0.5f), maxSpeed * dt);
            }

            Vector3 wn = wallNormal.sqrMagnitude > 0f ? wallNormal.normalized : Vector3.forward;
            for (int k = 0; k < Iterations; k++)
            {
                for (int i = 0; i < _pos.Length - 1; i++)
                    Satisfy(i, i + 1, i == 0 ? BellLink : _link);
                for (int i = 1; i < _pos.Length; i++)
                {
                    Vector3 p = _pos[i];
                    float floor = floorY + strandRadius;
                    if (p.y < floor)
                    {
                        p.y = floor;
                        // friction: the part lying on the floor slides reluctantly
                        _prev[i] = Vector3.Lerp(_prev[i], new Vector3(p.x, _prev[i].y, p.z), floorFriction);
                    }
                    float d = Vector3.Dot(p - wallPoint, wn) - strandRadius * 2f;
                    if (d < 0f) p -= wn * d;
                    if (pushers != null)
                        foreach (Transform pusher in pushers)
                        {
                            if (pusher == null) continue;
                            Vector3 off = p - pusher.position;
                            float dist = off.magnitude;
                            if (dist >= pusherRadius) continue;
                            // push sideways along the wall so the rope parts instead of sticking to the face
                            Vector3 side = Vector3.ProjectOnPlane(off, wn);
                            if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(wn, Vector3.up);
                            p = pusher.position + side.normalized * pusherRadius + Vector3.Project(off, wn);
                        }
                    _pos[i] = p;
                }
            }
        }

        private void Satisfy(int a, int b, float rest)
        {
            float wa = _invMass[a], wb = _invMass[b];
            if (a == _grabbed) wa = 0f;
            if (b == _grabbed) wb = 0f;
            float w = wa + wb;
            if (w <= 0f) return;
            Vector3 delta = _pos[b] - _pos[a];
            float dist = delta.magnitude;
            if (dist < 1e-6f) return;
            Vector3 corr = delta * ((dist - rest) / (dist * w));
            _pos[a] += corr * wa;
            _pos[b] -= corr * wb;
        }

        private void SwingBells()
        {
            Vector3 dir = (_pos[1] - _pos[0]).normalized;
            bells.rotation = Quaternion.FromToRotation(_restDir, dir) * _restRotation;

            float speed = Vector3.Angle(_lastBellDir, dir) / Mathf.Max(Time.deltaTime, 1e-4f);
            _lastBellDir = dir;
            _ringCooldown -= Time.deltaTime;
            if (speed > ringThreshold && _ringCooldown <= 0f) Ring(Mathf.Clamp01(speed / fullRingSpeed));
        }

        private void HandlePointer()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || targetCamera == null) return;
            if (_grabbed > 0 && (!mouse.leftButton.isPressed || !IsInteractive))
            {
                if (_dragged) Release();
                else Tug();
                _grabbed = -1;
                _pullProgress = 0f;   // the bells spring back up
                return;
            }
            if (_grabbed > 0)
            {
                Vector2 pointer = mouse.position.ReadValue();
                if ((pointer - _pressPosition).sqrMagnitude > clickSlopPixels * clickSlopPixels) _dragged = true;
                // Pull: once the rope is taut, keep dragging down. The bells give a little on the way
                // (see UpdateDip) and jingle softly at each third.
                float reach = BellLink + (_grabbed - 1) * _link;
                bool taut = Vector3.Distance(_pos[0], _pos[_grabbed]) >= reach * tautShare;
                if (taut && float.IsNaN(_tautY)) _tautY = pointer.y;
                if (!taut && _pullProgress <= 0f) _tautY = float.NaN;
                _pullProgress = float.IsNaN(_tautY) ? 0f : Mathf.Clamp01((_tautY - pointer.y) / pullPixels);
                int stage = Mathf.FloorToInt(_pullProgress * 3f);
                if (stage > _pullStage && stage < 3) Ring(0.15f + 0.12f * stage);
                _pullStage = Mathf.Max(_pullStage, stage);
                if (_pullProgress >= 1f) Pull();
                return;
            }
            if (!IsInteractive || !mouse.leftButton.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            int node = NodeUnder(mouse.position.ReadValue());
            if (node < 0) return;
            _grabbed = node;
            _grabDepth = targetCamera.WorldToScreenPoint(_pos[node]).z;
            _pressPosition = mouse.position.ReadValue();
            _dragged = false;
            _tautY = float.NaN;
            _pullStage = 0;
        }

        /// <summary>Hook-to-anchor length: the bell cord plus how far the pull has drawn it down.</summary>
        private float BellLink => _bellLink + _dip;

        /// <summary>The bells follow the pull down and spring back (with a little overshoot) when let go.</summary>
        private void UpdateDip(float dt)
        {
            float target = _grabbed > 0 ? bellDip * _pullProgress : 0f;
            _dipVelocity += ((target - _dip) * dipSpring - _dipVelocity * dipDamping) * dt;
            _dip += _dipVelocity * dt;
            Vector3 s = _bellScale;
            s.y *= BellLink / Mathf.Max(_bellLink, 1e-3f);   // the cord stretches; the hook stays put
            bells.localScale = s;
        }

        private void UpdateHint(float dt)
        {
            if (hint == null) return;
            Mouse mouse = Mouse.current;
            bool show = mouse != null && IsInteractive && (_grabbed > 0 || NodeUnder(mouse.position.ReadValue()) >= 0);
            _hintAlpha = Mathf.MoveTowards(_hintAlpha, show ? 1f : 0f, dt * 6f);
            if (hintGroup != null) hintGroup.alpha = _hintAlpha;
            if (_hintAlpha <= 0f) return;
            if (mouse != null) hint.position = mouse.position.ReadValue() + hintOffset;
            if (hintFill != null) hintFill.fillAmount = Mathf.MoveTowards(hintFill.fillAmount, _grabbed > 0 ? _pullProgress : 0f, dt * 4f);
        }

        /// <summary>The rope has been pulled down: let go, shake and ring the bells, then fire onPulled.</summary>
        private void Pull()
        {
            _pulled = true;
            _grabbed = -1;
            _pullProgress = 0f;
            if (hintFill != null) hintFill.fillAmount = 1f;
            Release();
            Vector3 side = Vector3.Cross(wallNormal.normalized, Vector3.up) * (Random.value < 0.5f ? -1f : 1f);
            for (int i = 1; i < Mathf.Min(_pos.Length, 8); i++)
                _prev[i] = _pos[i] - side * (tugSpeed * 0.8f * Step * (1f - i / 8f));
            Ring(1f);
            StartCoroutine(AfterPull());
        }

        private System.Collections.IEnumerator AfterPull()
        {
            float t = 0f;
            while (t < pulledDelay)
            {
                t += Time.deltaTime;
                // keep the bells ringing while they settle
                if (_ringCooldown <= 0f) Ring(0.6f);
                yield return null;
            }
            onPulled?.Invoke();
            // Pulling again only makes sense once the camera is back at the door.
            while (roomView != null && (roomView.IsInside || (director != null && director.IsMoving))) yield return null;
            _pulled = false;
        }

        /// <summary>Nearest rope node (never the hook) to the pointer, within the pick radius.</summary>
        public int NodeUnder(Vector2 screen)
        {
            if (_pos == null) return -1;
            int best = -1;
            float bestDist = pickPixels;
            for (int i = 1; i < _pos.Length - 1; i++)
            {
                Vector3 a = targetCamera.WorldToScreenPoint(_pos[i]), b = targetCamera.WorldToScreenPoint(_pos[i + 1]);
                if (a.z <= 0f || b.z <= 0f) continue;
                Vector2 ab = (Vector2)b - (Vector2)a;
                float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(screen - (Vector2)a, ab) / ab.sqrMagnitude) : 0f;
                float d = Vector2.Distance(screen, (Vector2)a + ab * t);
                if (d < bestDist) { bestDist = d; best = t < 0.5f ? i : i + 1; }
            }
            return best;
        }

        /// <summary>Letting go after a drag: the rope keeps only part of its speed and swings out gently.</summary>
        private void Release()
        {
            for (int i = 1; i < _pos.Length; i++)
                _prev[i] = Vector3.Lerp(_pos[i], _prev[i], releaseKeep);
        }

        /// <summary>A short pull and release: the rope jerks sideways and the bells ring.</summary>
        private void Tug()
        {
            Vector3 side = Vector3.Cross(wallNormal.normalized, Vector3.up) * (Random.value < 0.5f ? -1f : 1f);
            for (int i = 1; i < _pos.Length; i++)
            {
                float k = 1f - Mathf.Abs(i - _grabbed) / (float)_pos.Length;
                _prev[i] = _pos[i] - (side * tugSpeed + wallNormal.normalized * tugSpeed * 0.3f) * k * Step;
            }
            Ring(0.8f);
        }

        private void Ring(float volume)
        {
            _ringCooldown = ringInterval;
            if (audioSource == null) return;
            if (_clips == null) _clips = new[] { BellClip(1), BellClip(2), BellClip(3) };
            audioSource.pitch = Random.Range(0.96f, 1.05f);
            audioSource.PlayOneShot(_clips[Random.Range(0, _clips.Length)], Mathf.Lerp(0.25f, 1f, volume));
        }

        /// <summary>Synthesised suzu jingle: three small bells with inharmonic partials and a rattling pellet.</summary>
        private static AudioClip BellClip(int seed)
        {
            const int rate = 44100;
            const float seconds = 1.4f;
            var rnd = new System.Random(seed * 7919);
            int n = (int)(rate * seconds);
            var data = new float[n];
            float[] bases = { 1880f, 2420f, 2960f };
            float[] ratios = { 1f, 2.32f, 4.25f, 6.63f };
            float[] amps = { 1f, 0.45f, 0.22f, 0.1f };
            foreach (float f0 in bases)
            {
                float onset = (float)rnd.NextDouble() * 0.07f;
                float detune = 1f + ((float)rnd.NextDouble() - 0.5f) * 0.03f;
                int hits = 3 + rnd.Next(3);
                for (int h = 0; h < hits; h++)
                {
                    float t0 = onset + h * (0.045f + (float)rnd.NextDouble() * 0.05f);
                    float gain = Mathf.Pow(0.55f, h);
                    int start = (int)(t0 * rate);
                    for (int i = start; i < n; i++)
                    {
                        float t = (i - start) / (float)rate;
                        float env = Mathf.Exp(-t * 4.2f) * Mathf.Min(1f, t * 900f);
                        if (env < 1e-4f) break;
                        float s = 0f;
                        for (int p = 0; p < ratios.Length; p++)
                            s += amps[p] * Mathf.Sin(2f * Mathf.PI * f0 * detune * ratios[p] * t) * Mathf.Exp(-t * p * 2.5f);
                        data[i] += s * env * gain * 0.12f;
                    }
                }
            }
            var clip = AudioClip.Create("SuzuJingle" + seed, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ---- mesh: two strands twisted round the chain ----

        private void BuildMesh()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "BellRope", hideFlags = HideFlags.DontSave };
                _mesh.MarkDynamic();
                GetComponent<MeshFilter>().sharedMesh = _mesh;
            }
            int samples = (segments + 1) * SamplesPerLink + 1;
            _samples = new Vector3[samples];
            int vertsPerStrand = samples * RingSides;
            _verts = new Vector3[vertsPerStrand * 2];
            _normals = new Vector3[_verts.Length];
            _mesh.Clear();
            _mesh.vertices = _verts;
            _mesh.subMeshCount = 2;
            for (int strand = 0; strand < 2; strand++)
            {
                var tris = new int[(samples - 1) * RingSides * 6 + (RingSides - 2) * 6];
                int k = 0, o = strand * vertsPerStrand;
                for (int s = 0; s < samples - 1; s++)
                    for (int j = 0; j < RingSides; j++)
                    {
                        int a = o + s * RingSides + j, b = o + s * RingSides + (j + 1) % RingSides;
                        int c = a + RingSides, d = b + RingSides;
                        tris[k++] = a; tris[k++] = c; tris[k++] = b;
                        tris[k++] = b; tris[k++] = c; tris[k++] = d;
                    }
                for (int j = 1; j < RingSides - 1; j++)
                {
                    tris[k++] = o; tris[k++] = o + j + 1; tris[k++] = o + j;
                    int e = o + (samples - 1) * RingSides;
                    tris[k++] = e; tris[k++] = e + j; tris[k++] = e + j + 1;
                }
                _mesh.SetTriangles(tris, strand);
            }
        }

        private void UpdateMesh()
        {
            if (_mesh == null || _pos == null) return;
            // rope nodes only (from the bell anchor down), smoothed with Catmull-Rom
            int nodes = _pos.Length - 1;
            int samples = _samples.Length;
            for (int s = 0; s < samples; s++)
            {
                float u = s / (float)(samples - 1) * (nodes - 1);
                int i = Mathf.Min((int)u, nodes - 2);
                float t = u - i;
                Vector3 p0 = Node(i - 1), p1 = Node(i), p2 = Node(i + 1), p3 = Node(i + 2);
                _samples[s] = transform.InverseTransformPoint(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t
                                                                       + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t));
            }
            Vector3 normal = Vector3.forward;
            float along = 0f;
            int per = samples * RingSides;
            for (int s = 0; s < samples; s++)
            {
                Vector3 tangent = (_samples[Mathf.Min(s + 1, samples - 1)] - _samples[Mathf.Max(s - 1, 0)]).normalized;
                if (tangent.sqrMagnitude < 0.5f) tangent = Vector3.down;
                normal = Vector3.ProjectOnPlane(normal, tangent);          // parallel transport keeps the twist steady
                if (normal.sqrMagnitude < 1e-6f) normal = Vector3.ProjectOnPlane(Vector3.right, tangent);
                normal.Normalize();
                Vector3 binormal = Vector3.Cross(tangent, normal);
                if (s > 0) along += Vector3.Distance(_samples[s], _samples[s - 1]);
                float twist = along * twistRate * Mathf.PI * 2f;
                for (int strand = 0; strand < 2; strand++)
                {
                    float a = twist + strand * Mathf.PI;
                    Vector3 centre = _samples[s] + (normal * Mathf.Cos(a) + binormal * Mathf.Sin(a)) * twistRadius;
                    for (int j = 0; j < RingSides; j++)
                    {
                        float b = j * Mathf.PI * 2f / RingSides;
                        Vector3 dir = normal * Mathf.Cos(b) + binormal * Mathf.Sin(b);
                        int v = strand * per + s * RingSides + j;
                        _verts[v] = centre + dir * strandRadius;
                        _normals[v] = dir;
                    }
                }
            }
            _mesh.vertices = _verts;
            _mesh.normals = _normals;
            _mesh.RecalculateBounds();
        }

        private Vector3 Node(int i) => _pos[Mathf.Clamp(i + 1, 1, _pos.Length - 1)];
    }
}
