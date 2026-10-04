#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;

namespace BuddahGo.AI
{
    // Temporarily remove only obstacle collision/visibility. Network roots keep their original
    // activation and scene identity; the four networked boxes are not dynamic spawnable prefabs.
    internal sealed class AITestObstacleScope : IDisposable
    {
        private readonly GameObject[] _objects;
        private readonly Collider[] _colliders;
        private readonly Renderer[] _renderers;
        private readonly bool[] _colliderEnabled, _rendererEnabled;
        private readonly Action<string, string> _record;
        private bool _restored, _disabled;
        public AITestObstacleScope(Action<string, string> record)
        {
            _record = record;
            string[] names = { "DebugboxCanPush", "DebugboxCanPush (1)", "DebugboxCanPush (2)", "DebugboxCanPush (3)", "DebugboxTriggerPush" };
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _objects = names.Select(name => all.Single(t => t.name == name && t.parent != null && t.parent.name == "DebugBox").gameObject).ToArray();
            _colliders = _objects.SelectMany(o => o.GetComponentsInChildren<Collider>(true)).Distinct().ToArray();
            _renderers = _objects.SelectMany(o => o.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
            _colliderEnabled = _colliders.Select(c => c.enabled).ToArray();
            _rendererEnabled = _renderers.Select(r => r.enabled).ToArray();
        }
        public void Disable()
        {
            if (_disabled) return;
            foreach (var obstacle in _objects) _record?.Invoke("test-box-disabled", obstacle.name
                + "; activeSelf=" + obstacle.activeSelf + "; activeInHierarchy=" + obstacle.activeInHierarchy + "; collision/visibility only");
            foreach (var collider in _colliders) collider.enabled = false;
            foreach (var renderer in _renderers) renderer.enabled = false;
            _disabled = true;
        }
        public void Dispose()
        {
            if (_restored || !_disabled) return; _restored = true;
            for (int i = 0; i < _colliders.Length; i++) if (_colliders[i] != null) _colliders[i].enabled = _colliderEnabled[i];
            for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _renderers[i].enabled = _rendererEnabled[i];
            for (int i = 0; i < _objects.Length; i++)
            {
                if (_objects[i] == null) continue;
                _record?.Invoke("test-box-restored", _objects[i].name + "; active=" + _objects[i].activeSelf
                    + "; activeInHierarchy=" + _objects[i].activeInHierarchy + "; component enable states restored");
            }
        }
    }
}
#endif
