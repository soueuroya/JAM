using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using JAM.Core;
using JAM.Data;

namespace JAM.UI
{
    /// <summary>
    /// Unified row controller for both resource nodes (tap) and buildings (unlock + produce).
    /// Embeds the corresponding worker card directly inside the row.
    /// </summary>
    public class GameRowController : MonoBehaviour
    {
        public enum RowType { ResourceNode, Building }

        // ── Config ─────────────────────────────────────────────────────────────
        private RowType     _type;
        private string      _resourceId;   // tapped resource (nodes) or primary output (buildings)
        private string      _buildingId;
        private string      _workerId;
        private WorkerData  _workerData;
        private BuildingData _buildingData;

        // ── State ──────────────────────────────────────────────────────────────
        private bool _isUnlocked;   // buildings start locked
        private bool _isCooldown;   // tap animation guard

        // ── UI refs ────────────────────────────────────────────────────────────
        private VisualElement _gameRow;
        private Label         _rowSub;
        private Button        _actionBtn;
        private VisualElement _prodBarBg;
        private VisualElement _prodBarFill;
        private Label         _workerLevelLabel;
        private Button        _workerBtn;
        private VisualElement _tickFill;

        // ── Tap animation ──────────────────────────────────────────────────────
        private static readonly Color _colDefault = new Color(0.118f, 0.118f, 0.118f);
        private static readonly Color _colCollect = new Color(0.05f,  0.17f,  0.06f);
        private static readonly Color _bdrDefault = new Color(0.165f, 0.165f, 0.165f);
        private static readonly Color _bdrComplete = Color.green;

        // ═══════════════════════════════════════════════════════════════════════
        //  Public Init
        // ═══════════════════════════════════════════════════════════════════════

        public void InitAsNode(VisualElement root, string title, string icon,
                               string resourceId, string workerId)
        {
            _type        = RowType.ResourceNode;
            _resourceId  = resourceId;
            _workerId    = workerId;
            _isUnlocked  = true;

            InitCommon(root, icon, title);

            _rowSub.text   = $"Tap to collect";
            _actionBtn.text = "TAP";
            _actionBtn.RegisterCallback<MouseUpEvent>(_ => TryTap());
            _actionBtn.RegisterCallback<PointerUpEvent>(_ => TryTap());

            GameManager.Instance.ResourceSystem.OnGlobalReset += OnGlobalReset;

            _prodBarBg.style.display = DisplayStyle.None;  // no prod bar for nodes
        }

