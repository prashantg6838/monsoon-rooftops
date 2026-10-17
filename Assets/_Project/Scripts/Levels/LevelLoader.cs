using System;
using UnityEngine;

namespace MonsoonRooftops.Levels
{
    public static class LevelLoader
    {
        public static LevelData Parse(string json)
        {
            var level = JsonUtility.FromJson<LevelData>(json);
            if (level == null || level.grid == null || level.cells == null)
                throw new FormatException("Level JSON is missing 'grid' or 'cells'.");
            level.fixedObjects ??= Array.Empty<FixedObjectData>();
            level.availableParts ??= Array.Empty<PartCountData>();
            level.waves ??= Array.Empty<WaveData>();
            level.neighbourRequests ??= Array.Empty<NeighbourRequestData>();
            level.solution ??= Array.Empty<PlacedPartData>();
            level.tutorial ??= Array.Empty<TutorialStepData>();
            return level;
        }

        // Levels live in Assets/_Project/Resources/Levels so they can be edited as plain text.
        public static LevelData LoadResource(string name)
        {
            var asset = Resources.Load<TextAsset>("Levels/" + name);
            if (asset == null) throw new FormatException($"Level resource 'Levels/{name}' not found.");
            return Parse(asset.text);
        }
    }
}
