UnityEditor.AssetDatabase.Refresh();
var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
return "scene=" + s.path + " dirty=" + s.isDirty + " playing=" + UnityEditor.EditorApplication.isPlaying + " mat=" + (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_FrostSwatchLocal/Ice/Ice Shader.mat") != null) + " project=" + UnityEngine.Application.dataPath + " quality=" + UnityEngine.QualitySettings.names[UnityEngine.QualitySettings.GetQualityLevel()];
