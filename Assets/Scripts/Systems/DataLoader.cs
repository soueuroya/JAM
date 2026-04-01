using System;
using System.Collections.Generic;
using UnityEngine;
using JAM.Data;
using Newtonsoft.Json;

namespace JAM.Systems
{
    public class DataLoader : MonoBehaviour
    {
        private Dictionary<string, ResourceData> _resources = new Dictionary<string, ResourceData>();
        private Dictionary<string, BuildingData> _buildings = new Dictionary<string, BuildingData>();
        private Dictionary<string, WorkerData> _workers = new Dictionary<string, WorkerData>();

        public Dictionary<string, ResourceData> Resources => _resources;
        public Dictionary<string, BuildingData> Buildings => _buildings;
        public Dictionary<string, WorkerData> Workers => _workers;

        public void LoadAllData()
        {
            LoadResources();
            LoadBuildings();
            LoadWorkers();
            Debug.Log("[DataLoader] All game data successfully loaded from JSON.");
        }

        private void LoadResources()
        {
            TextAsset jsonFile = UnityEngine.Resources.Load<TextAsset>("Data/ResourceData");
            if (jsonFile != null)
            {
                ResourceList list = JsonConvert.DeserializeObject<ResourceList>(jsonFile.text);
                foreach (var item in list.resources)
                {
                    _resources[item.id] = item;
                }
                Debug.Log($"[DataLoader] Loaded {_resources.Count} Resources");
            }
        }

        private void LoadBuildings()
        {
            TextAsset jsonFile = UnityEngine.Resources.Load<TextAsset>("Data/BuildingData");
            if (jsonFile != null)
            {
                BuildingList list = JsonConvert.DeserializeObject<BuildingList>(jsonFile.text);
                foreach (var item in list.buildings)
                {
                    _buildings[item.id] = item;
                }
                Debug.Log($"[DataLoader] Loaded {_buildings.Count} Buildings");
            }
        }

        private void LoadWorkers()
        {
            TextAsset jsonFile = UnityEngine.Resources.Load<TextAsset>("Data/WorkerData");
            if (jsonFile != null)
            {
                WorkerList list = JsonConvert.DeserializeObject<WorkerList>(jsonFile.text);
                foreach (var item in list.workers)
                {
                    _workers[item.id] = item;
                }
                Debug.Log($"[DataLoader] Loaded {_workers.Count} Workers");
            }
        }
    }
}
