using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuddahGo.Match
{
    /// <summary>
    /// This player's pick for the next match, made in the menu (skill page, skin page, sand table) and saved in
    /// PlayerPrefs. The room reads it when the player joins and whenever it changes; the server validates it.
    /// Ids are stored as picked; validation happens where they are used.
    /// </summary>
    public static class LocalMatchSelection
    {
        private const string SkillKeyPrefix = "match.selection.skill";
        private const string SkinKey = "match.selection.skin";
        private const string MapKey = "match.selection.map";

        private static string[] _skills;
        private static string _skinId;
        private static string _mapId;

        /// <summary>Raised after any of the three changes.</summary>
        public static event Action Changed;

        /// <summary>A copy of the skill id per slot (empty string = empty slot).</summary>
        public static string[] Skills
        {
            get
            {
                Load();
                return (string[])_skills.Clone();
            }
        }

        public static string SkinId { get { Load(); return _skinId; } }
        public static string MapId { get { Load(); return _mapId; } }

        /// <summary>True once the player has saved a loadout (any slot filled).</summary>
        public static bool HasSkills
        {
            get
            {
                Load();
                for (int i = 0; i < _skills.Length; i++)
                    if (!string.IsNullOrEmpty(_skills[i])) return true;
                return false;
            }
        }

        public static void SetSkills(IReadOnlyList<string> skillIds)
        {
            Load();
            bool changed = false;
            for (int i = 0; i < _skills.Length; i++)
            {
                string id = (skillIds != null && i < skillIds.Count ? skillIds[i] : null) ?? string.Empty;
                if (_skills[i] == id) continue;
                _skills[i] = id;
                PlayerPrefs.SetString(SkillKeyPrefix + i, id);
                changed = true;
            }
            if (changed) Commit();
        }

        public static void SetSkin(string skinId)
        {
            Load();
            skinId ??= string.Empty;
            if (_skinId == skinId) return;
            _skinId = skinId;
            PlayerPrefs.SetString(SkinKey, skinId);
            Commit();
        }

        public static void SetMap(string mapId)
        {
            Load();
            mapId ??= string.Empty;
            if (_mapId == mapId) return;
            _mapId = mapId;
            PlayerPrefs.SetString(MapKey, mapId);
            Commit();
        }

        private static void Commit()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static void Load()
        {
            if (_skills != null) return;
            _skills = new string[SkillLoadout.SlotCount];
            for (int i = 0; i < _skills.Length; i++)
                _skills[i] = PlayerPrefs.GetString(SkillKeyPrefix + i, string.Empty);
            _skinId = PlayerPrefs.GetString(SkinKey, string.Empty);
            _mapId = PlayerPrefs.GetString(MapKey, string.Empty);
        }

        /// <summary>Test hook: forget the cached values so the next read goes back to PlayerPrefs.</summary>
        internal static void ReloadForTests() => _skills = null;
    }
}
