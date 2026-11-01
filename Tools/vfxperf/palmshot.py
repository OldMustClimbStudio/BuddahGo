"""palmshot.py <label> [knobs]: in the running race, fire one palm from the local player and save Game-view screenshots
at +0.4, +0.9, +1.5 and +2.4 s after the fire (scratchpad/shots/<label>-t<sec>.png). knobs: C# run every frame on each
BuddahGo.VFX.InkFluidVolume `f` before the shot (e.g. a different cell size)."""
import sys, os, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import shots
HERE = os.path.dirname(os.path.abspath(__file__))
label = sys.argv[1] if len(sys.argv) > 1 else "shot"
knobs = sys.argv[2] if len(sys.argv) > 2 else ""
OUT = os.path.join(HERE, "shots").replace("\\", "/")
os.makedirs(OUT, exist_ok=True)
res = shots.res
shots.ensure_race()

SETUP = shots.LOCAL_RACER + r'''
SkillAction a; db.TryGet("push_projectile_hands", out a);
var palm = (Skill_PushProjectileHands)a;
float[] at = new float[] { 0.4f, 0.9f, 1.5f, 2.4f };
int next = 0; bool fired = false; float tFire = -1f;
float t0 = UnityEngine.Time.realtimeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction cb = null;
cb = () => {
  float t = UnityEngine.Time.realtimeSinceStartup - t0;
  if (!UnityEditor.EditorApplication.isPlaying || t > 8f) { UnityEditor.EditorApplication.update -= cb; return; }
  __KNOBS__
  if (!fired && t >= 1.0f) { palm.ExecuteServer(local, 0); palm.ExecuteObservers(local, 0, false, true); cdL.SetValue(hand, 0f); cdR.SetValue(hand, 0f); push.Invoke(hand, new object[] { false, hand.ServerHandYaw, hand.transform.position, true }); fired = true; tFire = t; }
  if (fired && next < at.Length && t - tFire >= at[next]) { UnityEngine.ScreenCapture.CaptureScreenshot("__OUT__/__LABEL__-t" + at[next].ToString("F1") + ".png", 1); next++; }
};
UnityEditor.EditorApplication.update += cb;
return "armed";'''.replace("__OUT__", OUT).replace("__LABEL__", label).replace("__KNOBS__", shots.fluid_knobs(knobs))
print(res(shots.code(SETUP)))
time.sleep(6)
print(sorted(f for f in os.listdir(OUT) if f.startswith(label)))
