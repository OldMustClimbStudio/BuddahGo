"""shots.py: code() / is_playing() / wait_not_playing() helpers for the vfxperf scripts (from the design session; the
shoot() preview-rig capture below needs an EditorTools class named in CLS and is kept as-is)."""
import sys, time, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
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
