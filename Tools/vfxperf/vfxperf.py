"""vfxperf.py <skillId> [count] [interval] [label] [knobs]: frame-time / VRAM recorder for any skill's VFX, in the
editor's single-player race (AITuningHarness, 1 player + 2 AI). Baseline 0-2 s; from 2 s the local player casts
<skillId> <count> times, <interval> s apart (the skill's server + observer entry points are called directly, so cooldowns
and resources do not matter); recording ends 9 s after the last cast. For push_projectile_hands it also fires the hand
(cooldown reset by reflection) after each cast, like palmperf24.py. Writes vfxperf-<label>.out (t, unscaled frame ms,
live ink palms, simulating fluids, graphics-driver MB, fluid MB) and prints median / p95 / max per band.
Docs: Docs/vfx/README.md (性能测量)."""
import sys, os, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import shots
HERE = os.path.dirname(os.path.abspath(__file__))
skill = sys.argv[1]
count = int(sys.argv[2]) if len(sys.argv) > 2 else 6
interval = float(sys.argv[3]) if len(sys.argv) > 3 else 0.2
label = sys.argv[4] if len(sys.argv) > 4 else skill
knobs = sys.argv[5] if len(sys.argv) > 5 else ""
stop_at = 2.0 + count * interval + 9.0
res = shots.res
shots.ensure_race()

SETUP = shots.LOCAL_RACER + shots.FLUID_STATS + r'''
SkillAction a; if (!db.TryGet("__SKILL__", out a)) return "no skill __SKILL__";
bool isPalm = "__SKILL__" == "push_projectile_hands";
int count = __COUNT__; float interval = __INTERVAL__f; float stopAt = __STOP__f;
var sb = new System.Text.StringBuilder();
System.AppDomain.CurrentDomain.SetData("vfxperf", sb);
float t0 = UnityEngine.Time.realtimeSinceStartup;
int lastFrame = -1; int casts = 0;
UnityEditor.EditorApplication.CallbackFunction cb = null;
cb = () => {
  float t = UnityEngine.Time.realtimeSinceStartup - t0;
  if (!UnityEditor.EditorApplication.isPlaying || t > stopAt) { UnityEditor.EditorApplication.update -= cb; sb.Append("END\n"); return; }
  if (UnityEngine.Time.frameCount == lastFrame) return;
  lastFrame = UnityEngine.Time.frameCount;
  if (casts < count && t >= 2f + interval * casts) {
    a.ExecuteServer(local, 0); a.ExecuteObservers(local, 0, false, true);
    if (isPalm) { cdL.SetValue(hand, 0f); cdR.SetValue(hand, 0f); push.Invoke(hand, new object[] { casts % 2 == 0, hand.ServerHandYaw, hand.transform.position, true }); }
    casts++;
  }
  __KNOBS__
  __SAMPLE__
};
UnityEditor.EditorApplication.update += cb;
return "recording " + "__SKILL__" + " x" + count + "; vsync=" + UnityEngine.QualitySettings.vSyncCount;'''
SETUP = SETUP.replace("__SKILL__", skill).replace("__COUNT__", str(count)).replace("__INTERVAL__", str(interval)).replace("__STOP__", str(stop_at))
SETUP = SETUP.replace("__KNOBS__", shots.fluid_knobs(knobs)).replace("__SAMPLE__", shots.SAMPLE_FRAME)
READ = r'''var sb = System.AppDomain.CurrentDomain.GetData("vfxperf"); return sb == null ? "none" : sb.ToString();'''
print(res(shots.code(SETUP)))
time.sleep(stop_at + 2)
out = str(res(shots.code(READ)))
open(os.path.join(HERE, "vfxperf-%s.out" % label), "w", encoding="utf-8").write(out)
rows = shots.frame_rows(out)
def band(lo, hi, lab):
    r = [x for x in rows if lo <= x[0] < hi]
    if not r: print(lab, "no frames"); return
    d = sorted(x[1] for x in r)
    print("%-26s frames=%4d  median=%5.1f  p95=%5.1f  max=%6.1f ms  gfxMB=%d..%d" % (lab, len(r), d[len(d)//2], d[int(len(d)*0.95)], d[-1], min(x[4] for x in r), max(x[4] for x in r)))
tc = 2.0 + count * interval
print("== %s: %s x%d every %.2f s ==" % (label, skill, count, interval))
band(0.3, 2.0, "baseline")
band(2.0, tc + 0.2, "casting")
band(tc + 0.2, tc + 3.2, "all alive (3 s)")
band(tc + 3.2, tc + 6.2, "next 3 s")
band(tc + 6.2, stop_at, "after")
