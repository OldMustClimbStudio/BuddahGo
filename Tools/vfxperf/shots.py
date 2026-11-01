"""shots.py: helpers shared by the vfxperf scripts: code() / res() / is_playing() / wait_not_playing() / ensure_race(),
the C# snippets that find the local racer and record frames (from the design session; the shoot() preview-rig capture
below needs an EditorTools class named in CLS and is kept as-is)."""
import sys, time, os, json, runpy
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import umcp

def code(c, safety=True):
    r, body = umcp.call("execute_code", {"action": "execute", "code": c, "compiler": "auto", "safety_checks": safety})
    return umcp.text_of(r) if r else body

def is_playing():
    t = code("return UnityEditor.EditorApplication.isPlaying ? \"1\" : \"0\";")
    return '"result":"1"' in t

def wait_not_playing(limit=40):
    for _ in range(limit):
        if not is_playing(): return True
        time.sleep(1)
    return False

# The execute_code result string, or the tail of the raw reply when it is not one.
def res(t, tail=600):
    try: return json.loads(t)["data"]["result"]
    except Exception: return t[-tail:]

def in_race():
    return is_playing() and '"result":"RaceMap"' in code('return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;')

# Starts the single-player race (racestart.py) unless one is already running.
def ensure_race():
    if not in_race():
        runpy.run_path(os.path.join(HERE, "racestart.py"))

# C#: the local racer `local`, the skill database `db`, its hand `hand`, the private push entry point `push` and the
# two push cooldown fields `cdL` / `cdR` (reset by reflection so every fire goes through).
LOCAL_RACER = r'''
SkillExecutor local = null;
foreach (var e in UnityEngine.Object.FindObjectsByType<SkillExecutor>(UnityEngine.FindObjectsSortMode.None)) if (e.IsOwner) local = e;
if (local == null) return "no local";
var db = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Skills_Assets/Skill Database.asset");
var hand = local.GetComponent<BuddahHandControl>();
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var push = typeof(BuddahHandControl).GetMethod("TryPushServer", bf);
var cdL = typeof(BuddahHandControl).GetField("_nextPushServerTimeLeft", bf);
var cdR = typeof(BuddahHandControl).GetField("_nextPushServerTimeRight", bf);
'''

# C#: the fluid's static counters, read by reflection (`fluidType`, `simProp`, `bytesProp`).
FLUID_STATS = r'''
var fluidType = typeof(BuddahGo.VFX.PalmInkTimeline).Assembly.GetType("BuddahGo.VFX.InkFluidVolume");
var simProp = fluidType != null ? fluidType.GetProperty("SimulatingCount") : null;
var bytesProp = fluidType != null ? fluidType.GetProperty("GpuBytes") : null;
'''

# C#, once per frame: appends "t frameMs inkPalms simulatingFluids gfxMB fluidMB" to `sb`.
SAMPLE_FRAME = r'''
  int ink = UnityEngine.Object.FindObjectsByType<BuddahGo.VFX.PalmInkTimeline>(UnityEngine.FindObjectsSortMode.None).Length;
  int sim = simProp != null ? (int)simProp.GetValue(null) : -1;
  long fb = bytesProp != null ? (long)bytesProp.GetValue(null) : -1;
  long gfx = UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver();
  sb.Append(t.ToString("F3") + " " + (UnityEngine.Time.unscaledDeltaTime * 1000f).ToString("F1") + " " + ink + " " + sim + " " + (gfx / 1048576) + " " + (fb / 1048576) + "\n");'''

# C#, once per frame: runs `knobs` on every InkFluidVolume `f` ("" = nothing).
def fluid_knobs(knobs):
    return ("foreach (var f in UnityEngine.Object.FindObjectsByType<BuddahGo.VFX.InkFluidVolume>(UnityEngine.FindObjectsSortMode.None)) { " + knobs + " }") if knobs else ""

# Rows of SAMPLE_FRAME output: (t, ms, ink, sim, gfxMB, fluidMB).
def frame_rows(out):
    rows = []
    for l in out.splitlines():
        p = l.split()
        if len(p) == 6: rows.append((float(p[0]), float(p[1]), int(p[2]), int(p[3]), int(p[4]), int(p[5])))
    return rows

def shoot(outdir, t, cams):
    os.makedirs(outdir, exist_ok=True)
    if is_playing():
        umcp.call("manage_editor", {"action": "stop"}); wait_not_playing()
    print(code("return BuddahGo.VFX.EditorTools.%s.Setup(%sf);" % (os.environ.get("CLS","PalmFrontalProto"), t))[:160])
    r, _ = umcp.call("manage_editor", {"action": "play"}); print(umcp.text_of(r)[:80])
    frozen = False
    for _ in range(60):
        time.sleep(1)
        if '"result":"frozen"' in code("return UnityEngine.Time.timeScale == 0f ? \"frozen\" : \"running\";"):
            frozen = True; break
    print("frozen" if frozen else "NOT FROZEN after 60 s")
    for cam in cams:
        p = os.path.join(outdir, "t%s_%s.png" % (t, cam)).replace("\\", "/")
        print(code('return BuddahGo.VFX.EditorTools.%s.Capture("Cam_%s", "%s");' % (os.environ.get("CLS","PalmFrontalProto"), cam, p))[:160])
    umcp.call("manage_editor", {"action": "stop"})
    wait_not_playing()
    time.sleep(2)

if __name__ == "__main__":
    out = sys.argv[1]
    specs = sys.argv[2:]
    # specs like 0.6:Caster,Side 1.05:Caster,Side
    for spec in specs:
        t, cams = spec.split(":")
        shoot(out, t, cams.split(","))
    print(os.listdir(out))
