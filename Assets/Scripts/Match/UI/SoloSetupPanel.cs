using BuddahGo.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    public sealed class SoloSetupPanel : MonoBehaviour
    {
        private const string DifficultyKey = "SoloMatch.Difficulty";
        private const string CountKey = "SoloMatch.AICount";
        private static readonly string[] DifficultyLabels = { "简单", "普通", "困难" };
        private readonly Button[] _difficultyButtons = new Button[3];
        private MainMenuUI _menu;
        private TMP_Text _status;
        private Button _start;
        private Button _back;
        private SoloDifficulty _difficulty;
        private bool _requested;
        private int _aiCount;
        private Button _mode;

        public void Initialize(MainMenuUI menu, TMP_FontAsset font)
        {
            _menu = menu;
            _difficulty = (SoloDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, 1), 0, 2);
            _aiCount = PlayerPrefs.GetInt(CountKey, 5) == 0 ? 0 : 5;
            var card = SoloUIFactory.Card(transform, "SoloSetup", 520f);
            SoloUIFactory.Label(card, "Title", "单人游戏", new Vector2(0f, 205f), new Vector2(600f, 50f), font, 34f);
            _mode = SoloUIFactory.Button(card, "AICount", string.Empty,
                new Vector2(0f, 125f), new Vector2(520f, 56f), font);
            _mode.onClick.AddListener(() => { _aiCount = _aiCount == 0 ? 5 : 0; RefreshMode(); });
            SoloUIFactory.Label(card, "PracticeExplanation", "点击切换：单人赛 / Practice",
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
            _start = SoloUIFactory.Button(card, "StartPractice", "开始 Practice", new Vector2(-145f, -200f),
                new Vector2(260f, 60f), font);
            _back = SoloUIFactory.Button(card, "Back", "返回", new Vector2(145f, -200f), new Vector2(260f, 60f), font);
            _start.onClick.AddListener(StartPractice);
            _back.onClick.AddListener(() => _menu.ShowHome());
            SoloUIFactory.Navigate(_mode, _difficultyButtons[0], _difficultyButtons[1], _difficultyButtons[2], _start, _back);
            RefreshDifficulty();
            RefreshMode();
        }

        private void RefreshMode()
        {
            _mode.GetComponentInChildren<TMP_Text>().text = _aiCount == 0 ? "Practice · 0 AI" : "单人赛 · 5 AI";
            _start.GetComponentInChildren<TMP_Text>().text = _aiCount == 0 ? "开始 Practice" : "开始单人赛";
        }

        public void RestoreSettings(SoloMatchSettings settings)
        {
            _difficulty = settings.Difficulty;
            _aiCount = settings.AICount;
            RefreshMode();
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

        private void StartPractice()
        {
            if (_requested) return;
            var session = SessionControl.Current;
            if (session == null) { _status.text = "初始化尚未完成，请稍后重试。"; return; }
            PlayerPrefs.SetInt(CountKey, _aiCount);
            PlayerPrefs.SetInt(DifficultyKey, (int)_difficulty);
            PlayerPrefs.Save();
            if (!session.StartSoloHost(new SoloMatchSettings(_aiCount, _difficulty)))
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
            _mode.interactable = !_requested;
            _back.interactable = !_requested;
            foreach (var button in _difficultyButtons) button.interactable = !_requested;
        }
    }
}
