using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillUiSpriteLibrary : MonoBehaviour
{
    [Serializable]
    public struct SkillSpriteEntry
    {
        public string skillId;
        public string blackSpriteName;
        public Sprite blackSprite;
        public string whiteSpriteName;
        public Sprite whiteSprite;
    }

    [SerializeField] private List<SkillSpriteEntry> entries = new();

    public bool TryGetSprites(string skillId, out SkillSpriteEntry entry)
    {
        string normalizedId = (skillId ?? string.Empty).Trim();
        for (int i = 0; i < entries.Count; i++)
        {
            if (string.Equals((entries[i].skillId ?? string.Empty).Trim(), normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                entry = entries[i];
                return true;
            }
        }

        entry = default;
        return false;
    }
}
