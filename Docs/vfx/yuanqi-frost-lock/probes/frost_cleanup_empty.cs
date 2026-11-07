int left = 0;
foreach (var g in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>())
    if (g != null && g.name.StartsWith("FrostSwatch") && g.scene.IsValid()) { UnityEngine.Object.DestroyImmediate(g); left++; }
var race = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
string before = race.path + " dirty=" + race.isDirty;
if (race.isDirty) return "STOP: " + before + " (not discarding anything)";
var s = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
return "leftoverRemoved=" + left + " before: " + before + " | now '" + s.path + "' dirty=" + s.isDirty;
