using UnityEngine;

namespace BuddahGo.Match
{
    // Visible screen effects have a server counterpart for racers without a local screen.
    // Kept in Contracts so shared skills do not reference an AI implementation.
    [DisallowMultipleComponent]
    public sealed class SkillPerceptionState : MonoBehaviour
    {
        public uint CurtainUntilTick { get; private set; }
        public uint VisionImpairedUntilTick { get; private set; }
        public void SetCurtain(uint untilTick, bool impaired)
        {
            CurtainUntilTick = untilTick;
            VisionImpairedUntilTick = impaired ? untilTick : 0;
        }
        public void Clear() { CurtainUntilTick = 0; VisionImpairedUntilTick = 0; }

        public static void ApplyCurtain(SkillExecutor caster, float duration, bool backlash)
        {
            if (caster == null || !caster.IsServerInitialized || caster.TimeManager == null) return;
            var racers = RacerDirectory.Current;
            if (racers == null) return;
            uint until = caster.TimeManager.LocalTick + (uint)Mathf.CeilToInt(duration / (float)caster.TimeManager.TickDelta);
            foreach (var racer in racers.All)
            {
                if (racer == null || !racer.TryGetComponent(out SkillPerceptionState perception)) continue;
                bool self = racer.gameObject == caster.gameObject;
                perception.SetCurtain(until, self == backlash);
            }
        }
    }
}
