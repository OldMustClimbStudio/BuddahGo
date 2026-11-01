"""recompile.py: AssetDatabase.Refresh() then wait until the editor is done compiling / reloading."""
import sys, os, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import shots, umcp
res = lambda t: shots.res(t, 300)
if shots.is_playing():
    umcp.call("manage_editor", {"action": "stop"}); shots.wait_not_playing(); time.sleep(2)
print(res(shots.code('UnityEditor.AssetDatabase.Refresh(); return "refreshed compiling=" + UnityEditor.EditorApplication.isCompiling;')))
quiet = 0
for i in range(90):
    time.sleep(2)
    try:
        r = str(res(shots.code('return "compiling=" + UnityEditor.EditorApplication.isCompiling + " updating=" + UnityEditor.EditorApplication.isUpdating;')))
    except Exception as e:
        r = "err " + str(e)[:80]
    if r == "compiling=False updating=False": quiet += 1
    else: quiet = 0; print(i, r[:120])
    if quiet >= 2: break
print(res(shots.code('return "done";')))
r, body = umcp.call("read_console", {"action": "get", "types": ["error"], "count": 20, "format": "plain"})
print(umcp.text_of(r)[:1500] if r else body[:500])
