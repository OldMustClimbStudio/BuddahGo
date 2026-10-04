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

    // Drives an idle MainMenu into a Solo race the way a player would: start the host, submit the first three
    // loadout options, then the first skin. Stage 3 means every selection has been submitted.
    internal sealed class SoloHarnessFlow
    {
        public int Stage { get; private set; }
        public bool Advance(int aiCount, SoloDifficulty difficulty, Action<string[]> loadoutSubmitted)
        {
            if (Stage == 0 && SessionControl.Current != null)
            {
                if (!SessionControl.Current.StartSoloHost(new SoloMatchSettings(aiCount, difficulty)))
                    throw new InvalidOperationException(SessionControl.Current.LastError);
                Stage = 1;
            }
            var selection = PropertiesSelectionManager.Instance;
            if (Stage == 1 && selection != null && selection.IsClientInitialized && selection.IsStageCountdownActive
                && selection.CurrentStagePropertyKey == PropertiesSelectionManager.SkillLoadoutStageKey)
            {
                var ids = selection.GetOptionsForProperty(PropertiesSelectionManager.SkillLoadoutStageKey).Take(3).Select(o => o.OptionId).ToArray();
                selection.SubmitSkillLoadoutSelection(ids); Stage = 2; loadoutSubmitted?.Invoke(ids);
            }
            if (Stage == 2 && selection != null && selection.CurrentStagePropertyKey == PropertiesSelectionManager.SkinStageKey)
            {
                var options = selection.GetOptionsForProperty(PropertiesSelectionManager.SkinStageKey);
                if (options.Count > 0) { selection.SubmitPlayerSelection(PropertiesSelectionManager.SkinStageKey, options[0].OptionId); Stage = 3; }
            }
            return Stage >= 3;
        }
    }
}
#endif
