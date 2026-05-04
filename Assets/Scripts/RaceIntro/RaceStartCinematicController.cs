using SteamMultiplayer.Network;
using UnityEngine;
using UnityEngine.Playables;

// Phase 6 Area 4 — Race-Start Timeline trigger.
// Subscribes (poll-based) to RoomStateManager._authoritativeGoIssued + _raceStartTick
// SyncVars; on countdown begin, stops Intro Timeline and plays Race-Start Timeline
// (letterbox + 3-2-1 UI per Section 3.4). Late-join Q6 (b.1) policy: if local
// TimeManager.LocalTick has already passed _raceStartTick when SyncVar fires,
// skip the Timeline entirely (no half-played cinematic UX).
//
// Timeline asset path placeholder per Q5: Assets/Cinematics/RaceStart/RaceStartTimeline.playable.
// Yonezawa wires the asset reference at Stage 4 close; engineer holds null-guarded
// reference until then. SMOKE Path A can run with raceStartTimeline=null and
// still validate engineering side via the gameplay unlock chain.
[DisallowMultipleComponent]
public class RaceStartCinematicController : MonoBehaviour
{
    [Header("Timelines")]
    [SerializeField] private PlayableDirector raceStartTimeline;
    [SerializeField] private PlayableDirector introTimeline;

    private bool _countdownTriggered;
    private uint _lastObservedRaceStartTick;

    private void Update()
    {
        RoomStateManager room = RoomStateManager.Instance;
        if (room == null)
            return;

        // Idempotent — fire countdown side effects exactly once per sequence.
        // Reset latch when SyncVar resets to 0 (new race / abort).
        uint currentRaceStartTick = room.RaceStartTick;
        if (currentRaceStartTick == 0u)
        {
            _countdownTriggered = false;
            _lastObservedRaceStartTick = 0u;
            return;
        }

        if (currentRaceStartTick == _lastObservedRaceStartTick)
            return;

        _lastObservedRaceStartTick = currentRaceStartTick;
        if (_countdownTriggered)
            return;

        if (!room.IsAuthoritativeGoIssued)
            return;

        _countdownTriggered = true;
        OnCountdownBegin(currentRaceStartTick, room);
    }

    private void OnCountdownBegin(uint raceStartTick, RoomStateManager room)
    {
        // Q6 (b.1) — Late-join: if local tick already passed raceStartTick when
        // SyncVar arrived, skip the Timeline entirely (don't play half-cinematic).
        var timeManager = FishNet.InstanceFinder.TimeManager;
        if (timeManager != null && timeManager.LocalTick >= raceStartTick)
        {
            Debug.Log($"[Phase6][Cinematic] Late-join skip Timeline localTick={timeManager.LocalTick} raceStartTick={raceStartTick}");
            return;
        }

        // Stop intro Timeline immediately (Section 3.4.1: clean cut, no overlap).
        if (introTimeline != null && introTimeline.state == PlayState.Playing)
        {
            introTimeline.Stop();
            Debug.Log("[Phase6][Cinematic] Intro Timeline stopped at countdown begin");
        }

        // Start Race-Start Timeline. Null-guard for pre-asset-wiring SMOKE runs.
        if (raceStartTimeline != null)
        {
            raceStartTimeline.time = 0;
            raceStartTimeline.Play();
            Debug.Log($"[Phase6][Cinematic] Race-Start Timeline play() raceStartTick={raceStartTick}");
        }
        else
        {
            Debug.LogWarning("[Phase6][Cinematic] raceStartTimeline reference null — Yonezawa needs to wire asset (Q5 placeholder Assets/Cinematics/RaceStart/RaceStartTimeline.playable)");
        }
    }
}
