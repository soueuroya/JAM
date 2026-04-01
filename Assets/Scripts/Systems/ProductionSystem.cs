using System;
using System.Collections.Generic;
using UnityEngine;

namespace JAM.Systems
{
    [Serializable]
    public class ProductionJob
    {
        public string BuildingId;
        public string ResourceId;
        public float Amount;
        public float Duration;
        public float RemainingTime;
        public bool IsComplete;

        public ProductionJob(string buildingId, string resourceId, float amount, float duration)
        {
            BuildingId = buildingId;
            ResourceId = resourceId;
            Amount = amount;
            Duration = duration;
            RemainingTime = duration;
            IsComplete = false;
        }
    }

    public class ProductionSystem : MonoBehaviour
    {
        private List<ProductionJob> _activeProductions = new List<ProductionJob>();

        public event Action<string, string, float> OnProductionComplete;
        public event Action<string, float> OnProductionProgress;

        public void StartProduction(string buildingId, string resourceId, float amount, float duration)
        {
            ProductionJob job = new ProductionJob(buildingId, resourceId, amount, duration);
            _activeProductions.Add(job);
            Debug.Log($"[ProductionSystem] Started production for {resourceId} at {buildingId} (Duration: {duration}s)");
        }

        public bool IsProducing(string buildingId)
        {
            return _activeProductions.Exists(j => j.BuildingId == buildingId && !j.IsComplete);
        }

        public void UpdateSystem(float deltaTime)
        {
            for (int i = _activeProductions.Count - 1; i >= 0; i--)
            {
                var job = _activeProductions[i];
                job.RemainingTime -= deltaTime;

                float progress = 1f - Mathf.Clamp01(job.RemainingTime / job.Duration);
                OnProductionProgress?.Invoke(job.BuildingId, progress);

                if (job.RemainingTime <= 0)
                {
                    job.IsComplete = true;
                    CompleteProduction(i);
                }
            }
        }

        private void CompleteProduction(int index)
        {
            var job = _activeProductions[index];
            _activeProductions.RemoveAt(index);
            
            Debug.Log($"[ProductionSystem] Completed production for {job.ResourceId} at {job.BuildingId}");
            OnProductionComplete?.Invoke(job.BuildingId, job.ResourceId, job.Amount);
        }
    }
}
