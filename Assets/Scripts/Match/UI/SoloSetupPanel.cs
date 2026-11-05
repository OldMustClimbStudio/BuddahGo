using BuddahGo.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    public sealed class SoloSetupPanel : MonoBehaviour
    {
        private const string DifficultyKey = "SoloMatch.Difficulty";
        private static readonly string[] DifficultyLabels = { "简单", "普通", "困难" };
        private readonly Button[] _difficultyButtons = new Button[3];
        private MainMenuUI _menu;
        private TMP_Text _status;
        private Button _start;
        private Button _practice;
        private Button _back;
        private SoloDifficulty _difficulty;
        private bool _requested;

        public void Initialize(MainMenuUI menu, TMP_FontAsset font)
        {
            _menu = menu;
            // Match/ModeRule (Solo).defaultDifficulty is the first-run default; the player's last choice wins afterwards.
            ModeRuleRecord soloRule = DesignTableOverlay.ModeRule("Solo");
            int defaultDifficulty = soloRule != null && System.Enum.TryParse(soloRule.defaultDifficulty, true, out SoloDifficulty configured) ? (int)configured : 1;
            _difficulty = (SoloDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, defaultDifficulty), 0, 2);
            var card = SoloUIFactory.Card(transform, "SoloSetup", 520f);
            SoloUIFactory.Label(card, "Title", "单人游戏", new Vector2(0f, 205f), new Vector2(600f, 50f), font, 34f);
            int aiCount = ConfiguredAICount();
            SoloUIFactory.Label(card, "RacerCount", RacerCountNumerals[aiCount + 1] + "人单人赛 · 玩家 + " + aiCount + " AI",
                new Vector2(0f, 125f), new Vector2(600f, 56f), font, 26f);
            SoloUIFactory.Label(card, "Personalities", "五种技能人格 · 固定配装",
                new Vector2(0f, 70f), new Vector2(600f, 42f), font, 21f);
            SoloUIFactory.Label(card, "DifficultyLabel", "难度",
                new Vector2(0f, 15f), new Vector2(600f, 40f), font, 22f);
            for (int i = 0; i < DifficultyLabels.Length; i++)
            {
                int index = i;
                _difficultyButtons[i] = SoloUIFactory.Button(card, "Difficulty" + DifficultyLabels[i], DifficultyLabels[i],
                    new Vector2(-180f + 180f * i, -40f), new Vector2(160f, 48f), font);
                _difficultyButtons[i].onClick.AddListener(() => SelectDifficulty((SoloDifficulty)index));
            }
            _status = SoloUIFactory.Label(card, "Status", string.Empty, new Vector2(0f, -112f),
                new Vector2(600f, 72f), font, 21f);
            // "StartPractice" is the historical object name of the Solo race button (SoloSessionFlowTests finds it).
            _start = SoloUIFactory.Button(card, "StartPractice", "开始单人赛", new Vector2(-220f, -200f),
                new Vector2(210f, 60f), font);
            // Practice = the same Solo host with 0 AI; SoloMatchRules maps it to the Match/ModeRule Practice row
            // (training dummies, ends on the human's finish). The label stays Latin, like the results title
            // "Practice 完成": the BuddahGoSoloCJK subset has no glyphs for 训练模式 / 木桩 yet.
            _practice = SoloUIFactory.Button(card, "StartTraining", "Practice (0 AI)", new Vector2(0f, -200f),
                new Vector2(210f, 60f), font);
            _back = SoloUIFactory.Button(card, "Back", "返回", new Vector2(220f, -200f), new Vector2(210f, 60f), font);
            _start.onClick.AddListener(() => StartMatch(ConfiguredAICount()));
            _practice.onClick.AddListener(() => StartMatch(0));
            _back.onClick.AddListener(() => _menu.ShowHome());
            SoloUIFactory.Navigate(_difficultyButtons[0], _difficultyButtons[1], _difficultyButtons[2], _start, _practice, _back);
            RefreshDifficulty();
        }

        public void RestoreSettings(SoloMatchSettings settings)
        {
            _difficulty = settings.Difficulty;
            RefreshDifficulty();
        }

        private void OnEnable()
        {
            if (_start == null) return;
            _requested = false;
            _status.text = SessionControl.Current?.LastError ?? string.Empty;
            RefreshControls();
            SoloUIFactory.Focus(_start);
        }

        // Index = racers in the match (player + AI), for the "六人单人赛" style count label.
        private static readonly string[] RacerCountNumerals = { "零", "一", "二", "三", "四", "五", "六" };

        // Match/ModeRule (Solo) owns the AI count; RacerId.MaxAI is the fallback and the upper bound.
        // The count label and the started match both read it here so they cannot disagree.
        private static int ConfiguredAICount()
        {
            ModeRuleRecord solo = DesignTableOverlay.ModeRule("Solo");
            return solo != null ? Mathf.Clamp(solo.aiCount, 0, RacerId.MaxAI) : RacerId.MaxAI;
        }

        private void SelectDifficulty(SoloDifficulty difficulty)
        {
            _difficulty = difficulty;
            PlayerPrefs.SetInt(DifficultyKey, (int)difficulty);
            PlayerPrefs.Save();
            RefreshDifficulty();
        }

        private void RefreshDifficulty()
        {
            for (int i = 0; i < _difficultyButtons.Length; i++)
                _difficultyButtons[i].GetComponentInChildren<TMP_Text>().text =
                    ((int)_difficulty == i ? "● " : string.Empty) + DifficultyLabels[i];
        }

        private void StartMatch(int aiCount)
        {
            if (_requested) return;
            var session = SessionControl.Current;
            if (session == null) { _status.text = "初始化尚未完成，请稍后重试。"; return; }
            PlayerPrefs.SetInt(DifficultyKey, (int)_difficulty);
            PlayerPrefs.Save();
            if (!session.StartSoloHost(new SoloMatchSettings(aiCount, _difficulty)))
            {
                _status.text = string.IsNullOrEmpty(session.LastError) ? "启动失败，请重试。" : session.LastError;
                return;
            }
            _requested = true;
            _status.text = "正在进入选择…";
            RefreshControls();
        }

        private void Update()
        {
            if (_start == null) return;
            var session = SessionControl.Current;
            if (_requested && session != null && !session.IsStarting && !string.IsNullOrEmpty(session.LastError))
            {
                _requested = false;
                _status.text = session.LastError;
            }
            RefreshControls();
        }

        private void RefreshControls()
        {
            _start.interactable = !_requested && SessionControl.Current != null;
            _practice.interactable = _start.interactable;
            _back.interactable = !_requested;
            foreach (var button in _difficultyButtons) button.interactable = !_requested;
        }
    }
}
