using System;
using System.Collections.Generic;

namespace JAM.Data
{
    [Serializable]
    public class ResourceData
    {
        public string id;
        public string displayName;
        public string category;
    }

    [Serializable]
    public class ResourceList
    {
        public List<ResourceData> resources;
    }

    [Serializable]
    public class BuildingData
    {
        public string id;
        public Dictionary<string, float> input;
        public Dictionary<string, float> output;
        public float duration;
        public string type;
    }

    [Serializable]
    public class BuildingList
    {
        public List<BuildingData> buildings;
    }

    [Serializable]
    public class WorkerData
    {
        public string id;
        public string displayName;
        public string icon;
        public string resourceId;   // resource generated directly
        public float  baseRate;     // amount per tick at level 1
        public float  scalingFactor;// multiplier per level
        public float  interval;     // seconds between ticks
    }

    [Serializable]
    public class WorkerList
    {
        public List<WorkerData> workers;
    }
}
