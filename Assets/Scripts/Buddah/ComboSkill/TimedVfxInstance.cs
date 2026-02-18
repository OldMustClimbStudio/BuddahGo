using UnityEngine;

public class TimedVfxInstance : MonoBehaviour
{
    private float _timeLeft;
    private float _stopPlayingBeforeEndSeconds;
    private bool _particleStopped;

    public void Refresh(float durationSeconds)
    {
        Refresh(durationSeconds, 0f);
    }

    public void Refresh(float durationSeconds, float stopPlayingBeforeEndSeconds)
    {
        _timeLeft = Mathf.Max(_timeLeft, durationSeconds);
        _stopPlayingBeforeEndSeconds = Mathf.Max(0f, stopPlayingBeforeEndSeconds);
        _particleStopped = false;
    }

    private void Update()
    {
        _timeLeft -= Time.deltaTime;

        if (!_particleStopped && _timeLeft <= _stopPlayingBeforeEndSeconds)
        {
            StopAllParticleSystems();
            _particleStopped = true;
        }

        if (_timeLeft <= 0f)
            Destroy(gameObject);
    }

    private void StopAllParticleSystems()
    {
        var systems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            if (ps == null) continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
