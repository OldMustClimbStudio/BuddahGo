using UnityEngine;

[CreateAssetMenu(fileName = "GlobalSkillSettings", menuName = "Settings/GlobalSkillSettings")]
public class GlobalSkillSettings : ScriptableObject
{
    [Header("Global Obsession Settings")]
    [Min(0f)] public float obsessionGain = 0f;
}