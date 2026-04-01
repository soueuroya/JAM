using UnityEngine;
using UnityEngine.UIElements;
using JAM.Core;
using JAM.Data;
using System.Collections.Generic;

namespace JAM.UI
{
    public class MainUIController : MonoBehaviour
    {
        public static MainUIController Instance { get; private set; }

        [Header("UXML Templates (assign in Inspector)")]
        [SerializeField] private VisualTreeAsset _gameRowTemplate;

        private UIDocument    _doc;
        private VisualElement _root;
        private VisualElement _centerArea;
        private VisualElement _topBar;
        private VisualElement _bottomBar;

        // Live resource chip labels
        private readonly Dictionary<string, Label> _resourceChips = new();

        private static readonly (string id, string icon)[] TrackedResources =
        {
            ("logs",      "🪵"),
            ("iron_ore",  "🪨"),
            ("planks",    "🪚"),
            ("iron_bars", "⚙️"),
            ("tools",     "🔧"),
        };

        // Row definitions: (title, icon, resourceId/null, workerId, buildingId/null)
        private static readonly (string title, string icon, string resourceId, string workerId, string buildingId)[] Rows =
        {
            ("Tree",        "🌲", "logs",      "lumberjack",        null),
            ("Rock",        "⛏",  "iron_ore",  "miner",             null),
            ("Lumber Camp", "🪚", null,        "carpenter",         "lumber_camp"),
            ("Furnace",     "🔥", null,        "smelter",           "furnace"),
            ("Blacksmith",  "🔨", null,        "blacksmith_worker", "blacksmith"),
        };

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            _doc = GetComponent<UIDocument>();
            if (_doc == null) { Debug.LogError("[MainUIController] No UIDocument!"); return; }

            _root       = _doc.rootVisualElement;
            _centerArea = _root.Q<VisualElement>("CenterArea");
            _topBar     = _root.Q<VisualElement>("TopBar");
            _bottomBar  = _root.Q<VisualElement>("BottomBar");

            // Ensure overlay never blocks input
            var overlay = _root.Q<VisualElement>("OverlayLayer");
            if (overlay != null) overlay.pickingMode = PickingMode.Ignore;

            // Hide BottomBar — buildings are now rows in CenterArea
            if (_bottomBar != null) _bottomBar.style.display = DisplayStyle.None;
        }

        private void Start()
        {
            if (_gameRowTemplate == null)
                _gameRowTemplate = Resources.Load<VisualTreeAsset>("UI/UXML/GameRow");

            BuildTopBar();
            BuildGameRows();

            GameManager.Instance.ResourceSystem.OnResourceChanged += OnResourceChanged;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance?.ResourceSystem != null)
                GameManager.Instance.ResourceSystem.OnResourceChanged -= OnResourceChanged;
        }

        // ── Top Bar ────────────────────────────────────────────────────────────

        private void BuildTopBar()
        {
            _topBar.Clear();
            _topBar.style.flexDirection = FlexDirection.Row;
            _topBar.style.alignItems    = Align.Center;

            foreach (var (id, icon) in TrackedResources)
            {
                var chip = new VisualElement();
                chip.AddToClassList("resource-chip");

                var iconLbl  = new Label(icon);
                iconLbl.AddToClassList("resource-chip-icon");

                var valueLbl = new Label("0");
                valueLbl.AddToClassList("resource-chip-label");
                valueLbl.name = $"chip_{id}";

                chip.Add(iconLbl);
                chip.Add(valueLbl);
                _topBar.Add(chip);

                _resourceChips[id] = valueLbl;
            }

            // Spacer
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            _topBar.Add(spacer);

            // Debug reset button
            var resetBtn = new Button();
            resetBtn.text = "⟳ RESET";
            resetBtn.AddToClassList("debug-reset-btn");
            resetBtn.RegisterCallback<MouseUpEvent>(_  => DoReset());
            resetBtn.RegisterCallback<PointerUpEvent>(_ => DoReset());
            _topBar.Add(resetBtn);
        }

        private void OnResourceChanged(string id, float amount)
        {
            if (_resourceChips.TryGetValue(id, out var lbl))
                lbl.text = Mathf.FloorToInt(amount).ToString();
        }

        private void DoReset()
        {
            GameManager.Instance.ResourceSystem.ResetAllResources();
        }

        // ── Game Rows ──────────────────────────────────────────────────────────

        private void BuildGameRows()
        {
            if (_centerArea == null || _gameRowTemplate == null)
            {
                Debug.LogError("[MainUIController] CenterArea or GameRow template missing!");
                return;
            }

            _centerArea.Clear();
            _centerArea.style.flexDirection  = FlexDirection.Column;
            _centerArea.style.alignItems     = Align.Stretch;
            _centerArea.style.justifyContent = Justify.FlexStart;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.verticalScrollerVisibility   = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _centerArea.Add(scroll);

            var dataLoader = GameManager.Instance.DataLoader;

            foreach (var (title, icon, resourceId, workerId, buildingId) in Rows)
            {
                var rowEl = _gameRowTemplate.Instantiate();
                scroll.Add(rowEl);

                var ctrl = gameObject.AddComponent<GameRowController>();

                if (buildingId == null)
                {
                    // Resource node row
                    ctrl.InitAsNode(rowEl, title, icon, resourceId, workerId);
                }
                else
                {
                    // Building row
                    if (dataLoader.Buildings.TryGetValue(buildingId, out var bData))
                        ctrl.InitAsBuilding(rowEl, title, icon, bData, workerId);
                    else
                        Debug.LogError($"[MainUIController] Building '{buildingId}' not found in data.");
                }
            }
        }
    }
}
