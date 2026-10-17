using System.Collections.Generic;
using MonsoonRooftops.Grid;

namespace MonsoonRooftops.Parts
{
    public class PartDefinition
    {
        public PartType Type { get; }
        public string Key { get; }
        public string DisplayName { get; }
        public Direction[] BasePorts { get; }
        public bool IsVertical { get; }

        public PartDefinition(PartType type, string key, string displayName, bool isVertical, params Direction[] basePorts)
        {
            Type = type;
            Key = key;
            DisplayName = displayName;
            IsVertical = isVertical;
            BasePorts = basePorts;
        }
    }

    // Phase 1 keeps part data in code. The plan moves it to ScriptableObjects once economy/upgrades need tuning.
    public static class PartCatalog
    {
        private static readonly Dictionary<PartType, PartDefinition> ByType = new Dictionary<PartType, PartDefinition>
        {
            { PartType.GutterStraight, new PartDefinition(PartType.GutterStraight, "gutter_straight", "Straight Gutter", false, Direction.North, Direction.South) },
            { PartType.Elbow90, new PartDefinition(PartType.Elbow90, "elbow_90", "Elbow Gutter", false, Direction.North, Direction.East) },
            { PartType.Downpipe, new PartDefinition(PartType.Downpipe, "downpipe", "Downpipe", true, Direction.North, Direction.South) },
            { PartType.TJunction, new PartDefinition(PartType.TJunction, "t_junction", "T-Junction", false, Direction.East, Direction.South, Direction.West) },
            { PartType.Tarp, new PartDefinition(PartType.Tarp, "tarp", "Tarp", false, Direction.South) },
            { PartType.Valve, new PartDefinition(PartType.Valve, "valve", "Valve", false, Direction.East, Direction.South, Direction.West) }
        };

        public static IEnumerable<PartDefinition> All => ByType.Values;

        public static PartDefinition Get(PartType type) => ByType[type];

        public static bool TryParse(string key, out PartType type)
        {
            foreach (var def in ByType.Values)
            {
                if (def.Key == key)
                {
                    type = def.Type;
                    return true;
                }
            }
            type = default;
            return false;
        }
    }
}
