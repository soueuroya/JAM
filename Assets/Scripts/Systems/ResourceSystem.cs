using System;
using System.Collections.Generic;
using UnityEngine;

namespace JAM.Systems
{
    public class ResourceSystem : MonoBehaviour
    {
        private Dictionary<string, float> _resources = new Dictionary<string, float>();

        public event Action<string, float> OnResourceChanged;

        public void AddResource(string id, float amount)
        {
            if (string.IsNullOrEmpty(id)) return;

            if (!_resources.ContainsKey(id))
            {
                _resources[id] = 0;
            }

            _resources[id] += amount;
            Debug.Log($"[ResourceSystem] Added {amount} to {id}. Current: {_resources[id]}");
            OnResourceChanged?.Invoke(id, _resources[id]);
        }

        public bool SpendResource(string id, float amount)
        {
            if (string.IsNullOrEmpty(id) || !_resources.ContainsKey(id)) return false;

            if (_resources[id] >= amount)
            {
                _resources[id] -= amount;
                Debug.Log($"[ResourceSystem] Spent {amount} of {id}. Current: {_resources[id]}");
                OnResourceChanged?.Invoke(id, _resources[id]);
                return true;
            }

            Debug.LogWarning($"[ResourceSystem] Insufficient {id} to spend {amount}. Current: {_resources[id]}");
            return false;
        }

        public float GetResource(string id)
        {
            if (string.IsNullOrEmpty(id) || !_resources.ContainsKey(id)) return 0;
            return _resources[id];
        }

        public void ResetAllResources()
        {
            var keys = new System.Collections.Generic.List<string>(_resources.Keys);
            foreach (var key in keys)
            {
                _resources[key] = 0;
                OnResourceChanged?.Invoke(key, 0);
            }
            Debug.Log("[ResourceSystem] All resources reset to 0.");
        }

        public Dictionary<string, float> GetAllResources()
        {
            return new Dictionary<string, float>(_resources);
        }
    }
}