        public void InitAsBuilding(VisualElement root, string title, string icon,
                                   BuildingData bData, string workerId)
        {
            _type         = RowType.Building;
            _buildingData = bData;
            _buildingId   = bData.id;
            _workerId     = workerId;
            _isUnlocked   = false;              // locked by default
            _resourceId   = bData.output.Keys.FirstOrDefault() ?? "";

            InitCommon(root, icon, title);

            UpdateBuildingSub();
            _actionBtn.RegisterCallback<MouseUpEvent>(_ => OnBuildingAction());
            _actionBtn.RegisterCallback<PointerUpEvent>(_ => OnBuildingAction());

            GameManager.Instance.ResourceSystem.OnGlobalReset += OnGlobalReset;

            // Subscribe to production events
            GameManager.Instance.ProductionSystem.OnProductionProgress += OnProductionProgress;
            GameManager.Instance.ProductionSystem.OnProductionComplete  += OnProductionComplete;

            RefreshActionBtn();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Common Init
        // ═══════════════════════════════════════════════════════════════════════

        private void InitCommon(VisualElement root, string icon, string title)
        {
            _workerData = GameManager.Instance.DataLoader.Workers.TryGetValue(_workerId, out var wd)
                          ? wd : null;

            _gameRow          = root.Q<VisualElement>("GameRow");
            _rowSub           = root.Q<Label>("RowSub");
            _actionBtn        = root.Q<Button>("ActionBtn");
            _prodBarBg        = root.Q<VisualElement>("ProdBarBg");
            _prodBarFill      = root.Q<VisualElement>("ProdBarFill");
            _tickFill         = root.Q<VisualElement>("TickFill");
            _workerLevelLabel = root.Q<Label>("WorkerLevel");
            _workerBtn        = root.Q<Button>("WorkerBtn");

            root.Q<Label>("RowIcon").text = icon;
            root.Q<Label>("RowTitle").text = title.ToUpper();

            if (_workerData != null)
            {
                root.Q<Label>("WorkerIcon").text = _workerData.icon;
                root.Q<Label>("WorkerName").text = _workerData.displayName;
            }

            // Worker button
            _workerBtn.RegisterCallback<MouseUpEvent>(_ => OnWorkerBtnClick());
            _workerBtn.RegisterCallback<PointerUpEvent>(_ => OnWorkerBtnClick());

            // Subscribe to worker state changes
            GameManager.Instance.WorkerSystem.OnWorkerChanged += OnWorkerChanged;

            RefreshWorkerUI();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Unity Update — tick bar
        // ═══════════════════════════════════════════════════════════════════════

        private void Update()
        {
            if (_tickFill == null || _workerId == null) return;
            float p = GameManager.Instance.WorkerSystem.GetTickProgress(_workerId);
            _tickFill.style.width = Length.Percent(p * 100f);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Resource Node — Tap
        // ═══════════════════════════════════════════════════════════════════════

        private void TryTap()
        {
            if (_isCooldown) return;
            StartCoroutine(TapRoutine());
        }

        private IEnumerator TapRoutine()
        {
            _isCooldown = true;
            _actionBtn.SetEnabled(false);

            // Fill animation: green bg, default border
            _gameRow.style.backgroundColor = _colCollect;
            SetBorder(_bdrDefault);
            yield return new WaitForSeconds(0.1f);

            // Complete: green border flash
            SetBorder(_bdrComplete);
            GameManager.Instance.ResourceSystem.AddResource(_resourceId, 1f);

            yield return new WaitForSeconds(0.06f);

            // Reset
            _gameRow.style.backgroundColor = _colDefault;
            SetBorder(_bdrDefault);
            _actionBtn.SetEnabled(true);
            _isCooldown = false;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Building — Unlock & Production
        // ═══════════════════════════════════════════════════════════════════════

        private void OnBuildingAction()
        {
            if (!_isUnlocked)
            {
                _isUnlocked = true;
                RefreshActionBtn();
                UpdateBuildingSub();
                return;
            }

            TryStartProduction();
        }

        private void TryStartProduction()
        {
            if (GameManager.Instance.ProductionSystem.IsProducing(_buildingId)) return;

            var rs = GameManager.Instance.ResourceSystem;
            foreach (var kv in _buildingData.input)
            {
                if (rs.GetResource(kv.Key) < kv.Value)
                {
                    Debug.LogWarning($"[GameRow] Not enough {kv.Key} for {_buildingId}.");
                    return;
                }
            }

            foreach (var kv in _buildingData.input)
                rs.SpendResource(kv.Key, kv.Value);

            var firstOut = _buildingData.output.First();
            GameManager.Instance.ProductionSystem.StartProduction(
                _buildingId, firstOut.Key, firstOut.Value, _buildingData.duration);

            _actionBtn.SetEnabled(false);
            _actionBtn.text = "RUNNING…";
        }

        private void OnProductionProgress(string buildingId, float progress)
        {
            if (buildingId != _buildingId) return;
            if (_prodBarFill != null)
                _prodBarFill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
        }

        private void OnProductionComplete(string buildingId, string resourceId, float amount)
        {
            if (buildingId != _buildingId) return;
            StartCoroutine(ClearProdBar());
            RefreshActionBtn();
        }

        private IEnumerator ClearProdBar()
        {
            yield return new WaitForSeconds(0.4f);
            if (_prodBarFill != null)
                _prodBarFill.style.width = Length.Percent(0f);
        }

        private void RefreshActionBtn()
        {
            if (!_isUnlocked)
            {
                _actionBtn.text = "🔒 UNLOCK";
                _actionBtn.SetEnabled(true);
                _gameRow.AddToClassList("game-row--locked");
            }
            else
            {
                _actionBtn.text = "▶ START";
                _actionBtn.SetEnabled(true);
                _gameRow.RemoveFromClassList("game-row--locked");
            }
        }

        private void UpdateBuildingSub()
        {
            if (_buildingData == null) return;
            var ins  = string.Join(" + ", _buildingData.input.Select(kv  => $"{kv.Value}x {Cap(kv.Key)}"));
            var outs = string.Join(" + ", _buildingData.output.Select(kv => $"{kv.Value}x {Cap(kv.Key)}"));
            _rowSub.text = $"{ins} → {outs}";
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Worker
        // ═══════════════════════════════════════════════════════════════════════

        private void OnWorkerBtnClick()
        {
            if (_workerId == null) return;
            var ws = GameManager.Instance.WorkerSystem;
            if (!ws.IsHired(_workerId))
                ws.HireWorker(_workerId);
            else
                ws.UpgradeWorker(_workerId);
        }

        private void OnWorkerChanged(string wId, int level)
        {
            if (wId == _workerId) RefreshWorkerUI();
        }

        private void RefreshWorkerUI()
        {
            if (_workerId == null) return;
            var ws    = GameManager.Instance.WorkerSystem;
            bool hired = ws.IsHired(_workerId);
            int level  = ws.GetLevel(_workerId);

            _workerLevelLabel.text = hired ? $"Lv{level}" : "Not Hired";
            _workerBtn.text        = hired ? $"▲ Lv{level + 1}" : "HIRE";

            if (hired)
                _workerBtn.AddToClassList("game-row-worker-btn--hired");
            else
                _workerBtn.RemoveFromClassList("game-row-worker-btn--hired");
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════════════════════════════════

        private void SetBorder(Color c)
        {
            _gameRow.style.borderTopColor    = c;
            _gameRow.style.borderRightColor  = c;
            _gameRow.style.borderBottomColor = c;
            _gameRow.style.borderLeftColor   = c;
        }

        private void OnGlobalReset()
        {
            if (_type == RowType.Building)
            {
                _isUnlocked = false;
                RefreshActionBtn();
                UpdateBuildingSub();
            }
            RefreshWorkerUI();
        }

        private static string Cap(string s) =>
            string.IsNullOrEmpty(s) ? s
            : char.ToUpper(s[0]) + s.Substring(1).Replace("_", " ");

        private void OnDestroy()
        {
            if (GameManager.Instance?.WorkerSystem != null)
                GameManager.Instance.WorkerSystem.OnWorkerChanged -= OnWorkerChanged;
            if (GameManager.Instance?.ResourceSystem != null)
            {
                //GameManager.Instance.ResourceSystem.OnResourceChanged -= OnResourceChanged;
                GameManager.Instance.ResourceSystem.OnGlobalReset    -= OnGlobalReset;
                GameManager.Instance.ProductionSystem.OnProductionProgress -= OnProductionProgress;
                GameManager.Instance.ProductionSystem.OnProductionComplete  -= OnProductionComplete;
            }
        }
    }
}
