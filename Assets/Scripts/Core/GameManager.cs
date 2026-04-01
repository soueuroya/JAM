using UnityEngine;
using JAM.Systems;

namespace JAM.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Systems")]
        [SerializeField] private ResourceSystem _resourceSystem;
        [SerializeField] private ProductionSystem _productionSystem;
        [SerializeField] private WorkerSystem _workerSystem;
        [SerializeField] private DataLoader _dataLoader;
        [SerializeField] private SupabaseService _supabaseService;

        public ResourceSystem ResourceSystem => _resourceSystem;
        public ProductionSystem ProductionSystem => _productionSystem;
        public WorkerSystem WorkerSystem => _workerSystem;
        public DataLoader DataLoader => _dataLoader;
        public SupabaseService SupabaseService => _supabaseService;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeSystems();
        }

        private void Start()
        {
            // Start simulation
            SimulateBootstrap();
            
            // Sync initial data from cloud
            if (_supabaseService != null)
            {
                _supabaseService.LoadPlayerData();
            }
        }

        private void InitializeSystems()
        {
            if (_resourceSystem == null) _resourceSystem = GetComponentInChildren<ResourceSystem>();
            if (_productionSystem == null) _productionSystem = GetComponentInChildren<ProductionSystem>();
            if (_workerSystem == null) _workerSystem = GetComponentInChildren<WorkerSystem>();
            if (_dataLoader == null) _dataLoader = GetComponentInChildren<DataLoader>();
            if (_supabaseService == null) _supabaseService = GetComponentInChildren<SupabaseService>();

            // Load data first
            if (_dataLoader != null)
            {
                _dataLoader.LoadAllData();
            }

            // Initialize WorkerSystem with loaded data
            if (_workerSystem != null && _dataLoader != null)
            {
                _workerSystem.Initialize(_dataLoader.Workers);
            }

            // Hook up events
            if (_productionSystem != null && _resourceSystem != null)
            {
                _productionSystem.OnProductionComplete += (buildingId, resourceId, amount) =>
                {
                    float multiplier = _workerSystem != null ? _workerSystem.GetMultiplier(buildingId) : 1f;
                    float finalAmount = amount * multiplier;
                    
                    Debug.Log($"[GameManager] Production Complete: {resourceId} x{finalAmount} (from {buildingId})");
                    _resourceSystem.AddResource(resourceId, finalAmount);
                };
            }

            Debug.Log("[GameManager] Core Systems Initialized");
        }

        private void SimulateBootstrap()
        {
            Debug.Log("[GameManager] Starting Bootstrap Simulation...");

            // Provide starting resources for the player to test the production panels
            _resourceSystem.AddResource("logs", 20);
            _resourceSystem.AddResource("iron_ore", 10);

            Debug.Log("[GameManager] Starting resources granted. Use the UI panels to produce planks and iron bars!");
        }

        private void Update()
        {
            if (_productionSystem != null)
            {
                _productionSystem.UpdateSystem(Time.deltaTime);
            }
        }
    }
}
