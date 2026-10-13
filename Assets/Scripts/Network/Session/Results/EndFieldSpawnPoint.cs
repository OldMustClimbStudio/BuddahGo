using UnityEngine;

namespace SteamMultiplayer.Network.Results
{
    public class EndFieldSpawnPoint : MonoBehaviour
    {
        [SerializeField] private int spawnOrderIndex;
        [SerializeField] private string debugLabel = string.Empty;

        public int SpawnOrderIndex => spawnOrderIndex;
        public string DebugLabel => debugLabel;

        private void Awake()
        {
            // Match/TrackResultAnchor owns the anchor transforms (rank = spawnOrderIndex + 1); scene placement is the fallback.
            ProjectConfigDatabase db = DesignTableOverlay.Db;
            if (db == null)
                return;
            string trackId = DesignTableOverlay.CurrentTrackId();
            TrackResultAnchorRecord row = DesignTableOverlay.Single(db.trackResultAnchors, r => r.trackId == trackId && r.rank == spawnOrderIndex + 1);
            if (row != null)
                transform.SetPositionAndRotation(row.position, Quaternion.Euler(0f, row.rotationY, 0f));
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.25f);
        }
    }
}
