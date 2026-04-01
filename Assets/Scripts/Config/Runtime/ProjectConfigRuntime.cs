using UnityEngine;

public static class ProjectConfigRuntime
{
    private const string DefaultResourcesPath = "Config/ProjectConfigDatabase";

    public static ProjectConfigDatabase Database { get; private set; }
    public static SkillConfigRepository SkillConfigs { get; private set; }
    public static SelectionRuleRepository SelectionRules { get; private set; }
    public static GlobalRuleRepository GlobalRules { get; private set; }

    public static bool EnsureInitialized(ProjectConfigDatabase preferredDatabase = null)
    {
        ProjectConfigDatabase databaseToUse = preferredDatabase != null ? preferredDatabase : Database;
        if (databaseToUse == null)
            databaseToUse = LoadDefaultDatabase();

        if (databaseToUse == null)
            return false;

        if (ReferenceEquals(Database, databaseToUse) && SkillConfigs != null && SelectionRules != null && GlobalRules != null)
            return true;

        Database = databaseToUse;
        SkillConfigs = new SkillConfigRepository(Database);
        SelectionRules = new SelectionRuleRepository(Database);
        GlobalRules = new GlobalRuleRepository(Database);
        return true;
    }

    public static bool TryGetSkillConfigRepository(out SkillConfigRepository repository)
    {
        repository = null;
        if (!EnsureInitialized())
            return false;

        repository = SkillConfigs;
        return repository != null;
    }

    public static bool TryGetSelectionRuleRepository(out SelectionRuleRepository repository)
    {
        repository = null;
        if (!EnsureInitialized())
            return false;

        repository = SelectionRules;
        return repository != null;
    }

    public static bool TryGetGlobalRuleRepository(out GlobalRuleRepository repository)
    {
        repository = null;
        if (!EnsureInitialized())
            return false;

        repository = GlobalRules;
        return repository != null;
    }

    private static ProjectConfigDatabase LoadDefaultDatabase()
    {
        ProjectConfigDatabase assetDatabase = Resources.Load<ProjectConfigDatabase>(DefaultResourcesPath);
        if (assetDatabase != null)
            return assetDatabase;

        TextAsset jsonText = Resources.Load<TextAsset>(DefaultResourcesPath);
        if (jsonText == null || string.IsNullOrWhiteSpace(jsonText.text))
            return null;

        ProjectConfigDatabase jsonDatabase = ScriptableObject.CreateInstance<ProjectConfigDatabase>();
        JsonUtility.FromJsonOverwrite(jsonText.text, jsonDatabase);
        jsonDatabase.name = "ProjectConfigDatabase_FromJson";
        return jsonDatabase;
    }
}
