using System;
using System.Collections.Generic;
using MonsoonRooftops.Grid;

namespace MonsoonRooftops.Parts
{
    // Pure C# placement rules: no Unity scene dependency, so it is unit-testable.
    public class PlacementModel
    {
        public const int MaxUndo = 10;

        private readonly GridSystem _grid;
        private readonly Dictionary<PartType, int> _limits;
        private readonly List<PartInstance> _parts = new List<PartInstance>();
        private readonly List<Action> _undo = new List<Action>();
        private int _nextId = 1;

        public IReadOnlyList<PartInstance> Parts => _parts;
        public int UndoCount => _undo.Count;
        public event Action Changed;

        public PlacementModel(GridSystem grid, Dictionary<PartType, int> limits)
        {
            _grid = grid;
            _limits = limits;
        }

        public IEnumerable<PartType> AvailableTypes => _limits.Keys;

        public int Remaining(PartType type)
        {
            _limits.TryGetValue(type, out var limit);
            var used = 0;
            foreach (var p in _parts)
                if (p.Type == type) used++;
            return limit - used;
        }

        public PartInstance PartAt(int x, int z)
        {
            foreach (var p in _parts)
                if (p.X == x && p.Z == z) return p;
            return null;
        }

        public bool CanPlace(PartType type, int x, int z, PartInstance ignore = null)
        {
            if (!_grid.InBounds(x, z)) return false;
            if (!_grid.TryGetCell(x, z, out _)) return false;
            if (_grid.TryGetFixedObject(x, z, out _)) return false;
            var existing = PartAt(x, z);
            if (existing != null && existing != ignore) return false;
            if (ignore == null && Remaining(type) <= 0) return false;
            return true;
        }

        public PartInstance Place(PartType type, int x, int z, int rotation = 0)
        {
            if (!CanPlace(type, x, z)) return null;
            var part = new PartInstance(_nextId++, type, x, z, ((rotation % 4) + 4) % 4);
            _parts.Add(part);
            PushUndo(() => _parts.Remove(part));
            RaiseChanged();
            return part;
        }

        public bool Remove(PartInstance part)
        {
            var index = _parts.IndexOf(part);
            if (index < 0) return false;
            _parts.RemoveAt(index);
            PushUndo(() => _parts.Insert(Math.Min(index, _parts.Count), part));
            RaiseChanged();
            return true;
        }

        public bool Rotate(PartInstance part)
        {
            if (!_parts.Contains(part)) return false;
            part.Rotation = (part.Rotation + 1) % 4;
            PushUndo(() => part.Rotation = (part.Rotation + 3) % 4);
            RaiseChanged();
            return true;
        }

        public bool Move(PartInstance part, int x, int z)
        {
            if (!_parts.Contains(part) || !CanPlace(part.Type, x, z, part)) return false;
            if (part.X == x && part.Z == z) return false;
            int oldX = part.X, oldZ = part.Z;
            part.X = x;
            part.Z = z;
            PushUndo(() => { part.X = oldX; part.Z = oldZ; });
            RaiseChanged();
            return true;
        }

        public bool Undo()
        {
            if (_undo.Count == 0) return false;
            var action = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            action();
            RaiseChanged();
            return true;
        }

        // A port is connected when the neighbour is a tank or a part with a port facing back, and the heights allow it:
        // same tier joins normally; different tiers join only through a downpipe outlet dropping onto the lower side.
        public bool IsPortConnected(PartInstance part, Direction port)
        {
            int nx = part.X + port.OffsetX();
            int nz = part.Z + port.OffsetZ();
            int ownHeight = _grid.HeightAt(part.X, part.Z);
            int otherHeight = _grid.HeightAt(nx, nz);
            if (otherHeight < 0) return false;

            bool otherIsOutlet = false;
            if (!_grid.HasTank(nx, nz))
            {
                var neighbour = PartAt(nx, nz);
                if (neighbour == null || !neighbour.HasPort(port.Opposite())) return false;
                otherIsOutlet = neighbour.IsOutlet(port.Opposite());
            }
            return HeightsCompatible(ownHeight, part.IsOutlet(port), otherHeight, otherIsOutlet);
        }

        private static bool HeightsCompatible(int heightA, bool aIsOutlet, int heightB, bool bIsOutlet)
        {
            if (heightA == heightB) return !aIsOutlet && !bIsOutlet;
            if (heightA > heightB) return aIsOutlet && !bIsOutlet;
            return bIsOutlet && !aIsOutlet;
        }

        private void PushUndo(Action undo)
        {
            _undo.Add(undo);
            if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}
