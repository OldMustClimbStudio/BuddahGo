int removed = 0;
foreach (var g in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>())
    if (g != null && (g.name.StartsWith("FrostSwatch") ) && g.scene.IsValid() && g.transform.parent == null) { UnityEngine.Object.DestroyImmediate(g); removed++; }
var race = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
string raceState = race.path + " dirty=" + race.isDirty;
// RaceMap was never saved; reopen MainMenu (Single) which discards nothing on disk.
var menu = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
return "removed roots=" + removed + " before: " + raceState + " | now " + menu.path + " dirty=" + menu.isDirty;
