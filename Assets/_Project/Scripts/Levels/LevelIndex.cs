using System;
using System.Collections.Generic;
using MonsoonRooftops.Save;
using UnityEngine;

namespace MonsoonRooftops.Levels
{
    [Serializable]
    public class WorldEntry
    {
        public string name;
        public string[] levels;
    }

    [Serializable]
    public class LevelIndexData
    {
        public WorldEntry[] worlds;
        public string[] testLevels;
    }

    // Resources/Levels/index.json: which levels ship, in which world and order.
    public class LevelIndex
    {
        public LevelIndexData Data { get; private set; }
        public List<string> Ordered { get; } = new List<string>();

        public static LevelIndex Load()
        {
            var asset = Resources.Load<TextAsset>("Levels/index");
            if (asset == null) throw new FormatException("Resources/Levels/index.json not found.");
            var index = new LevelIndex { Data = JsonUtility.FromJson<LevelIndexData>(asset.text) };
            index.Data.testLevels ??= Array.Empty<string>();
            foreach (var world in index.Data.worlds)
                index.Ordered.AddRange(world.levels);
            return index;
        }

        public int Number(string id) => Ordered.IndexOf(id) + 1;

        public string NextAfter(string id)
        {
            int i = Ordered.IndexOf(id);
            return i >= 0 && i + 1 < Ordered.Count ? Ordered[i + 1] : null;
        }

        // A level opens once the one before it has at least one star. Test levels are always open.
        public bool IsUnlocked(string id)
        {
            int i = Ordered.IndexOf(id);
            if (i <= 0) return true;
            return ProgressStore.GetStars(Ordered[i - 1]) >= 1;
        }
    }
}
