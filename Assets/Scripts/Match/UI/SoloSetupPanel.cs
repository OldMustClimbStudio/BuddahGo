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
        private readonly Button[] _difficultyButtons = new Button[3];
        private MainMenuUI _menu;
        private TMP_Text _status;
        private Button _start;
        private Button _back;
        private SoloDifficulty _difficulty;
        private bool _requested;

        public void Initialize(MainMenuUI menu, TMP_FontAsset font)
        {
            _menu = menu;
            _difficulty = (SoloDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, 1), 0, 2);
            var card = SoloUIFactory.Card(transform, "SoloSetup", 520f);
            SoloUIFactory.Label(card, "Title", "单人游戏", new Vector2(0f, 205f), new Vector2(600f, 50f), font, 34f);
            var ai = SoloUIFactory.Button(card, "AICount", "AI 对手：0（Practice）",
                new Vector2(0f, 125f), new Vector2(520f, 56f), font);
            ai.interactable = false;
            SoloUIFactory.Label(card, "PracticeExplanation", "当前开放 Practice，AI 对手暂不可用。",
                new Vector2(0f, 70f), new Vector2(600f, 42f), font, 21f);
            SoloUIFactory.Label(card, "DifficultyLabel", "难度（保留选择，Practice 不受影响）",
                new Vector2(0f, 15f), new Vector2(600f, 40f), font, 22f);
            string[] labels = { "简单", "普通", "困难" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                _difficultyButtons[i] = SoloUIFactory.Button(card, "Difficulty" + labels[i], labels[i],
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
            SoloUIFactory.Navigate(_difficultyButtons[0], _difficultyButtons[1], _difficultyButtons[2], _start, _back);
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

        private void SelectDifficulty(SoloDifficulty difficulty)
        {
            _difficulty = difficulty;
            PlayerPrefs.SetInt(DifficultyKey, (int)difficulty);
            PlayerPrefs.Save();
            RefreshDifficulty();
        }

        private void RefreshDifficulty()
        {
            string[] labels = { "简单", "普通", "困难" };
            for (int i = 0; i < _difficultyButtons.Length; i++)
                _difficultyButtons[i].GetComponentInChildren<TMP_Text>().text =
                    ((int)_difficulty == i ? "● " : string.Empty) + labels[i];
        }

        private void StartPractice()
        {
            if (_requested) return;
            var session = SessionControl.Current;
            if (session == null) { _status.text = "初始化尚未完成，请稍后重试。"; return; }
            PlayerPrefs.SetInt(CountKey, 0);
            PlayerPrefs.SetInt(DifficultyKey, (int)_difficulty);
            PlayerPrefs.Save();
            if (!session.StartSoloHost(new SoloMatchSettings(0, _difficulty)))
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
            _back.interactable = !_requested;
            foreach (var button in _difficultyButtons) button.interactable = !_requested;
        }
    }
}
