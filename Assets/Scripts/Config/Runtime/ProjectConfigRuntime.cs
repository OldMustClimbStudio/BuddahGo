using UnityEngine;

public static class ProjectConfigRuntime
{
    private const string DefaultResourcesPath = "Config/ProjectConfigDatabase";

    public static ProjectConfigDatabase Database { get; private set; }
    public static SkillConfigRepository SkillConfigs { get; private set; }
    public static SelectionRuleRepository SelectionRules { get; private set; }
    public static GlobalRuleRepository GlobalRules { get; private set; }
    public static LocalizedTextRepository Texts { get; private set; }

    public static bool EnsureInitialized(ProjectConfigDatabase preferredDatabase = null)
    {
        ProjectConfigDatabase databaseToUse = preferredDatabase != null ? preferredDatabase : Database;
        if (databaseToUse == null)
            databaseToUse = LoadDefaultDatabase();

        if (databaseToUse == null)
            return false;

        if (ReferenceEquals(Database, databaseToUse) && SkillConfigs != null && SelectionRules != null && GlobalRules != null && Texts != null)
            return true;

        Database = databaseToUse;
        SkillConfigs = new SkillConfigRepository(Database);
        SelectionRules = new SelectionRuleRepository(Database);
        GlobalRules = new GlobalRuleRepository(Database);
        Texts = new LocalizedTextRepository(Database);
        return true;
    }

    /// <summary>Drop the cached repositories so the next lookup rebuilds them (the importer calls this after rewriting the asset's lists).</summary>
    public static void Invalidate()
    {
        SkillConfigs = null;
        SelectionRules = null;
        GlobalRules = null;
        Texts = null;
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

    public static bool TryGetTextRepository(out LocalizedTextRepository repository)
    {
        repository = null;
        if (!EnsureInitialized())
            return false;

        repository = Texts;
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
