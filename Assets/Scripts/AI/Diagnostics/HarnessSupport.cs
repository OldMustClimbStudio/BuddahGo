#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using BuddahGo.Match;
using SteamMultiplayer.Network;
using UnityEngine;

namespace BuddahGo.AI
{
    // Shared by the diagnostic harnesses only. Evidence formats (rows, events, summaries) stay with each harness.
    internal static class HarnessArgs
    {
        public static bool Has(string[] args, string flag) => Array.IndexOf(args, flag) >= 0;
        public static string Value(string[] args, string flag)
        {
            int index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }

    // Deterministic measurement cap (60 fps, no vSync, keep running unfocused); Restore returns the previous values.
    internal struct MeasurementSettings
    {
        private bool _runInBackground;
        private int _targetFrameRate, _vSyncCount;
        public static MeasurementSettings Apply()
        {
            var previous = new MeasurementSettings { _runInBackground = Application.runInBackground,
                _targetFrameRate = Application.targetFrameRate, _vSyncCount = QualitySettings.vSyncCount };
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            return previous;
        }
        public void Restore()
        {
            Application.runInBackground = _runInBackground; Application.targetFrameRate = _targetFrameRate; QualitySettings.vSyncCount = _vSyncCount;
        }
    }

    // Drives an idle menu into a Solo race the way a player would: the loadout goes into the saved menu pick
    // (LocalMatchSelection), the solo host starts and the room carries the pick into the race. Once the room has
    // moved on, the player's own saved pick is put back. Stage 2 means the race is loading.
    internal sealed class SoloHarnessFlow
    {
        public int Stage { get; private set; }
        public string[] Submitted { get; private set; }
        public string Note { get; private set; }
        private string[] _playerSkills;

        /// <param name="loadout">Skill ids to race with; null or not selectable uses the first three selectable skills.</param>
        public bool Advance(SoloMatchSettings settings, string[] loadout, Action<string[]> loadoutSubmitted = null)
        {
            if (Stage == 0 && SessionControl.Current != null)
            {
                string[] offered = SelectableSkills();
                string[] ids = loadout;
                if (ids == null || ids.Length < SkillLoadout.SlotCount || ids.Any(id => !offered.Contains(id)) || ids.Distinct().Count() != ids.Length)
                {
                    if (loadout != null)
                        Note = "requested loadout [" + string.Join(",", loadout) + "] not selectable (selectable: "
                               + string.Join(",", offered) + "); used the first three selectable skills";
                    ids = offered.Take(SkillLoadout.SlotCount).ToArray();
                }
                _playerSkills = LocalMatchSelection.Skills;
                LocalMatchSelection.SetSkills(ids);
                if (!SessionControl.Current.StartSoloHost(settings))
                {
                    LocalMatchSelection.SetSkills(_playerSkills);
                    throw new InvalidOperationException(SessionControl.Current.LastError);
                }
                Submitted = ids;
                loadoutSubmitted?.Invoke(ids);
                Stage = 1;
            }
            RoomStateManager room = RoomStateManager.Instance;
            if (Stage == 1 && room != null && room.IsClientInitialized && room.CurrentMatchSessionPhase != MatchSessionPhase.InRoom)
            {
                LocalMatchSelection.SetSkills(_playerSkills);
                Stage = 2;
            }
            return Stage >= 2;
        }

        private static string[] SelectableSkills()
        {
            SkillDatabase database = Resources.FindObjectsOfTypeAll<SkillDatabase>().FirstOrDefault();
            return database == null ? Array.Empty<string>()
                : database.BuildSelectableSkillOptions().Where(o => o.IsUnlocked).Select(o => o.OptionId).ToArray();
        }
    }
}
#endif
