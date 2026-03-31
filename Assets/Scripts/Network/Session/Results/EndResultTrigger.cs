using UnityEngine;

namespace SteamMultiplayer.Network.Results
{
    [RequireComponent(typeof(Collider))]
    public class EndResultTrigger : MonoBehaviour
    {
        [SerializeField] private bool _logTriggerEvents;

        private void Reset()
        {
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_logTriggerEvents)
                Debug.LogWarning("[EndResultTrigger] This trigger is deprecated. ResultScene now uses ResultDecisionManager + ResultDecisionUI instead of collider-based transitions.");
        }
    }
}
