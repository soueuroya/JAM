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
        private VisualElement _sideBar;
        private VisualElement _resourceScroll;
        private ScrollView    _mainScroll;
        private Button        _resetBtn;
        private Button        _mapBtn;

        // Live resource chip labels
        private readonly Dictionary<string, Label> _resourceChips = new Dictionary<string, Label>();
        private readonly Dictionary<string, VisualElement> _rowJumpPoints = new Dictionary<string, VisualElement>();

        private static readonly (string id, string icon)[] Tier1Resources =
        {
            ("logs",      "🪵"),
            ("iron_ore",  "🪨"),
            ("planks",    "🪚"),
            ("iron_bars", "⚙️"),
            ("tools",     "🔧"),
        };

        private static readonly (string id, string icon)[] Tier2Resources =
        {
            ("oil",       "🛢"),
            ("fuel",      "⛽"),
            ("steel",     "🧱"),
            ("machines",  "⚙️"),
            ("ship",      "⛵"),
        };

        private static readonly (string title, string icon, string resourceId, string workerId, string buildingId, bool isTier2)[] Rows =
        {
            // Tier 1
            ("Tree",          "🌲", "logs",      "lumberjack",        null,         false),
            ("Rock",          "⛏",  "iron_ore",  "miner",             null,         false),
            ("Lumber Camp",   "🪚", null,        "carpenter",         "lumber_camp", false),
            ("Furnace",       "🔥", null,        "smelter",           "furnace",     false),
            ("Blacksmith",    "🔨", null,        "blacksmith_worker", "blacksmith",  false),
            
            // Progression Bridge
            ("Shipyard",      "⛵", null,        "shipwright",        "shipyard",    false),

            // Tier 2 (Locked by default)
            ("Oil Well",      "⛽", "oil",       "roughneck",         null,         true),
            ("Refinery",      "🏭", null,        "refiner",           "refinery",    true),
            ("Foundry",       "🏗", null,        "steel_worker",      "foundry",     true),
            ("Workshop",      "🛠", null,        "engineer",          "workshop",    true),
        };

        private readonly List<VisualElement> _tier2Elements = new();
        private bool _isTier2Unlocked = false;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            _doc = GetComponent<UIDocument>();
            if (_doc == null) { Debug.LogError("[MainUIController] No UIDocument!"); return; }

            _root           = _doc.rootVisualElement;
            _centerArea     = _root.Q<VisualElement>("CenterArea");
            _sideBar        = _root.Q<VisualElement>("SideBar");
            _resourceScroll = _root.Q<VisualElement>("ResourceScroll");
            _resetBtn       = _root.Q<Button>("ResetBtn");
            _mapBtn         = _root.Q<Button>("MapBtn");

            // Ensure overlay never blocks input
            var overlay = _root.Q<VisualElement>("OverlayLayer");
            if (overlay != null) overlay.pickingMode = PickingMode.Ignore;

            if (_resetBtn != null)
            {
                _resetBtn.RegisterCallback<MouseUpEvent>(_ => DoReset());
                _resetBtn.RegisterCallback<PointerUpEvent>(_ => DoReset());
            }

            if (_mapBtn != null)
            {
                _mapBtn.RegisterCallback<MouseUpEvent>(_ => OnClickMap());
                _mapBtn.RegisterCallback<PointerUpEvent>(_ => OnClickMap());
            }
        }

        private void Start()
        {
            if (_gameRowTemplate == null)
                _gameRowTemplate = Resources.Load<VisualTreeAsset>("UI/UXML/GameRow");

            BuildSideBar();
            BuildGameRows();

            // Enable mouse-drag scrolling for Sidebar
            if (_resourceScroll != null)
                _resourceScroll.AddManipulator(new MouseDragScroll());

            GameManager.Instance.ResourceSystem.OnResourceChanged += OnResourceChanged;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance?.ResourceSystem != null)
                GameManager.Instance.ResourceSystem.OnResourceChanged -= OnResourceChanged;
        }

        // ── Top Bar ────────────────────────────────────────────────────────────

        private void BuildSideBar()
        {
            if (_resourceScroll == null) return;
            _resourceScroll.Clear();
            _resourceChips.Clear();

            // Initially only T1 and Ship (to see progress)
            foreach (var (id, icon) in Tier1Resources) AddChip(id, icon);
            
            // We hide T2 chips until unlocked
            if (_isTier2Unlocked)
            {
                foreach (var (id, icon) in Tier2Resources) AddChip(id, icon);
            }
        }

        private void AddChip(string id, string icon)
        {
            var chip = new VisualElement();
            chip.AddToClassList("resource-chip");
            var iconLbl = new Label(icon);
            iconLbl.AddToClassList("resource-chip-icon");
            var valueLbl = new Label("0");
            valueLbl.AddToClassList("resource-chip-label");
            valueLbl.name = $"chip_{id}";
            chip.Add(iconLbl);
            chip.Add(valueLbl);
            _resourceScroll.Add(chip);
            _resourceChips[id] = valueLbl;

            // Click to jump to the row that produces/contains this resource
            chip.RegisterCallback<PointerDownEvent>(_ => JumpToRow(id));
            
            // Sync current value
            float current = GameManager.Instance.ResourceSystem.GetResource(id);
            valueLbl.text = Mathf.FloorToInt(current).ToString();
        }

        private void OnResourceChanged(string id, float amount)
        {
            if (_resourceChips.TryGetValue(id, out var lbl))
                lbl.text = Mathf.FloorToInt(amount).ToString();

            // Show Map button when first ship is built
            if (id == "ship" && amount >= 1 && !_isTier2Unlocked)
            {
                _mapBtn.style.display = DisplayStyle.Flex;
            }
        }

        private void OnClickMap()
        {
            _mapBtn.style.display = DisplayStyle.None;
            UnlockTier2();
        }

        private void UnlockTier2()
        {
            _isTier2Unlocked = true;
            
            // Rebuild sidebar with T2 resources
            foreach (var (id, icon) in Tier2Resources)
                if (!_resourceChips.ContainsKey(id)) AddChip(id, icon);

            // Show the rows
            foreach (var el in _tier2Elements)
            {
                el.style.display = DisplayStyle.Flex;
            }
            Debug.Log("[MainUIController] Arrived at The Industrial Island!");
        }

        private void DoReset()
        {
            _isTier2Unlocked = false;
            _mapBtn.style.display = DisplayStyle.None;
            foreach (var el in _tier2Elements)
            {
                el.style.display = DisplayStyle.None;
            }

            GameManager.Instance.WorkerSystem.ResetAllWorkers();
            GameManager.Instance.ResourceSystem.ResetAllResources();
            
            // Sidebar rebuild to remove T2 chips visually
            BuildSideBar();
        }

        private void JumpToRow(string resourceId)
        {
            if (_rowJumpPoints.TryGetValue(resourceId, out var targetRow) && _mainScroll != null)
            {
                _mainScroll.ScrollTo(targetRow);
                
                // Visual feedback (brief highlight or just the scroll)
                Debug.Log($"[MainUIController] Jumping to producer of {resourceId}");
            }
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

            _mainScroll = new ScrollView(ScrollViewMode.Vertical);
            _mainScroll.style.flexGrow = 1;
            _mainScroll.verticalScrollerVisibility   = ScrollerVisibility.Hidden;
            _mainScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _mainScroll.AddManipulator(new MouseDragScroll());
            _centerArea.Add(_mainScroll);

            var dataLoader = GameManager.Instance.DataLoader;

            _tier2Elements.Clear();
            _rowJumpPoints.Clear();

            foreach (var (title, icon, resourceId, workerId, buildingId, isTier2) in Rows)
            {
                var rowTemplateContainer = _gameRowTemplate.Instantiate();
                var rowEl = rowTemplateContainer.Q<VisualElement>("GameRow");
                _mainScroll.Add(rowTemplateContainer);

                var ctrl = gameObject.AddComponent<GameRowController>();

                // Register jump point for this resource or building output
                if (!string.IsNullOrEmpty(resourceId))
                {
                    _rowJumpPoints[resourceId] = rowTemplateContainer;
                }

                if (isTier2)
                {
                    rowTemplateContainer.style.display = DisplayStyle.None;
                    _tier2Elements.Add(rowTemplateContainer);
                }

                if (buildingId == null)
                    ctrl.InitAsNode(rowTemplateContainer, title, icon, resourceId, workerId);
                else
                {
                    if (dataLoader.Buildings.TryGetValue(buildingId, out var bData))
                    {
                        ctrl.InitAsBuilding(rowTemplateContainer, title, icon, bData, workerId);
                        // Register the building's output as a jump point
                        // FIX: BuildingData does not have OutputDescriptor, so use output dictionary
                        if (bData.output != null)
                        {
                            foreach (var outputResource in bData.output.Keys)
                            {
                                _rowJumpPoints[outputResource] = rowTemplateContainer;
                            }
                        }
                    }
                }
            }
        }

        // ── Custom Scroll Manipulator (for Mouse Dragging) ───────────────────
        private class MouseDragScroll : MouseManipulator
        {
            private bool    _active;
            private Vector2 _lastPos;

            public MouseDragScroll()
            {
                activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<MouseDownEvent>(OnMouseDown);
                target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
                target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
                target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
                target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            }

            private void OnMouseDown(MouseDownEvent e)
            {
                if (CanStartManipulation(e))
                {
                    _active = true;
                    _lastPos = e.localMousePosition;
                    target.CaptureMouse();
                }
            }

            private void OnMouseMove(MouseMoveEvent e)
            {
                if (_active && target is ScrollView scroll)
                {
                    Vector2 diff = e.localMousePosition - _lastPos;
                    // Move the scroller offset (inverted for drag feeling)
                    scroll.scrollOffset -= new Vector2(0, diff.y);
                    _lastPos = e.localMousePosition;
                }
            }

            private void OnMouseUp(MouseUpEvent e)
            {
                if (_active)
                {
                    _active = false;
                    target.ReleaseMouse();
                }
            }
        }
    }
}
