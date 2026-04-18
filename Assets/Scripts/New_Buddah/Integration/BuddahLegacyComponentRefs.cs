using System;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    [Serializable]
    public class BuddahLegacyComponentRefs
    {
        [SerializeField] private BuddahMovement movement;
        [SerializeField] private Rigidbody rigidbody;
        [SerializeField] private PlayerCamera playerCamera;
        [SerializeField] private BuddahHandControl handControl;
        [SerializeField] private BuddahRespawn respawn;
        [SerializeField] private SplineProgressTracker splineProgressTracker;

        public BuddahMovement Movement => movement;
        public Rigidbody Rigidbody => rigidbody;
        public PlayerCamera PlayerCamera => playerCamera;
        public BuddahHandControl HandControl => handControl;
        public BuddahRespawn Respawn => respawn;
        public SplineProgressTracker SplineProgressTracker => splineProgressTracker;

        public void Resolve(GameObject owner)
        {
            if (owner == null)
                return;

            if (movement == null)
                movement = owner.GetComponent<BuddahMovement>();
            if (rigidbody == null)
                rigidbody = owner.GetComponent<Rigidbody>();
            if (playerCamera == null)
                playerCamera = owner.GetComponent<PlayerCamera>();
            if (handControl == null)
                handControl = owner.GetComponent<BuddahHandControl>();
            if (respawn == null)
                respawn = owner.GetComponent<BuddahRespawn>();
            if (splineProgressTracker == null)
                splineProgressTracker = owner.GetComponent<SplineProgressTracker>();
        }
    }
}
