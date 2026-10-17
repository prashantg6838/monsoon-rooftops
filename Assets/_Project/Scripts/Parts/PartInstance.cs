using System.Collections.Generic;
using MonsoonRooftops.Grid;

namespace MonsoonRooftops.Parts
{
    public class PartInstance
    {
        public int Id { get; }
        public PartType Type { get; }
        public int X { get; internal set; }
        public int Z { get; internal set; }
        public int Rotation { get; internal set; }

        public PartInstance(int id, PartType type, int x, int z, int rotation)
        {
            Id = id;
            Type = type;
            X = x;
            Z = z;
            Rotation = rotation;
        }

        public IEnumerable<Direction> GetPorts()
        {
            foreach (var p in PartCatalog.Get(Type).BasePorts)
                yield return p.Rotate(Rotation);
        }

        // A downpipe takes water in at its North port (same tier) and drops it out of its South port (one tier down).
        public bool IsOutlet(Direction port) => Type == PartType.Downpipe && port == Direction.South.Rotate(Rotation);

        // A valve takes water in at its South port and sends it all out of one side, switched by tapping.
        public Direction ValveInlet => Direction.South.Rotate(Rotation);

        public bool HasPort(Direction d)
        {
            foreach (var p in GetPorts())
                if (p == d) return true;
            return false;
        }
    }
}
