using BuddahGo.Match;
using SteamMultiplayer.Network.Results;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    public sealed class QuitConfirmDialog : MonoBehaviour, ILocalInputBlock
    {
        private GameObject _view;
        private GameObject _previousSelection;
        private ILocalInputBlock _previousBlock;
        private Button _cancel;
        private Button _confirm;
        private TMP_Text _status;
        private bool _open;
        private bool _stopping;
        private bool _cursorWasVisible;
        private CursorLockMode _cursorLock;
        public bool IsBlocked => _open || (_previousBlock != null && _previousBlock.IsBlocked);

        public void Initialize(TMP_FontAsset font)
        {
            _view = SoloUIFactory.Canvas(transform, "QuitConfirmCanvas", 32000).gameObject;
            var card = SoloUIFactory.Card(_view.transform, "QuitConfirmation", 320f);
            SoloUIFactory.Label(card, "Title", "退出到主菜单？", new Vector2(0f, 95f),
                new Vector2(600f, 55f), font, 32f);
            _status = SoloUIFactory.Label(card, "Status", "本局进度不会保留。比赛仍在进行。",
                new Vector2(0f, 25f), new Vector2(610f, 55f), font, 22f);
            _cancel = SoloUIFactory.Button(card, "CancelQuit", "继续比赛", new Vector2(-145f, -90f),
                new Vector2(260f, 60f), font);
            _confirm = SoloUIFactory.Button(card, "ConfirmQuit", "退出到主菜单", new Vector2(145f, -90f),
                new Vector2(260f, 60f), font);
            _cancel.onClick.AddListener(Close);
            _confirm.onClick.AddListener(Confirm);
            SoloUIFactory.Navigate(_cancel, _confirm);
            _view.SetActive(false);
        }

        private void Update()
        {
            if (_view == null) return;
            var stage = MatchResultPresentationCoordinator.Instance;
            bool eligible = MatchRules.Current.AllowQuitDialog &&
                (stage == null || stage.CurrentStage == MatchResultPresentationStage.Racing);
            if (!eligible) { if (_open && !_stopping) Close(); return; }
            if (_stopping) return;
            bool cancel = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (cancel) { if (_open) Close(); else Open(); }
        }

        private void Open()
        {
            _open = true;
            _previousBlock = LocalInputBlock.Current;
            LocalInputBlock.Current = this;
            _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _cursorWasVisible = Cursor.visible;
            _cursorLock = Cursor.lockState;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _view.SetActive(true);
            SoloUIFactory.Focus(_cancel);
        }

        private void Close()
        {
            if (!_open || _stopping) return;
            ReleaseBlock();
            _view.SetActive(false);
            Cursor.lockState = _cursorLock;
            Cursor.visible = _cursorWasVisible;
            if (_previousSelection != null && _previousSelection.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_previousSelection);
        }

        private void Confirm()
        {
            if (_stopping) return;
            if (SessionControl.Current == null)
            {
                _status.text = "暂时无法退出，请取消后重试。";
                return;
            }
            _stopping = true;
            _status.text = "正在返回主菜单…";
            _cancel.interactable = _confirm.interactable = false;
            SessionControl.Current.RequestStopSession();
        }

        private void ReleaseBlock()
        {
            if (ReferenceEquals(LocalInputBlock.Current, this)) LocalInputBlock.Current = _previousBlock;
            _previousBlock = null;
            _open = false;
        }

        private void OnDisable()
        {
            bool wasOpen = _open;
            ReleaseBlock();
            if (_view != null) _view.SetActive(false);
            if (wasOpen)
            {
                Cursor.lockState = _stopping ? CursorLockMode.None : _cursorLock;
                Cursor.visible = _stopping || _cursorWasVisible;
            }
        }
    }
}
