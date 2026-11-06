using System.Collections;
using System.Collections.Generic;
using BuddahGo.Match;
using SteamMultiplayer.Network;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// The online pages of the menu environment ("一笔" design, v0.2 mockups 06 and 07).
    /// Door page, at the closed hall door (道场), over deep edge gradients: "联机道场" at the top left, and a vertical menu
    /// on the door's lower right panel — 创建房间 (creates a public Steam lobby, then the doors open and the view cuts to
    /// the hall; without Steam the hall opens offline and races solo) and 加入房间, which swaps the menu for the join panel
    /// in the same place: a four-character room number typed straight from the keyboard (letters and digits; Backspace
    /// deletes) and the public rooms Steam lists (↑ / ↓ or the pointer chooses one). Enter joins by the number when it is
    /// complete, else the chosen room (MenuRoomSession). Esc in the join panel goes back to the menu; in the menu it glides back to the main
    /// menu. The selected menu item lifts with a vermilion dot, an ink stroke under it and its description below.
    /// Lobby page, inside the hall, over deep top and bottom gradients: an ensō and the room's name at the top left, and at the top right "房号 · 公开 · n / 六", a label over every seat
    /// (● 你 房主 or a guest's name; empty seats show nothing) that follows the seat on screen and fades as players come and go, the sand
    /// table's label, and along the bottom baseline "Esc 离开房间" on the left and 形象 · 技能 · the tag on the right.
    /// While the host is alone the room races solo: the tag reads 单人修行, 简单 · 普通 · 困难 sit left of it (← → or a
    /// click; the current one has its ink on the baseline), a line over it says "玩家 + n 位 AI", and the empty seats show
    /// the AI's names, dimmed. Enter or a click on the tag starts the solo race (SessionControl.StartSoloHost, with those
    /// names): the tag reads 正在进入 with a turning ensō, and a failure is written over the tag. With guests the tag reads
    /// 开始修行 for the host (enabled once every guest is ready) and 准备 / 取消准备 for a guest. Seats show the room's
    /// players (RoomStateManager) with their ready state. The sand table's label names the room's map; while the
    /// table is looked at the host switches maps with ← →. Esc leaves the room unless the sand table is being looked at;
    /// the host leaving with guests in the room is asked first (解散 / 留下). Back from a match while still in the room
    /// (the Steam lobby is kept), the menu skips its opening and walks straight into the hall.
    /// Seat labels sit on a dark backing. The bell rope's pull hint is built in the same style (the builder hands it to the
    /// rope). Hides the TEMP door panel, the TEMP rope hint and the TEMP leave button.
    /// Built by Assets/Editor/UI/MenuTitleHomeBuilder.cs.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuDojoPage : MonoBehaviour
    {
        [System.Serializable]
        public class DoorItem
        {
            public TextMeshProUGUI text;
            [TextArea] public string description;
            [Tooltip("Pressed on submit; empty = not wired yet (a notice says so).")]
            public Button target;
            [Tooltip("Opens the join panel instead.")]
            public bool opensJoin;
            [Tooltip("Not open yet: greyed (0.3), marked 即将开放, cannot be chosen.")]
            public bool comingSoon;
            [System.NonSerialized] public float lift;
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private MenuCameraDirector director;
        [SerializeField] private MenuRoomView roomView;
        [SerializeField] private MenuLobby lobby;
        [SerializeField] private MenuSandTableFocus sandTable;
        [SerializeField] private Renderer sandTableRenderer;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Sprite dotSprite;

        [Header("Door page")]
        [SerializeField] private CanvasGroup doorPage;
        [SerializeField] private DoorItem[] doorItems = new DoorItem[0];
        [SerializeField] private TextMeshProUGUI doorDescription;
        [SerializeField] private RectTransform doorDot;
        [SerializeField] private RectTransform doorInk;
        [SerializeField] private TextMeshProUGUI doorBack;
        [Tooltip("Left edge of the menu items (canvas pixels from the left; the menu box sits on the door's lower right panel).")]
        [SerializeField] private float itemLeft = 1376f;
        [SerializeField] private float itemTop = 686f;
        [SerializeField] private float itemStep = 46f;
        [Tooltip("Room the description takes under the selected item.")]
        [SerializeField] private float descriptionRoom = 34f;

        [Header("Join panel (加入房间)")]
        [Tooltip("The menu's items, dot, ink and description; fades out while the join panel is open.")]
        [SerializeField] private CanvasGroup doorMenu;
        [SerializeField] private CanvasGroup joinPanel;
        [SerializeField] private TextMeshProUGUI[] codeCells = new TextMeshProUGUI[0];
        [SerializeField] private Image[] codeLines = new Image[0];
        [SerializeField] private RectTransform codeField;
        [SerializeField] private JoinRoom[] rooms = new JoinRoom[0];
        [SerializeField] private TextMeshProUGUI joinHint;

        [System.Serializable]
        public class JoinRoom
        {
            public string name;
            public int players;
            public int capacity = 6;
            public RectTransform row;
            public Image back;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI infoText;
            [System.NonSerialized] public float lift;
        }

        [Header("Lobby page")]
        [SerializeField] private CanvasGroup lobbyPage;
        [SerializeField] private TextMeshProUGUI roomTitle;
        [SerializeField] private TextMeshProUGUI roomInfo;
        [SerializeField] private RectTransform seatLabels;
        [SerializeField] private RectTransform sandLabel;
        [SerializeField] private TextMeshProUGUI leaveHint;
        [SerializeField] private TextMeshProUGUI[] lobbyItems = new TextMeshProUGUI[0];
        [SerializeField] private RectTransform startButton;
        [SerializeField] private string roomName = "清风禅院";
        [Tooltip("Names guests get in join order (the host is 你).")]
        [SerializeField] private string[] guestNames = { "云游僧", "无尘", "明心", "慧觉", "了凡" };

        [Header("Solo (the host alone in the room)")]
        [Tooltip("The three difficulties left of the tag; shown only while the host is alone.")]
        [SerializeField] private CanvasGroup soloGroup;
        [SerializeField] private TextMeshProUGUI[] difficultyItems = new TextMeshProUGUI[0];
        [SerializeField] private RectTransform difficultyInk;
        [SerializeField] private TextMeshProUGUI startLabel;
        [Tooltip("T5 line over the tag: who races, or why the start failed.")]
        [SerializeField] private TextMeshProUGUI startNote;
        [SerializeField] private Image startSpinner;
        [Tooltip("练习: a click (or P) starts the same solo host with no AI (Match/ModeRule Practice: three training dummies); shown only while the host is alone.")]
        [SerializeField] private TextMeshProUGUI practiceItem;
        [Tooltip("With guests 形象 · 技能 slide right by this much, into the room the difficulties leave.")]
        [SerializeField] private float itemsShiftWithGuests = 216f;

        [Header("Notice")]
        [SerializeField] private TextMeshProUGUI notice;

        [Header("TEMP panels hidden while these pages are up")]
        [SerializeField] private GameObject tempDoorPanel;
        [SerializeField] private GameObject[] tempHidden = new GameObject[0];

        private static readonly Color Paper = new Color32(0xF4, 0xEF, 0xE6, 0xFF);
        private static readonly Color Vermilion = new Color32(0xB8, 0x35, 0x2A, 0xFF);
        private static readonly string[] Numerals = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

        private RectTransform _canvasRect;
        private int _selected;
        private float _inkX, _inkW, _dotY;
        private float _noticeT;
        private string _roomCode = "";
        private int _openedFrame = -1;
        private bool _joinOpen;
        private float _joinT;
        private int _joinFrame = -1;
        private string _code = "";
        private int _roomSelected = -1;   // -1: the room number field

        private sealed class Seat
        {
            public RectTransform rect;
            public CanvasGroup group;
            public RectTransform back;
            public Image dot;
            public TextMeshProUGUI text;
            public string name;
            public float shown;
        }

        private readonly List<Seat> _seats = new List<Seat>();
        private float _startHover;

        private readonly MenuRoomSession _session = new MenuRoomSession();
        private bool _awaitingLobby;
        private float _orphanT;
        private bool _sandWasActive;
        private List<LobbyListItemData> _listedShown = new List<LobbyListItemData>();

        /// <summary>One of the online pages is up (the menu hides its proto panel then).</summary>
        public bool IsShown => DoorShown || LobbyShown;

        private bool DoorShown => director != null && director.InRoomView && !director.IsMoving && (roomView == null || !roomView.IsInside);
        private bool LobbyShown => roomView != null && roomView.IsInside && (sandTable == null || !sandTable.IsActive);

        public void Configure(Camera cam, MenuCameraDirector dir, MenuRoomView room, MenuLobby lob, MenuSandTableFocus sand, Renderer sandRenderer,
                              TMP_FontAsset f, Sprite dot)
        {
            targetCamera = cam; director = dir; roomView = room; lobby = lob; sandTable = sand; sandTableRenderer = sandRenderer; font = f; dotSprite = dot;
        }

        public void SetDoor(CanvasGroup page, DoorItem[] items, TextMeshProUGUI desc, RectTransform dot, RectTransform ink, TextMeshProUGUI back)
        {
            doorPage = page; doorItems = items; doorDescription = desc; doorDot = dot; doorInk = ink; doorBack = back;
        }

        public void SetJoin(CanvasGroup menu, CanvasGroup panel, TextMeshProUGUI[] cells, Image[] lines, RectTransform field, JoinRoom[] list,
                            TextMeshProUGUI hint)
        {
            doorMenu = menu; joinPanel = panel; codeCells = cells; codeLines = lines; codeField = field; rooms = list; joinHint = hint;
        }

        public void SetLobby(CanvasGroup page, TextMeshProUGUI title, TextMeshProUGUI info, RectTransform seats, RectTransform sand,
                             TextMeshProUGUI leave, TextMeshProUGUI[] items, RectTransform start, TextMeshProUGUI noticeText,
                             GameObject doorPanel, GameObject[] hidden)
        {
            lobbyPage = page; roomTitle = title; roomInfo = info; seatLabels = seats; sandLabel = sand; leaveHint = leave;
            lobbyItems = items; startButton = start; notice = noticeText; tempDoorPanel = doorPanel; tempHidden = hidden;
        }

        public void SetSolo(CanvasGroup group, TextMeshProUGUI[] difficulties, RectTransform ink, TextMeshProUGUI label, TextMeshProUGUI note, Image spinner, TextMeshProUGUI practice = null)
        {
            soloGroup = group; difficultyItems = difficulties; difficultyInk = ink; startLabel = label; startNote = note; startSpinner = spinner;
            practiceItem = practice;
        }

        // Same key as the old solo setup panel, so the last difficulty carries over.
        private const string DifficultyKey = "SoloMatch.Difficulty";
        private SoloDifficulty _difficulty = SoloDifficulty.Normal;
        private bool _soloRequested;
        private string _soloError = "";
        private float _soloT, _diffInkX, _diffInkW;
        // With guests: the tag shows 正在进入 from the host's start until the room moves into the race.
        private float _onlineStartT;
        [System.NonSerialized] private float[] _itemsSoloX;

        /// <summary>Match/ModeRule (Solo) owns the AI count; RacerId.MaxAI is the fallback and the upper bound.</summary>
        private static int SoloAICount()
        {
            ModeRuleRecord solo = DesignTableOverlay.ModeRule("Solo");
            return solo != null ? Mathf.Clamp(solo.aiCount, 0, RacerId.MaxAI) : RacerId.MaxAI;
        }

        private int TakenCount() => _session.PlayerCount;

        /// <summary>Match/ModeRule (Solo) defaultDifficulty until the player has picked one.</summary>
        private static SoloDifficulty DefaultDifficulty()
        {
            ModeRuleRecord solo = DesignTableOverlay.ModeRule("Solo");
            return solo != null && System.Enum.TryParse(solo.defaultDifficulty, true, out SoloDifficulty d) ? d : SoloDifficulty.Normal;
        }

        /// <summary>The UI/MenuOption row marked isDefault in a group (new rooms' max_players and visibility).</summary>
        private static string MenuOptionDefault(string group, string fallback)
        {
            ProjectConfigDatabase db = DesignTableOverlay.Db;
            if (db == null || db.menuOptions == null) return fallback;
            foreach (MenuOptionRecord option in db.menuOptions)
                if (option != null && option.optionGroup == group && option.isDefault && !string.IsNullOrWhiteSpace(option.value)) return option.value;
            return fallback;
        }

        /// <summary>
        /// Offline, or the host alone in an online room: the room races solo against AI. A guest never races solo, and
        /// while a joined room's roster has not arrived yet (Connecting) nothing can be started.
        /// </summary>
        private bool IsSolo => !_session.InLobby || (_session.IsOnline && _session.IsHost && _session.PlayerCount <= 1);

        private bool Connecting => _session.InLobby && !_session.IsOnline;

        /// <summary>The AI names in seat order: the empty seats after the host's, as many as the race has AI.</summary>
        private List<string> SoloNames()
        {
            var names = new List<string>();
            int count = SoloAICount();
            for (int i = 0; i < guestNames.Length && names.Count < count; i++) names.Add(guestNames[i]);
            while (names.Count < count) names.Add("道友" + Numeral(names.Count + 1));
            return names;
        }

        private bool _practiceHover;

        /// <summary>The same solo host; practice has no AI (SoloMatchRules maps 0 AI to the Match/ModeRule Practice row).</summary>
        private void StartSolo(bool practice = false)
        {
            if (_soloRequested) return;
            PlayerPrefs.SetInt(DifficultyKey, (int)_difficulty);
            PlayerPrefs.Save();
            _soloError = "";
            var settings = practice ? new SoloMatchSettings(0, _difficulty) : new SoloMatchSettings(SoloAICount(), _difficulty, SoloNames());
            if (!_session.StartSolo(settings, out string error)) { _soloError = error; return; }
            _soloRequested = true;
        }

        /// <summary>Tag, difficulty and the line over the tag follow the head count and the solo start.</summary>
        private void UpdateSolo(float dt)
        {
            if (_onlineStartT > 0f) _onlineStartT = Mathf.Max(0f, _onlineStartT - dt);
            ISessionControl session = SessionControl.Current;
            if (_soloRequested && session != null && !session.IsStarting && !string.IsNullOrEmpty(session.LastError))
            {
                _soloRequested = false;
                _soloError = session.LastError;
            }
            bool solo = IsSolo;
            _soloT = Mathf.MoveTowards(_soloT, solo ? 1f : 0f, dt / 0.3f);
            if (soloGroup != null) soloGroup.alpha = _soloT;
            // 形象 · 技能: built at their solo places; with guests they close the gap the difficulties leave.
            if (_itemsSoloX == null || _itemsSoloX.Length != lobbyItems.Length)
            {
                _itemsSoloX = new float[lobbyItems.Length];
                for (int i = 0; i < lobbyItems.Length; i++) _itemsSoloX[i] = lobbyItems[i].rectTransform.anchoredPosition.x;
            }
            float shift = itemsShiftWithGuests * (1f - _soloT * _soloT * (3f - 2f * _soloT));
            for (int i = 0; i < lobbyItems.Length; i++)
            {
                RectTransform rt = lobbyItems[i].rectTransform;
                rt.anchoredPosition = new Vector2(_itemsSoloX[i] + shift, rt.anchoredPosition.y);
            }
            for (int i = 0; i < difficultyItems.Length; i++)
                difficultyItems[i].alpha = Mathf.MoveTowards(difficultyItems[i].alpha, i == (int)_difficulty ? 1f : 0.65f, dt / 0.15f);
            if (difficultyInk != null && difficultyItems.Length > (int)_difficulty)
            {
                RectTransform t = difficultyItems[(int)_difficulty].rectTransform;
                float k = 1f - Mathf.Exp(-14f * dt);
                float goalW = difficultyItems[(int)_difficulty].preferredWidth + 8f;
                if (_diffInkW <= 0f) _diffInkX = t.anchoredPosition.x - 4f;
                _diffInkX = Mathf.Lerp(_diffInkX, t.anchoredPosition.x - 4f, k);
                _diffInkW = Mathf.Lerp(_diffInkW, goalW, k);
                difficultyInk.anchoredPosition = new Vector2(_diffInkX, difficultyInk.anchoredPosition.y);
                difficultyInk.sizeDelta = new Vector2(_diffInkW, difficultyInk.sizeDelta.y);
            }
            bool starting = solo ? _soloRequested : _onlineStartT > 0f || _session.IsStartingMatch;
            bool guestReady = _session.TryGetLocalPlayer(out RoomPlayerState me) && me.IsReady;
            if (startLabel != null)
                startLabel.text = Connecting ? "正在连接" : starting ? "正在进入" : solo ? "单人修行" : _session.IsHost ? "开始修行" : guestReady ? "取消准备" : "准备";
            if (startNote != null)
                startNote.text = Connecting ? "" : !solo ? (starting ? ""
                                          : !_session.IsHost ? (guestReady ? "等房主开局" : "准备好了就按 Enter")
                                          : "玩家 + " + Numeral(TakenCount() - 1) + " 位道友" + (_session.CanHostStart ? "" : "　<color=#F4EFE6AA>等道友准备</color>"))
                               : !string.IsNullOrEmpty(_soloError) ? "<color=#B8352A>" + _soloError + "</color>"
                               : _soloRequested ? ""
                               : _practiceHover ? "不带 AI　场上立着木桩"
                               : "玩家 + " + Numeral(SoloAICount()) + " 位 AI";
            if (practiceItem != null)
                practiceItem.alpha = Mathf.MoveTowards(practiceItem.alpha, _soloRequested ? 0.3f : _practiceHover ? 1f : 0.65f, dt / 0.15f);
            if (startSpinner != null)
            {
                Color c = startSpinner.color;
                c.a = Mathf.MoveTowards(c.a, starting ? 0.85f : 0f, dt / 0.15f);
                startSpinner.color = c;
                startSpinner.rectTransform.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * 240f);
            }
        }

        private void Awake()
        {
            _difficulty = (SoloDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, (int)DefaultDifficulty()), 0, 2);
            _canvasRect = transform as RectTransform;
            doorPage.alpha = 0f;
            lobbyPage.alpha = 0f;
            // Names start hidden: they come in once the view has settled in the hall (UpdateLobby).
            if (seatLabels != null && seatLabels.GetComponent<CanvasGroup>() is CanvasGroup names) names.alpha = 0f;
            if (sandLabel != null && sandLabel.GetComponent<TextMeshProUGUI>() is TextMeshProUGUI sand) sand.alpha = 0f;
            if (notice != null) notice.alpha = 0f;
            if (doorDescription != null) doorDescription.text = doorItems.Length > 0 ? doorItems[0].description : "";
            if (joinPanel != null) joinPanel.alpha = 0f;
            ShowListedRooms();
        }

        private void Start()
        {
            if (tempDoorPanel != null)
            {
                var g = tempDoorPanel.GetComponent<CanvasGroup>();
                if (g == null) g = tempDoorPanel.AddComponent<CanvasGroup>();
                g.alpha = 0f;
                g.blocksRaycasts = false;
            }
            foreach (GameObject go in tempHidden)
                if (go != null) go.SetActive(false);
            if (lobby != null)
                for (int i = 0; i < lobby.Capacity; i++) _seats.Add(MakeSeat(i));
            if (lobby != null) lobby.Changed += OnLobbyChanged;
            if (roomView != null) roomView.Entered += OnEntered;
            _session.Entered += OnSessionEntered;
            _session.Exited += OnSessionExited;
            _session.Failed += OnSessionFailed;
            _session.ListChanged += ShowListedRooms;
            _session.Tick();
            // A solo start that failed after the menu unloaded comes back here with its error.
            ISessionControl control = SessionControl.Current;
            if (control != null && control.TakeFailedSoloSettings() != null)
                _soloError = string.IsNullOrEmpty(control.LastError) ? "启动失败，请重试" : control.LastError;
            // Back from a match with the room kept: straight into the hall.
            if (_session.InLobby) StartCoroutine(ReturnToHall());
        }

        private void OnDestroy()
        {
            SetTyping(false);
            if (lobby != null) lobby.Changed -= OnLobbyChanged;
            if (roomView != null) roomView.Entered -= OnEntered;
            _session.Entered -= OnSessionEntered;
            _session.Exited -= OnSessionExited;
            _session.Failed -= OnSessionFailed;
            _session.ListChanged -= ShowListedRooms;
            _session.Dispose();
        }

        // ---------- network ----------

        private IEnumerator ReturnToHall()
        {
            MenuTitleHome title = FindFirstObjectByType<MenuTitleHome>();
            if (title != null)
            {
                title.SkipIntro();
                while (!title.IsHome) yield return null;
            }
            if (director != null && !director.InRoomView)
            {
                director.PlayToRoom();
                while (director.IsMoving || !director.InRoomView)
                {
                    if (!_session.InLobby) yield break;
                    yield return null;
                }
            }
            if (roomView != null && !roomView.IsInside && _session.InLobby) roomView.Enter();
        }

        private void CreateRoom()
        {
            if (_awaitingLobby) return;
            if (!_session.CanGoOnline)
            {
                // No Steam: the hall still opens and races solo.
                Notice(_session.OfflineReason);
                if (roomView != null) roomView.Enter();
                return;
            }
            _awaitingLobby = true;
            Notice("正在开门");
            int seats = lobby != null ? lobby.Capacity : 6;
            int capacity = int.TryParse(MenuOptionDefault("max_players", seats.ToString()), out int max) ? Mathf.Clamp(max, 2, seats) : seats;
            LobbyVisibility visibility = System.Enum.TryParse(MenuOptionDefault("visibility", "Public"), true, out LobbyVisibility v) ? v : LobbyVisibility.Public;
            _session.CreateRoom(roomName, capacity, visibility);
        }

        private void OnSessionEntered()
        {
            _awaitingLobby = false;
            if (_joinOpen) CloseJoin();
            if (roomView != null && !roomView.IsInside) roomView.Enter();
        }

        private void OnSessionExited(string message)
        {
            _awaitingLobby = false;
            _onlineStartT = 0f;
            if (roomView != null && roomView.IsInside) roomView.Leave();
            if (!string.IsNullOrEmpty(message)) Notice(message);
        }

        private void OnSessionFailed(string message)
        {
            _awaitingLobby = false;
            Notice(message);
        }

        /// <summary>The seats follow the room's head count; offline the host sits alone.</summary>
        private void SyncSeats()
        {
            if (lobby == null || lobby.HostSpot < 0) return;
            int want = Mathf.Min(_session.PlayerCount, lobby.Capacity);
            while (lobby.PlayerCount < want && lobby.Join() >= 0) { }
            while (lobby.PlayerCount > want && lobby.Leave() >= 0) { }
        }

        /// <summary>A lobby joined but its connection failed: leave it and say why.</summary>
        private void WatchConnection(float dt)
        {
            ISessionControl control = SessionControl.Current;
            bool orphan = _session.InLobby && !_session.IsOnline && roomView != null && roomView.IsInside
                          && control != null && !control.IsStarting;
            _orphanT = orphan ? _orphanT + dt : 0f;
            if (_orphanT < 1.5f) return;
            _orphanT = 0f;
            string error = control.LastError;
            _session.Leave();
            Notice(string.IsNullOrEmpty(error) ? "连接失败，请重试" : error);
        }

        private static string MapName(string mapId)
        {
            SelectionCatalog catalog = SelectionCatalog.Current;
            if (catalog == null) return "";
            return catalog.TryGetMap(catalog.NormalizeMapId(mapId), out SelectionCatalog.Map map) ? map.displayName : "";
        }

        /// <summary>Looking at the sand table: the host switches the room's map with ← → (A / D).</summary>
        private void HandleSandTable()
        {
            bool active = sandTable != null && sandTable.IsActive;
            bool entered = active && !_sandWasActive;
            _sandWasActive = active;
            if (!active) return;
            SelectionCatalog catalog = SelectionCatalog.Current;
            int count = catalog != null ? catalog.Maps.Count : 0;
            if (entered)
                Notice("地图　" + MapName(_session.MapId) + (_session.IsHost && count > 1 ? "　← → 换图" : ""));
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            int step = (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame ? -1 : 0)
                     + (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame ? 1 : 0);
            if (step == 0) return;
            if (!_session.IsHost) { Notice("地图由房主选择"); return; }
            if (count <= 1) { Notice("眼下只有「" + MapName(_session.MapId) + "」一张地图"); return; }
            int current = 0;
            for (int i = 0; i < count; i++) if (catalog.Maps[i].id == catalog.NormalizeMapId(_session.MapId)) current = i;
            SelectionCatalog.Map next = catalog.Maps[(current + step + count) % count];
            LocalMatchSelection.SetMap(next.id);
            Notice("地图　" + next.displayName);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _session.Tick();
            SyncSeats();
            WatchConnection(dt);
            HandleSandTable();
            // The lobby gives way to the confirm dialog (one focus, mockup 10).
            bool door = DoorShown, inside = LobbyShown && !MenuConfirmDialog.IsOpen;
            doorPage.alpha = Mathf.MoveTowards(doorPage.alpha, door ? 1f : 0f, dt / 0.3f);
            lobbyPage.alpha = Mathf.MoveTowards(lobbyPage.alpha, inside ? 1f : 0f, dt / 0.35f);
            if (doorPage.alpha > 0f) UpdateDoor(door, dt);
            if (lobbyPage.alpha > 0f) UpdateLobby(inside, dt);
            if (notice != null)
            {
                _noticeT = Mathf.MoveTowards(_noticeT, 0f, dt);
                notice.alpha = Mathf.Clamp01(_noticeT / 0.3f);
            }
        }

        private void Notice(string text)
        {
            if (notice == null) return;
            notice.text = text;
            _noticeT = 1.8f;
        }

        // ---------- door page ----------

        private void UpdateDoor(bool active, float dt)
        {
            if (!active && _joinOpen) CloseJoin();
            if (active && Time.frameCount != _openedFrame && Time.frameCount != _joinFrame)
            {
                if (_joinOpen) HandleJoinInput();
                else HandleDoorInput();
            }
            UpdateJoin(dt);
            float y = itemTop;
            for (int i = 0; i < doorItems.Length; i++)
            {
                DoorItem item = doorItems[i];
                item.lift = Mathf.MoveTowards(item.lift, i == _selected ? 1f : 0f, dt / 0.18f);
                float e = item.lift * item.lift * (3f - 2f * item.lift);
                item.text.fontSize = 22f; // T3; the selected item brightens, it does not grow
                item.text.alpha = item.comingSoon ? 0.3f : Mathf.Lerp(0.65f, 1f, e);
                item.text.rectTransform.anchoredPosition = new Vector2(itemLeft + 4f * e, -y);
                if (i == _selected)
                {
                    if (doorDescription != null)
                        doorDescription.rectTransform.anchoredPosition = new Vector2(itemLeft + 2f, -(y + 44f));
                    float k = 1f - Mathf.Exp(-14f * dt);
                    _dotY = Mathf.Lerp(_dotY == 0f ? y : _dotY, y, k);
                    _inkW = Mathf.Lerp(_inkW, item.text.preferredWidth + 24f, k);
                }
                y += itemStep + (descriptionRoom * e);
            }
            if (doorDot != null) doorDot.anchoredPosition = new Vector2(itemLeft - 22f, -(_dotY + 15f));
            if (doorInk != null)
            {
                doorInk.anchoredPosition = new Vector2(itemLeft - 4f, -(_dotY + 30f));
                doorInk.sizeDelta = new Vector2(_inkW, doorInk.sizeDelta.y);
            }
        }

        private void HandleDoorInput()
        {
            int move = 0;
            bool submit = false, back = false;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) move--;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) move++;
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) submit = true;
                if (kb.escapeKey.wasPressedThisFrame) back = true;
            }
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame) move--;
                if (pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame) move++;
                if (pad.buttonSouth.wasPressedThisFrame) submit = true;
                if (pad.buttonEast.wasPressedThisFrame) back = true;
            }
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 at = mouse.position.ReadValue();
                for (int i = 0; i < doorItems.Length; i++)
                {
                    if (doorItems[i].comingSoon || !MenuHit.Contains(doorItems[i].text.rectTransform, at, 14f)) continue;
                    if (mouse.delta.ReadValue().sqrMagnitude > 0f) Select(i);
                    if (mouse.leftButton.wasPressedThisFrame) { Select(i); submit = true; }
                }
                if (mouse.leftButton.wasPressedThisFrame && MenuHit.Contains(doorBack != null ? doorBack.rectTransform : null, at, 10f)) back = true;
            }
            if (move != 0)
            {
                // Steps over items that are not open yet.
                int next = _selected + move;
                while (next >= 0 && next < doorItems.Length && doorItems[next].comingSoon) next += move;
                if (next >= 0 && next < doorItems.Length) Select(next);
            }
            if (back) { if (director != null) director.ReturnFromRoom(); return; }
            if (!submit || doorItems.Length == 0) return;
            DoorItem item = doorItems[_selected];
            if (item.comingSoon) return;
            if (item.opensJoin) OpenJoin();
            else if (item.target != null) CreateRoom();
            else Notice(item.text.text + "　尚未开放");
        }

        // ---------- join panel ----------

        private void OpenJoin()
        {
            _joinOpen = true;
            _joinFrame = Time.frameCount;   // the key that opened it must not also act on it
            _code = "";
            _roomSelected = -1;
            SetTyping(true);
            if (_session.CanGoOnline) _session.RefreshList();
            else Notice(_session.OfflineReason);
        }

        /// <summary>The rows show the rooms Steam listed (as many as there are rows); the rest are hidden.</summary>
        private void ShowListedRooms()
        {
            _listedShown = new List<LobbyListItemData>(_session.Listed);
            for (int i = 0; i < rooms.Length; i++)
            {
                JoinRoom r = rooms[i];
                bool shown = i < _listedShown.Count;
                if (r.row != null) r.row.gameObject.SetActive(shown);
                if (!shown) continue;
                LobbyListItemData room = _listedShown[i];
                r.name = string.IsNullOrWhiteSpace(room.LobbyName) ? "无名禅院" : room.LobbyName;
                r.players = room.CurrentPlayers;
                r.capacity = room.MaxPlayers;
                if (r.nameText != null) r.nameText.text = r.name;
                if (r.infoText != null) r.infoText.text = Numeral(r.players) + " / " + Numeral(r.capacity);
            }
            if (_roomSelected >= VisibleRooms) _roomSelected = -1;
        }

        private int VisibleRooms => Mathf.Min(rooms.Length, _listedShown.Count);

        private void CloseJoin()
        {
            _joinOpen = false;
            _joinFrame = Time.frameCount;
            SetTyping(false);
        }

        private bool _typing;

        private void SetTyping(bool on)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || on == _typing) return;
            _typing = on;
            if (on) kb.onTextInput += OnText;
            else kb.onTextInput -= OnText;
        }

        /// <summary>Letters and digits go into the room number (upper case, four at most).</summary>
        private void OnText(char c)
        {
            if (!_joinOpen || !char.IsLetterOrDigit(c) || c > 127) return;
            if (_code.Length >= codeCells.Length) return;
            _code += char.ToUpperInvariant(c);
            _roomSelected = -1;
        }

        private void HandleJoinInput()
        {
            bool back = false, submit = false;
            int move = 0;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                // Letters come in as text, so W / S are not moves here.
                if (kb.upArrowKey.wasPressedThisFrame) move--;
                if (kb.downArrowKey.wasPressedThisFrame) move++;
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) submit = true;
                if (kb.escapeKey.wasPressedThisFrame) back = true;
                if (kb.backspaceKey.wasPressedThisFrame && _code.Length > 0) { _code = _code.Substring(0, _code.Length - 1); _roomSelected = -1; }
            }
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame) move--;
                if (pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame) move++;
                if (pad.buttonSouth.wasPressedThisFrame) submit = true;
                if (pad.buttonEast.wasPressedThisFrame) back = true;
            }
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 at = mouse.position.ReadValue();
                bool moved = mouse.delta.ReadValue().sqrMagnitude > 0f;
                bool click = mouse.leftButton.wasPressedThisFrame;
                for (int i = 0; i < VisibleRooms; i++)
                {
                    if (!MenuHit.Contains(rooms[i].row, at, 4f)) continue;
                    if (moved) _roomSelected = i;
                    if (click) { _roomSelected = i; submit = true; }
                }
                if (click && MenuHit.Contains(codeField, at, 8f)) _roomSelected = -1;
                if (click && MenuHit.Contains(doorBack != null ? doorBack.rectTransform : null, at, 10f)) back = true;
            }
            if (move != 0) _roomSelected = Mathf.Clamp(_roomSelected + move, -1, VisibleRooms - 1);
            if (back) { CloseJoin(); return; }
            if (!submit || _awaitingLobby) return;
            if (!_session.CanGoOnline) { Notice(_session.OfflineReason); return; }
            if (_roomSelected < 0 && _code.Length == codeCells.Length)
            {
                Notice("正在叩门　房号 " + _code);
                _awaitingLobby = true;
                _session.JoinByCode(_code);
            }
            else if (_roomSelected >= 0 && _roomSelected < _listedShown.Count)
            {
                Notice("正在叩门　「" + rooms[_roomSelected].name + "」");
                _awaitingLobby = true;
                _session.JoinListed(_listedShown[_roomSelected]);
            }
            else Notice("输入四位房号，或从列表里挑一个房间");
        }

        private void UpdateJoin(float dt)
        {
            _joinT = Mathf.MoveTowards(_joinT, _joinOpen ? 1f : 0f, dt / (_joinOpen ? 0.3f : 0.22f));
            float e = 1f - (1f - _joinT) * (1f - _joinT);
            if (doorMenu != null)
            {
                doorMenu.alpha = 1f - e;
                ((RectTransform)doorMenu.transform).anchoredPosition = new Vector2(-24f * e, 0f);
            }
            if (joinPanel != null)
            {
                joinPanel.alpha = e;
                ((RectTransform)joinPanel.transform).anchoredPosition = new Vector2(24f * (1f - e), 0f);
            }
            if (_joinT <= 0f) return;

            // Room number: typed characters, the next cell's line pulses while the field has the focus.
            bool fieldFocus = _roomSelected < 0;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < codeCells.Length; i++)
            {
                codeCells[i].text = i < _code.Length ? _code[i].ToString() : "";
                if (i >= codeLines.Length || codeLines[i] == null) continue;
                bool next = fieldFocus && i == Mathf.Min(_code.Length, codeCells.Length - 1);
                codeLines[i].color = next ? Color.Lerp(Paper, Vermilion, pulse) : new Color(1f, 1f, 1f, i < _code.Length ? 0.9f : 0.45f);
            }
            // Rooms: the chosen one lifts on a dark backing.
            for (int i = 0; i < rooms.Length; i++)
            {
                JoinRoom r = rooms[i];
                r.lift = Mathf.MoveTowards(r.lift, i == _roomSelected ? 1f : 0f, dt / 0.15f);
                float k = r.lift * r.lift * (3f - 2f * r.lift);
                if (r.back != null) r.back.color = new Color(0.02f, 0.015f, 0.015f, 0.65f * k);
                if (r.nameText != null) { r.nameText.alpha = Mathf.Lerp(0.75f, 1f, k); r.nameText.rectTransform.anchoredPosition = new Vector2(14f + 4f * k, 0f); }
                if (r.infoText != null) r.infoText.alpha = Mathf.Lerp(0.65f, 0.9f, k);
            }
            if (joinHint != null)
                joinHint.text = (fieldFocus ? (_code.Length == codeCells.Length ? "<sprite name=\"key_enter\" tint=1> 凭房号入门" : "直接键入房号　Backspace 删除")
                                            : "<sprite name=\"key_enter\" tint=1> 加入这个房间");
        }

        private void Select(int i)
        {
            if (i == _selected || doorItems[i].comingSoon) return;
            _selected = i;
            if (doorDescription != null) doorDescription.text = doorItems[i].description;
        }

        // ---------- lobby page ----------

        private void OnEntered()
        {
            _openedFrame = Time.frameCount;
            OnLobbyChanged();
        }

        private void OnLobbyChanged()
        {
            if (lobby == null) return;
            bool online = _session.InLobby;
            _roomCode = online ? _session.RoomCode : "";
            int capacity = online && _session.Capacity > 0 ? _session.Capacity : lobby.Capacity;
            string visibility = !online ? "单机"
                              : _session.Visibility == LobbyVisibility.FriendsOnly ? "好友"
                              : _session.Visibility == LobbyVisibility.Private ? "私密" : "公开";
            if (roomTitle != null) roomTitle.text = online && !string.IsNullOrWhiteSpace(_session.RoomName) ? _session.RoomName : roomName;
            if (roomInfo != null)
                roomInfo.text = (online ? "房号 " + _roomCode + "　" : "") + visibility + "　" + Numeral(_session.PlayerCount) + " / " + Numeral(capacity);
        }

        /// <summary>
        /// Who sits on seat i: the host on the host's seat, the other players on the other taken seats in seat order.
        /// Offline the host's seat is 你 房主.
        /// </summary>
        private string SeatLabel(int i)
        {
            int host = lobby.HostSpot;
            if (!_session.IsOnline) return i == host ? HostLabel : null;
            IReadOnlyList<RoomPlayerState> players = _session.Players;
            RoomPlayerState? player = null;
            if (i == host)
            {
                foreach (RoomPlayerState p in players) if (p.IsHost) { player = p; break; }
            }
            else
            {
                int order = 0;
                for (int k = 0; k < i; k++) if (k != host && lobby.IsTaken(k)) order++;
                foreach (RoomPlayerState p in players)
                {
                    if (p.IsHost) continue;
                    if (order-- == 0) { player = p; break; }
                }
            }
            if (player == null) return null;
            RoomPlayerState who = player.Value;
            string name = _session.IsLocal(who) ? "你" : string.IsNullOrWhiteSpace(who.PlayerName) ? "道友" : who.PlayerName;
            string state = who.IsHost ? "房主" : who.IsReady ? "已准备" : "未准备";
            return name + "　<size=80%><color=#F4EFE6AA>" + state + "</color></size>";
        }

        private const string HostLabel = "你　<size=80%><color=#F4EFE6AA>房主</color></size>";

        private static string Numeral(int n) => n >= 0 && n < Numerals.Length ? Numerals[n] : n.ToString();

        private Seat MakeSeat(int i)
        {
            var go = new GameObject("Seat_" + i, typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(seatLabels, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(200f, 30f);
            // Dark backing so the name reads over the bright shoji and lanterns.
            var backGo = new GameObject("Back", typeof(RectTransform), typeof(Image));
            var brt = (RectTransform)backGo.transform;
            brt.SetParent(rt, false);
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            var back = backGo.GetComponent<Image>();
            back.color = new Color(0.02f, 0.015f, 0.015f, 0.62f);
            back.raycastTarget = false;
            var dotGo = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            var drt = (RectTransform)dotGo.transform;
            drt.SetParent(rt, false);
            drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(9f, 9f);
            var dot = dotGo.GetComponent<Image>();
            dot.sprite = dotSprite;
            dot.color = Vermilion;
            dot.raycastTarget = false;
            var textGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            var trt = (RectTransform)textGo.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0f, 0.5f);
            trt.sizeDelta = new Vector2(180f, 30f);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 18f;
            text.characterSpacing = 8f;
            text.color = Paper;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            var g = go.GetComponent<CanvasGroup>();
            g.alpha = 0f;
            return new Seat { rect = rt, group = g, back = brt, dot = dot, text = text };
        }

        private float _namesT;

        private void UpdateLobby(bool active, float dt)
        {
            if (lobby == null || targetCamera == null) return;
            // The seat names and the sand table's label wait until the view has settled in the hall (not while the doors open).
            _namesT = Mathf.MoveTowards(_namesT, roomView != null && roomView.IsSettled ? 1f : 0f, dt / 0.3f);
            if (seatLabels != null && seatLabels.GetComponent<CanvasGroup>() is CanvasGroup names) names.alpha = _namesT;
            if (sandLabel != null && sandLabel.GetComponent<TextMeshProUGUI>() is TextMeshProUGUI sand) sand.alpha = 0.8f * _namesT;
            UpdateSolo(dt);
            OnLobbyChanged();
            if (sandLabel != null && sandLabel.GetComponent<TextMeshProUGUI>() is TextMeshProUGUI sandText)
            {
                string map = MapName(_session.MapId);
                sandText.text = string.IsNullOrEmpty(map) ? "沙盘" : "沙盘　<size=80%>" + map + "</size>";
            }
            // Solo: the empty seats show this race's AI, dimmer than the players and without the dot.
            bool solo = IsSolo;
            List<string> aiNames = solo ? SoloNames() : null;
            int ai = 0;
            for (int i = 0; i < _seats.Count; i++)
            {
                Seat seat = _seats[i];
                Transform spot = lobby.SpotAt(i);
                bool taken = lobby.IsTaken(i);
                // Names follow who sits where (the room's roster; offline only the host).
                seat.name = taken ? SeatLabel(i) : null;
                // Taken seats show their player; empty seats show nothing, or an AI when solo. A change fades through.
                string label = taken ? seat.name ?? ""
                             : aiNames != null && ai < aiNames.Count ? "<alpha=#A6>" + aiNames[ai++] : "";
                if (seat.text.text != label)
                {
                    seat.shown = Mathf.MoveTowards(seat.shown, 0f, dt / 0.15f);
                    if (seat.shown <= 0f) { seat.text.text = label; seat.dot.enabled = taken; }
                }
                else seat.shown = Mathf.MoveTowards(seat.shown, 1f, dt / 0.3f);
                // A player who leaves fades out with their name; an empty seat stays hidden.
                seat.group.alpha = seat.text.text.Length > 0 ? seat.shown : 0f;
                float w = seat.text.preferredWidth + (taken ? 16f : 0f);
                ((RectTransform)seat.dot.transform).anchoredPosition = new Vector2(-w * 0.5f + 4f, 0f);
                seat.text.rectTransform.anchoredPosition = new Vector2(-w * 0.5f + (taken ? 16f : 0f), 0f);
                seat.back.sizeDelta = new Vector2(w + 28f, 30f);
                if (spot == null) continue;
                Vector3 head = spot.position + Vector3.up * (lobby.PlayerHeight + 0.2f);
                Vector3 screen = targetCamera.WorldToScreenPoint(head);
                if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local))
                    seat.rect.anchoredPosition = local + new Vector2(0f, 6f * (1f - seat.shown));
                seat.rect.gameObject.SetActive(screen.z > 0f);
            }
            if (sandLabel != null && sandTableRenderer != null)
            {
                Bounds b = sandTableRenderer.bounds;
                Vector3 screen = targetCamera.WorldToScreenPoint(b.center + Vector3.up * b.extents.y);
                if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local))
                    sandLabel.anchoredPosition = local + new Vector2(0f, -40f);
            }
            if (active && Time.frameCount != _openedFrame && !MenuConfirmDialog.Blocking) HandleLobbyInput(dt);
        }

        private void HandleLobbyInput(float dt)
        {
            bool leave = false;
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) leave = true;
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) leave = true;
            Mouse mouse = Mouse.current;
            Vector2 at = mouse != null ? mouse.position.ReadValue() : new Vector2(-1f, -1f);
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (click && MenuHit.Contains(leaveHint != null ? leaveHint.rectTransform : null, at, 10f)) leave = true;
            foreach (TextMeshProUGUI item in lobbyItems)
            {
                bool hover = MenuHit.Contains(item.rectTransform, at, 12f);
                item.alpha = Mathf.MoveTowards(item.alpha, hover ? 1f : 0.8f, dt / 0.12f);
                if (click && hover) Notice("进房前在主页的「" + item.text + "」里选好");
            }
            bool solo = IsSolo;
            bool start = kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
            if (solo && !_soloRequested)
            {
                // Difficulty: ← → (A / D) or a click on one of the three.
                int step = 0;
                if (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) step--;
                if (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) step++;
                for (int i = 0; i < difficultyItems.Length; i++)
                    if (click && MenuHit.Contains(difficultyItems[i].rectTransform, at, 10f)) { _difficulty = (SoloDifficulty)i; _soloError = ""; }
                if (step != 0)
                {
                    _difficulty = (SoloDifficulty)Mathf.Clamp((int)_difficulty + step, 0, 2);
                    _soloError = "";
                }
            }
            if (startButton != null)
            {
                bool hover = MenuHit.Contains(startButton, at, 6f);
                _startHover = Mathf.MoveTowards(_startHover, hover ? 1f : 0f, dt / 0.12f);
                startButton.localScale = Vector3.one * (1f + 0.05f * _startHover);
                if (click && hover) start = true;
            }
            _practiceHover = false;
            if (solo && !_soloRequested && practiceItem != null)
            {
                _practiceHover = MenuHit.Contains(practiceItem.rectTransform, at, 10f);
                if ((click && _practiceHover) || (kb != null && kb.pKey.wasPressedThisFrame)) { StartSolo(true); start = false; }
            }
            if (start && Connecting) start = false;
            if (start && solo) StartSolo();
            else if (start && !_session.IsHost) _session.ToggleReady();
            else if (start && _onlineStartT <= 0f && !_session.IsStartingMatch)
            {
                if (!_session.CanHostStart) Notice("等道友都准备好再开局");
                else
                {
                    _onlineStartT = 1.5f;
                    _session.StartMatch();
                }
            }
            if (_soloRequested || _onlineStartT > 0f || _session.IsStartingMatch) leave = false; // the race is starting: stay
            if (leave && roomView != null)
            {
                // The host leaving with guests in the room breaks it up: ask first. Alone or a guest, just leave.
                if (!solo && _session.IsHost && MenuConfirmDialog.Instance != null)
                    MenuConfirmDialog.Instance.Show("离开房间", "你是房主，离开后房间会解散，道友们也会回到殿门。", "解散",
                        LeaveRoom, "留下");
                else LeaveRoom();
            }
        }

        // Online the lobby is left and the hall closes when Steam confirms (OnSessionExited); offline it just closes.
        private void LeaveRoom()
        {
            if (_session.InLobby) _session.Leave();
            else if (roomView != null) roomView.Leave();
        }
    }
}
