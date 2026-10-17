using System.Collections.Generic;
using MonsoonRooftops.Levels;

namespace MonsoonRooftops.Grid
{
    public class GridSystem
    {
        private readonly Dictionary<(int, int), CellData> _cells = new Dictionary<(int, int), CellData>();
        private readonly Dictionary<(int, int), FixedObjectData> _fixed = new Dictionary<(int, int), FixedObjectData>();

        public int Width { get; }
        public int Depth { get; }
        public int MaxHeight { get; }
        public IEnumerable<CellData> Cells => _cells.Values;
        public IEnumerable<FixedObjectData> FixedObjects => _fixed.Values;

        public GridSystem(LevelData level)
        {
            Width = level.grid.width;
            Depth = level.grid.depth;
            MaxHeight = level.grid.maxHeight;
            foreach (var c in level.cells) _cells[(c.x, c.z)] = c;
            foreach (var f in level.fixedObjects) _fixed[(f.x, f.z)] = f;
        }

        public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Depth;

        public bool TryGetCell(int x, int z, out CellData cell) => _cells.TryGetValue((x, z), out cell);

        public bool TryGetFixedObject(int x, int z, out FixedObjectData obj) => _fixed.TryGetValue((x, z), out obj);

        // Roof tier of a cell, or -1 when the cell does not exist.
        public int HeightAt(int x, int z) => _cells.TryGetValue((x, z), out var c) ? c.h : -1;

        public bool HasTank(int x, int z) => _fixed.TryGetValue((x, z), out var o) && o.type == "tank";
    }
}
