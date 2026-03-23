using UnityEngine;

public class PlayerSpeedParticleEffect : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private ParticleSystem[] particleSystems;

    [Header("Speed Trigger")]
    [SerializeField] private float playSpeedThreshold = 20f;
    [SerializeField] private float stopSpeedThreshold = 18f;
    [SerializeField] private bool usePlanarSpeed = true;

    private bool _isPlaying;

    private void Awake()
    {
        if (targetRigidbody == null)
            targetRigidbody = GetComponentInParent<Rigidbody>();

        if (particleSystems == null || particleSystems.Length == 0)
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);

        StopParticlesImmediate();
    }

    private void Update()
    {
        if (targetRigidbody == null || particleSystems == null || particleSystems.Length == 0)
            return;

        Vector3 velocity = targetRigidbody.velocity;
        if (usePlanarSpeed)
            velocity.y = 0f;

        float speed = velocity.magnitude;

        if (!_isPlaying && speed >= playSpeedThreshold)
        {
            PlayParticles();
            return;
        }

        if (_isPlaying && speed <= stopSpeedThreshold)
            StopParticles();
    }

    private void PlayParticles()
    {
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
                continue;

            if (!ps.gameObject.activeSelf)
                ps.gameObject.SetActive(true);

            ps.Play(true);
        }

        _isPlaying = true;
    }

    private void StopParticles()
    {
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
                continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        _isPlaying = false;
    }

    private void StopParticlesImmediate()
    {
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
                continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        _isPlaying = false;
    }
}
