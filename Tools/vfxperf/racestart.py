import sys, os, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import shots, umcp
code = shots.code
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "ingame").replace("\\", "/")
os.makedirs(OUT, exist_ok=True)
res = lambda t: shots.res(t, 400)
if shots.is_playing():
    umcp.call("manage_editor", {"action": "stop"}); shots.wait_not_playing()
print(res(code(r'''
var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (s.isDirty) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s);
UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
return "opened " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;''')))
umcp.call("manage_editor", {"action": "play"})
for _ in range(60):
    time.sleep(2)
    r = res(code('return UnityEditor.EditorApplication.isPlaying + " " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;'))
    if str(r).startswith("True MainMenu"): break
time.sleep(4)
ev = (OUT + "/harness-" + time.strftime("%H%M%S")).replace("/", "\\\\")
print(res(code('BuddahGo.AI.AITuningHarness.Begin("%s", null, 1, 2); return "harness started";' % ev)))
for i in range(90):
    time.sleep(3)
    r = res(code('var h = UnityEngine.Object.FindFirstObjectByType<BuddahGo.AI.AITuningHarness>(); var room = SteamMultiplayer.Network.RoomStateManager.Instance; return (h != null ? h.Status : "no harness") + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " go=" + (room != null && room.IsAuthoritativeGoIssued);'))
    if i % 5 == 0: print(r)
    if "go=True" in str(r): print(r); break
