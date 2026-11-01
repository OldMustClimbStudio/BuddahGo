"""palmperf24.py <label> [budget] [palms]: 24-palm frame recorder (built on the design session's palmperf.py).
0-2 s baseline; from 2 s one palm every 0.1 s until <palms> are out (2.4 s for 24); they all fly together until the
first expires (4 s after its spawn); from 7 s on, steady state: 6 palms / s for 6 s (the six-player worst case: every
client sees about 24 in the air). Stops at 15 s. Logs unscaled frame time, live ink palms, simulating fluids (new code
only), graphics-driver memory, fluid GPU bytes (new code only). budget: InkFluidVolume.simulationBudget forced on every
instance each frame (new code only; 0 = leave the prefab value)."""
import sys, os, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import shots
HERE = os.path.dirname(os.path.abspath(__file__))
label = sys.argv[1] if len(sys.argv) > 1 else "run"
budget = int(sys.argv[2]) if len(sys.argv) > 2 else 0
palms = int(sys.argv[3]) if len(sys.argv) > 3 else 24
knobs = sys.argv[4] if len(sys.argv) > 4 else ""   # C# run every frame on each BuddahGo.VFX.InkFluidVolume `f` (new code only)
res = shots.res

SETUP = shots.LOCAL_RACER + shots.FLUID_STATS + r'''
SkillAction a; db.TryGet("push_projectile_hands", out a);
var palm = (Skill_PushProjectileHands)a;
var budgetField = fluidType != null ? fluidType.GetField("simulationBudget") : null;
int budget = __BUDGET__;
int palmsWanted = __PALMS__;
System.Action<bool> fire = (left) => {
  cdL.SetValue(hand, 0f); cdR.SetValue(hand, 0f);
  push.Invoke(hand, new object[] { left, hand.ServerHandYaw, hand.transform.position, true });
};
System.Action buff = () => { palm.ExecuteServer(local, 0); palm.ExecuteObservers(local, 0, false, true); };
var sb = new System.Text.StringBuilder();
System.AppDomain.CurrentDomain.SetData("palmperf", sb);
float t0 = UnityEngine.Time.realtimeSinceStartup;
int lastFrame = -1; int shots = 0; int steady = 0; bool buffed2 = false; bool buffed3 = false;
UnityEditor.EditorApplication.CallbackFunction cb = null;
cb = () => {
  float t = UnityEngine.Time.realtimeSinceStartup - t0;
  if (!UnityEditor.EditorApplication.isPlaying || t > 15f) { UnityEditor.EditorApplication.update -= cb; sb.Append("END\n"); return; }
  if (UnityEngine.Time.frameCount == lastFrame) return;
  lastFrame = UnityEngine.Time.frameCount;
  if (shots < palmsWanted && t >= 2f + 0.1f * shots) { if (shots == 0) buff(); fire(shots % 2 == 0); shots++; }
  if (t >= 7f && steady < 36 && t >= 7f + steady / 6f) { if (!buffed2) { buff(); buffed2 = true; } if (t >= 11f && !buffed3) { buff(); buffed3 = true; } fire(steady % 2 == 0); steady++; }
  if (budget > 0 && budgetField != null) foreach (var f in UnityEngine.Object.FindObjectsByType(fluidType, UnityEngine.FindObjectsSortMode.None)) budgetField.SetValue(f, budget);
  __KNOBS__
  __SAMPLE__
};
UnityEditor.EditorApplication.update += cb;
return "recording; vsync=" + UnityEngine.QualitySettings.vSyncCount + " targetFps=" + UnityEngine.Application.targetFrameRate + " fluidType=" + (fluidType != null) + " budgetField=" + (budgetField != null);'''.replace("__BUDGET__", str(budget)).replace("__PALMS__", str(palms)).replace("__KNOBS__", shots.fluid_knobs(knobs)).replace("__SAMPLE__", shots.SAMPLE_FRAME)

READ = r'''var sb = System.AppDomain.CurrentDomain.GetData("palmperf"); return sb == null ? "none" : sb.ToString();'''

shots.ensure_race()
print(res(shots.code(SETUP)))
time.sleep(17)
out = str(res(shots.code(READ)))
open(os.path.join(HERE, "palmperf24-%s.out" % label), "w", encoding="utf-8").write(out)

rows = shots.frame_rows(out)
def band(lo, hi, lab):
    r = [x for x in rows if lo <= x[0] < hi]
    if not r: print(lab, "no frames"); return
    d = sorted(x[1] for x in r)
    print("%-28s frames=%4d  median=%5.1f  p95=%5.1f  max=%6.1f ms  ink=%2d..%2d sim=%2d..%2d  gfxMB=%d..%d  fluidMB=%d..%d" % (lab, len(r), d[len(d)//2], d[int(len(d)*0.95)], d[-1], min(x[2] for x in r), max(x[2] for x in r), min(x[3] for x in r), max(x[3] for x in r), min(x[4] for x in r), max(x[4] for x in r), min(x[5] for x in r), max(x[5] for x in r)))
print("== %s (budget %d, palms %d) ==" % (label, budget, palms))
band(0.3, 2.0, "baseline (0 palms)")
band(2.0, 2.0 + 0.1 * palms, "spawning %d palms" % palms)
band(2.0 + 0.1 * palms + 0.1, 6.0, "%d palms flying" % palms)
band(6.0, 7.0, "tail")
band(8.5, 13.0, "steady 6 / s (~24 alive)")
band(13.5, 15.0, "after (draining)")
