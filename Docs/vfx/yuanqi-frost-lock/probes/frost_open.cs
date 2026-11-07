var cur = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
if (cur.isDirty) return "FAILED: " + cur.path + " is dirty";
var s = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/RaceMap.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
UnityEngine.DynamicGI.UpdateEnvironment();
return "opened " + s.path + " dirty=" + s.isDirty + " main=" + (UnityEngine.Camera.main != null);
