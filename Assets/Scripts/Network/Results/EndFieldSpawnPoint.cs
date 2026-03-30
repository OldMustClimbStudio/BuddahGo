using UnityEngine;

namespace SteamMultiplayer.Network.Results
{
    public class EndFieldSpawnPoint : MonoBehaviour
    {
        [SerializeField] private int spawnOrderIndex;
        [SerializeField] private string debugLabel = string.Empty;

        public int SpawnOrderIndex => spawnOrderIndex;
        public string DebugLabel => debugLabel;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.25f);
        }
    }
}
