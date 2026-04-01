using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using JAM.Data;
using JAM.Core;

namespace JAM.Systems
{
    public class SupabaseService : MonoBehaviour
    {
        public static SupabaseService Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private string _supabaseUrl = "https://riytpkfautiwvpoltnyv.supabase.co";
        [SerializeField] private string _supabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InJpeXRwa2ZhdXRpd3Zwb2x0bnl2Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzQ5OTQ2NjQsImV4cCI6MjA5MDU3MDY2NH0.jL6nJg4M3kUACeceSvifxGX8vInNgOMVTRQablD6Bnk";
        
        [Header("Sync Settings")]
        [SerializeField] private float _syncInterval = 30f;
        
        private string _currentPlayerId = "00000000-0000-0000-0000-000000000001"; // Default test player
        private float _lastSyncTime;
        private bool _isSyncing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _lastSyncTime = Time.time;
        }

        private void Update()
        {
            if (Time.time - _lastSyncTime >= _syncInterval)
            {
                SavePlayerData();
                _lastSyncTime = Time.time;
            }
        }

        #region Public API

        public void LoadPlayerData()
        {
            StartCoroutine(LoadPlayerRoutine(_currentPlayerId));
        }

        public void SavePlayerData()
        {
            if (_isSyncing) return;
            StartCoroutine(SavePlayerRoutine(_currentPlayerId));
        }

        public void UpdateLeaderboard(int prestigeCount)
        {
            StartCoroutine(UpdateLeaderboardRoutine(_currentPlayerId, prestigeCount));
        }

        #endregion

        #region Routines

        private IEnumerator LoadPlayerRoutine(string playerId)
        {
            Debug.Log("[SupabaseService] Loading player data...");
            
            // 1. Fetch Players Table (Main Profile)
            string url = $"{_supabaseUrl}/rest/v1/players?id=eq.{playerId}&select=*";
            using (UnityWebRequest request = CreateRequest(url, "GET"))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("[SupabaseService] Player Profile Loaded.");
                    // Process profile to update prestige count, etc.
                }
                else
                {
                    Debug.LogError($"[SupabaseService] Load Profile Failed: {request.error}");
                }
            }

            // 2. Fetch Resources
            url = $"{_supabaseUrl}/rest/v1/resources?player_id=eq.{playerId}&select=*";
            using (UnityWebRequest request = CreateRequest(url, "GET"))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var resources = JsonConvert.DeserializeObject<List<ResourceSyncData>>(request.downloadHandler.text);
                    foreach (var res in resources)
                    {
                        GameManager.Instance.ResourceSystem.AddResource(res.resource_id, res.amount);
                    }
                    Debug.Log($"[SupabaseService] Loaded {resources.Count} resources.");
                }
            }
        }

        private IEnumerator SavePlayerRoutine(string playerId)
        {
            _isSyncing = true;
            Debug.Log("[SupabaseService] Syncing all local resources to cloud...");

            // 1. Gather Resource Data
            List<ResourceSyncData> payload = new List<ResourceSyncData>();
            var localResources = GameManager.Instance.ResourceSystem.GetAllResources();
            
            foreach (var kvp in localResources)
            {
                payload.Add(new ResourceSyncData { 
                    player_id = playerId, 
                    resource_id = kvp.Key, 
                    amount = kvp.Value 
                });
            }

            if (payload.Count == 0)
            {
                _isSyncing = false;
                yield break;
            }

            string json = JsonConvert.SerializeObject(payload);
            string url = $"{_supabaseUrl}/rest/v1/resources";

            using (UnityWebRequest request = CreateRequest(url, "POST", json))
            {
                // Prefer: resolution=merge-duplicates ensures an upsert behavior for the PK
                request.SetRequestHeader("Prefer", "resolution=merge-duplicates");
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                    Debug.Log($"[SupabaseService] Sync Success: {payload.Count} resources saved.");
                else
                    Debug.LogWarning($"[SupabaseService] Sync failed (Offline?): {request.error}");
            }

            _isSyncing = false;
        }

        private IEnumerator UpdateLeaderboardRoutine(string playerId, int prestigeCount)
        {
            Debug.Log("[SupabaseService] Updating leaderboard...");
            
            var payload = new { p_player_id = playerId, p_prestige_count = prestigeCount };
            string json = JsonConvert.SerializeObject(payload);
            string url = $"{_supabaseUrl}/rest/v1/rpc/update_leaderboard";

            using (UnityWebRequest request = CreateRequest(url, "POST", json))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                    Debug.Log("[SupabaseService] Leaderboard updated.");
                else
                    Debug.LogError($"[SupabaseService] RPC update_leaderboard failed: {request.error}");
            }
        }

        #endregion

        #region Helpers

        private UnityWebRequest CreateRequest(string url, string method, string json = null)
        {
            UnityWebRequest request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            
            if (json != null)
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", _supabaseKey);
            request.SetRequestHeader("Authorization", $"Bearer {_supabaseKey}");
            
            return request;
        }

        #endregion
        
        [Serializable]
        public class ResourceSyncData
        {
            public string player_id;
            public string resource_id;
            public float amount;
        }
    }
}
