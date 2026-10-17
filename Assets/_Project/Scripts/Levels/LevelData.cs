using System;

namespace MonsoonRooftops.Levels
{
    // JsonUtility schema. Mirrors section 4.4 of the plan; availableParts is a list
    // because JsonUtility cannot read dictionaries.
    [Serializable]
    public class LevelData
    {
        public string id;
        public int world;
        public string name;
        public GridInfo grid;
        public CellData[] cells;
        public FixedObjectData[] fixedObjects;
        public PartCountData[] availableParts;
        public WaveData[] waves;
        public NeighbourRequestData[] neighbourRequests;
        public GoalsData goals;
        public int sandbags;
        public PlacedPartData[] solution;   // reference solution: proves the level can be solved (see LevelSolvabilityTests)
        public TutorialStepData[] tutorial;
    }

    [Serializable]
    public class GridInfo
    {
        public int width;
        public int depth;
        public int maxHeight;
    }

    [Serializable]
    public class CellData
    {
        public int x;
        public int z;
        public int h;
        public string surface;
    }

    [Serializable]
    public class FixedObjectData
    {
        public string type;
        public int x;
        public int z;
        public int h;
        public int capacityL;
    }

    [Serializable]
    public class PartCountData
    {
        public string type;
        public int count;
    }

    [Serializable]
    public class WaveData
    {
        public float durationSec;
        public float intensity;
        public string label;
        public string[] hazards;
    }

    [Serializable]
    public class NeighbourRequestData
    {
        public string id;
        public int[] cell;
        public string type;
        public int rewardCoins;
    }

    [Serializable]
    public class PlacedPartData
    {
        public string type;
        public int x;
        public int z;
        public int rot;
    }

    // point: "none", "cell", "tray:<part key>", "button:start" or "button:drizzle".
    // advance: the player action that moves to the next step: "arm", "place", "rotate", "drizzle", "start", "clear".
    [Serializable]
    public class TutorialStepData
    {
        public string text;
        public string point;
        public int[] cell;
        public string advance;
    }

    [Serializable]
    public class GoalsData
    {
        public int litresTarget;
        public int star2;
        public int star3;
        public int maxFloodedCells;
    }
}
