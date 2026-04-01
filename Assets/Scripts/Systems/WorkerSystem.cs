using System;
using System.Collections.Generic;
using UnityEngine;
using JAM.Core;
using JAM.Data;

namespace JAM.Systems
{
    public class WorkerSystem : MonoBehaviour
    {
        // Fired when a worker is hired or upgraded: (workerId, newLevel)
        public event Action<string, int> OnWorkerChanged;

        private class WorkerState
        {
            public int   Level;          // 0 = not hired
            public float Timer;          // countdown to next auto-tick
            public bool  IsHired => Level > 0;
        }

        private Dictionary<string, WorkerData>  _data   = new();
        private Dictionary<string, WorkerState> _states = new();

        // ── Initialization ────────────────────────────────────────────────────

        public void Initialize(Dictionary<string, WorkerData> workerData)
        {
            _data = workerData;
            foreach (var kv in workerData)
            {
                _states[kv.Key] = new WorkerState { Level = 0, Timer = kv.Value.interval };
            }
            Debug.Log($"[WorkerSystem] Initialized with {_data.Count} workers.");
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void HireWorker(string workerId)
        {
            if (!_states.TryGetValue(workerId, out var state)) return;
            if (state.IsHired) return;

            state.Level = 1;
            if (_data.TryGetValue(workerId, out var data))
                state.Timer = data.interval;

            Debug.Log($"[WorkerSystem] Hired {workerId} at level 1.");
            OnWorkerChanged?.Invoke(workerId, state.Level);
        }

        public void UpgradeWorker(string workerId)
        {
            if (!_states.TryGetValue(workerId, out var state)) return;
            if (!state.IsHired) return;

            state.Level++;
            Debug.Log($"[WorkerSystem] Upgraded {workerId} to level {state.Level}.");
            OnWorkerChanged?.Invoke(workerId, state.Level);
        }

        public int GetLevel(string workerId) =>
            _states.TryGetValue(workerId, out var s) ? s.Level : 0;

        public bool IsHired(string workerId) =>
            _states.TryGetValue(workerId, out var s) && s.IsHired;

        /// <summary>Returns interval progress 0→1 for the current tick cycle.</summary>
        public float GetTickProgress(string workerId)
        {
            if (!_states.TryGetValue(workerId, out var state) || !state.IsHired) return 0;
            if (!_data.TryGetValue(workerId, out var data)) return 0;
            float interval = data.interval / Mathf.Pow(data.scalingFactor, state.Level - 1);
            return 1f - Mathf.Clamp01(state.Timer / interval);
        }

        /// <summary>Legacy multiplier kept for backward compatibility with ProductionSystem.</summary>
        public float GetMultiplier(string targetId) => 1.0f;

        // ── Auto-tick ─────────────────────────────────────────────────────────

        private void Update()
        {
            foreach (var kv in _states)
            {
                var state = kv.Value;
                if (!state.IsHired) continue;

                if (!_data.TryGetValue(kv.Key, out var data)) continue;

                // Speed scales with level: each level reduces interval by scalingFactor
                float interval = data.interval / Mathf.Pow(data.scalingFactor, state.Level - 1);
                state.Timer -= Time.deltaTime;

                if (state.Timer <= 0)
                {
                    state.Timer = interval;
                    float amount = data.baseRate * state.Level;
                    GameManager.Instance.ResourceSystem.AddResource(data.resourceId, amount);
                    Debug.Log($"[WorkerSystem] {data.displayName} (Lv{state.Level}) generated {amount} {data.resourceId}");
                }
            }
        }
    }
}
