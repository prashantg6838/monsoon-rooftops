using System;
using System.Collections.Generic;
using MonsoonRooftops.Grid;
using MonsoonRooftops.Levels;
using MonsoonRooftops.Parts;

namespace MonsoonRooftops.Water
{
    [Flags]
    public enum Hazards
    {
        None = 0,
        Leaves = 1
    }

    public readonly struct CellState
    {
        public readonly int X, Z;
        public readonly float Water, Capacity;
        public readonly bool IsTank, Flooded, Clogged, Sandbagged, IsValve;
        public readonly Direction ValveOutlet;

        public CellState(int x, int z, float water, float capacity, bool isTank, bool flooded,
            bool clogged, bool sandbagged, bool isValve, Direction valveOutlet)
        {
            X = x; Z = z; Water = water; Capacity = capacity; IsTank = isTank; Flooded = flooded;
            Clogged = clogged; Sandbagged = sandbagged; IsValve = isValve; ValveOutlet = valveOutlet;
        }
    }

    public readonly struct SimResult
    {
        public readonly float Litres;
        public readonly int FloodedCells;
        public readonly float OverflowLitres;
        public readonly int Stars;

        public SimResult(float litres, int floodedCells, float overflowLitres, int stars)
        {
            Litres = litres; FloodedCells = floodedCells; OverflowLitres = overflowLitres; Stars = stars;
        }
    }

    // Deterministic grid flow simulation. Pure C#: no scene, no randomness, fixed 10 Hz tick.
    // Water flows from every part towards the nearest tank along connected ports (shortest path, ties split evenly).
    public class WaterSimulator
    {
        public const float TickSeconds = 0.1f;
        public const float RainLitresPerCellPerSec = 3f;
        public const float PartCapacityPerTick = 4f;
        public const float PartStorageLitres = 10f;
        public const int FloodTicks = 30;
        public const int DrainTicks = 100;
        public const int LeafFirstTicks = 50;     // first clog 5 s into a leaves wave
        public const int LeafIntervalTicks = 100; // then one every 10 s
        public const int TarpReach = 1;           // a tarp catches rain over the 3x3 cells around it
        public const float OneStarShare = 0.4f;  // relaxed score: 1 star for 40% of the target
        private const float Epsilon = 1e-4f;

        private class Node
        {
            public int X, Z;
            public PartInstance Part;
            public bool IsTank;
            public float Water, Capacity, Pending;
            public int Distance = -1;
            public int FullTicks;
            public bool Flooded, TankReported, Clogged, Sandbagged;
            public Direction ValveOutlet;
            public readonly List<(Direction port, Node node)> Exits = new List<(Direction, Node)>();
            public readonly List<Node> Next = new List<Node>();

            public bool IsValve => Part != null && Part.Type == PartType.Valve;
        }

        private readonly List<Node> _nodes = new List<Node>();
        private readonly Dictionary<(int, int), Node> _byCell = new Dictionary<(int, int), Node>();
        private readonly List<Node> _rainTargets = new List<Node>(); // null entries are runoff
        private int _leafTicks;

        public int Ticks { get; private set; }
        public float TotalRainLitres { get; private set; }
        public float OverflowLitres { get; private set; }
        public float RunoffLitres { get; private set; }
        public int EverFlooded { get; private set; }
        public int ClogCount { get; private set; }
        public int SandbagsLeft { get; private set; }

        public event Action<int, int> OnTankFilled;
        public event Action<int, int> OnCellFlooding;
        public event Action<int, int> OnCellDrained;
        public event Action<int, int> OnPartOverflow;
        public event Action<int, int> OnClogged;

        public float TankLitres
        {
            get { float sum = 0f; foreach (var n in _nodes) if (n.IsTank) sum += n.Water; return sum; }
        }

        public float StoredLitres
        {
            get { float sum = 0f; foreach (var n in _nodes) sum += n.Water; return sum; }
        }

        public int FloodedNow
        {
            get { int c = 0; foreach (var n in _nodes) if (n.Flooded) c++; return c; }
        }

        public WaterSimulator(GridSystem grid, PlacementModel model, int sandbags = 0)
        {
            SandbagsLeft = sandbags;

            foreach (var f in grid.FixedObjects)
            {
                if (f.type != "tank") continue;
                var tank = new Node { X = f.x, Z = f.z, IsTank = true, Capacity = f.capacityL };
                _byCell[(f.x, f.z)] = tank;
                _nodes.Add(tank);
            }
            foreach (var p in model.Parts)
            {
                var node = new Node { X = p.X, Z = p.Z, Part = p, Capacity = PartStorageLitres };
                if (p.Type == PartType.Valve) node.ValveOutlet = Direction.East.Rotate(p.Rotation);
                _byCell[(p.X, p.Z)] = node;
                _nodes.Add(node);
            }
            _nodes.Sort(CompareCells);

            BuildFlowGraph(model);
            BuildRainTargets(grid);
        }

        private static int CompareCells(Node a, Node b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z);

        // Ports that only let water out (downpipe outlet, valve sides) cannot be used to push water in.
        private static bool CanEnter(Node v, Direction portOnV)
        {
            if (v.Part == null) return true;
            if (v.Part.IsOutlet(portOnV)) return false;
            if (v.Part.Type == PartType.Valve && portOnV != v.Part.ValveInlet) return false;
            return true;
        }

        private static bool CanLeave(PartInstance part, Direction port)
        {
            if (part.Type == PartType.Downpipe) return part.IsOutlet(port);
            if (part.Type == PartType.Valve) return port != part.ValveInlet;
            return true;
        }

        private void BuildFlowGraph(PlacementModel model)
        {
            var incoming = new Dictionary<Node, List<Node>>();
            foreach (var u in _nodes)
            {
                if (u.Part == null) continue;
                foreach (var port in u.Part.GetPorts())
                {
                    if (!CanLeave(u.Part, port)) continue;
                    if (!model.IsPortConnected(u.Part, port)) continue;
                    if (!_byCell.TryGetValue((u.X + port.OffsetX(), u.Z + port.OffsetZ()), out var v)) continue;
                    if (!CanEnter(v, port.Opposite())) continue;

                    u.Exits.Add((port, v));
                    if (!incoming.TryGetValue(v, out var list)) incoming[v] = list = new List<Node>();
                    list.Add(u);
                }
            }

            // Breadth-first distance to the nearest tank, walking edges backwards.
            var queue = new Queue<Node>();
            foreach (var n in _nodes)
            {
                if (!n.IsTank) continue;
                n.Distance = 0;
                queue.Enqueue(n);
            }
            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                if (!incoming.TryGetValue(v, out var sources)) continue;
                foreach (var u in sources)
                {
                    if (u.Distance >= 0) continue;
                    u.Distance = v.Distance + 1;
                    queue.Enqueue(u);
                }
            }

            foreach (var u in _nodes)
            {
                if (u.Distance < 0) continue;
                foreach (var exit in u.Exits)
                    if (exit.node.Distance == u.Distance - 1) u.Next.Add(exit.node);
            }
        }

        private static readonly Direction[] CatchOrder = { Direction.North, Direction.East, Direction.South, Direction.West };

        // Rain lands on every non-ground cell. A part catches its own cell's rain. A bare cell's rain goes to a
        // tarp within reach first, otherwise to the first neighbouring gutter on the same tier (N, E, S, W),
        // otherwise it is runoff.
        private void BuildRainTargets(GridSystem grid)
        {
            var cells = new List<CellData>(grid.Cells);
            cells.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));

            var tarps = new List<Node>();
            foreach (var n in _nodes)
                if (n.Part != null && n.Part.Type == PartType.Tarp) tarps.Add(n);

            foreach (var c in cells)
            {
                if (c.surface == "ground") continue;
                if (_byCell.TryGetValue((c.x, c.z), out var own))
                {
                    if (!own.IsTank) _rainTargets.Add(own);
                    continue;
                }

                Node target = null;
                foreach (var t in tarps)
                {
                    if (Math.Abs(t.X - c.x) <= TarpReach && Math.Abs(t.Z - c.z) <= TarpReach &&
                        grid.HeightAt(t.X, t.Z) == c.h)
                    {
                        target = t;
                        break;
                    }
                }
                if (target == null)
                {
                    foreach (var d in CatchOrder)
                    {
                        int nx = c.x + d.OffsetX(), nz = c.z + d.OffsetZ();
                        if (!_byCell.TryGetValue((nx, nz), out var n) || n.IsTank) continue;
                        if (n.Part.Type == PartType.Downpipe || n.Part.Type == PartType.Tarp) continue;
                        if (grid.HeightAt(nx, nz) != c.h) continue;
                        target = n;
                        break;
                    }
                }
                _rainTargets.Add(target);
            }
        }

        public static Hazards HazardsOf(WaveData wave)
        {
            var result = Hazards.None;
            if (wave.hazards == null) return result;
            foreach (var h in wave.hazards)
                if (h == "leaves") result |= Hazards.Leaves;
            return result;
        }

        public void Step(float intensity, Hazards hazards = Hazards.None)
        {
            Ticks++;
            if ((hazards & Hazards.Leaves) != 0) TickLeaves();

            float rain = RainLitresPerCellPerSec * intensity * TickSeconds;
            foreach (var target in _rainTargets)
            {
                TotalRainLitres += rain;
                if (target == null) RunoffLitres += rain;
                else target.Water += rain;
            }

            foreach (var n in _nodes)
            {
                if (n.IsTank || n.Clogged || n.Water <= Epsilon) continue;
                float amount = Math.Min(n.Water, PartCapacityPerTick);

                if (n.IsValve)
                {
                    var target = ValveTarget(n);
                    if (target == null) continue;
                    n.Pending -= amount;
                    target.Pending += amount;
                    continue;
                }

                if (n.Next.Count == 0) continue;
                float share = amount / n.Next.Count;
                n.Pending -= amount;
                foreach (var v in n.Next) v.Pending += share;
            }
            foreach (var n in _nodes)
            {
                n.Water += n.Pending;
                n.Pending = 0f;
            }

            foreach (var n in _nodes)
            {
                if (n.IsTank) SettleTank(n);
                else SettlePart(n);
            }
        }

        // A valve sends everything out of its selected side, if that side leads to a tank.
        private static Node ValveTarget(Node valve)
        {
            foreach (var exit in valve.Exits)
                if (exit.port == valve.ValveOutlet && exit.node.Distance >= 0) return exit.node;
            return null;
        }

        private void TickLeaves()
        {
            _leafTicks++;
            bool due = _leafTicks == LeafFirstTicks ||
                       (_leafTicks > LeafFirstTicks && (_leafTicks - LeafFirstTicks) % LeafIntervalTicks == 0);
            if (!due) return;

            // Leaves only settle in open gutters that carry water to a tank. The choice is a fixed rotation so a
            // level plays the same way every time.
            var candidates = new List<Node>();
            foreach (var n in _nodes)
            {
                if (n.Part == null || n.Clogged || n.Distance < 0) continue;
                var t = n.Part.Type;
                if (t == PartType.GutterStraight || t == PartType.Elbow90 || t == PartType.TJunction) candidates.Add(n);
            }
            if (candidates.Count == 0) return;

            var chosen = candidates[(ClogCount * 3) % candidates.Count];
            chosen.Clogged = true;
            ClogCount++;
            OnClogged?.Invoke(chosen.X, chosen.Z);
        }

        public bool IsClogged(int x, int z) => _byCell.TryGetValue((x, z), out var n) && n.Clogged;

        public bool ClearClog(int x, int z)
        {
            if (!_byCell.TryGetValue((x, z), out var n) || !n.Clogged) return false;
            n.Clogged = false;
            return true;
        }

        public bool ToggleValve(int x, int z)
        {
            if (!_byCell.TryGetValue((x, z), out var n) || !n.IsValve) return false;
            var left = Direction.West.Rotate(n.Part.Rotation);
            var right = Direction.East.Rotate(n.Part.Rotation);
            n.ValveOutlet = n.ValveOutlet == right ? left : right;
            return true;
        }

        // A sandbag stops a cell from counting as flooded. Water that spills is still lost.
        public bool PlaceSandbag(int x, int z)
        {
            if (SandbagsLeft <= 0) return false;
            if (!_byCell.TryGetValue((x, z), out var n) || n.IsTank || n.Sandbagged) return false;
            n.Sandbagged = true;
            n.FullTicks = 0;
            SandbagsLeft--;
            if (n.Flooded)
            {
                n.Flooded = false;
                OnCellDrained?.Invoke(n.X, n.Z);
            }
            return true;
        }

        private void SettleTank(Node n)
        {
            if (n.Water > n.Capacity)
            {
                OverflowLitres += n.Water - n.Capacity;
                n.Water = n.Capacity;
            }
            if (!n.TankReported && n.Water >= n.Capacity - Epsilon)
            {
                n.TankReported = true;
                OnTankFilled?.Invoke(n.X, n.Z);
            }
        }

        private void SettlePart(Node n)
        {
            bool overflowing = n.Water > n.Capacity;
            if (overflowing)
            {
                OverflowLitres += n.Water - n.Capacity;
                n.Water = n.Capacity;
                OnPartOverflow?.Invoke(n.X, n.Z);
            }
            if (n.Sandbagged) return;

            n.FullTicks = overflowing ? n.FullTicks + 1 : 0;
            bool shouldFlood = n.FullTicks >= FloodTicks;
            if (shouldFlood && !n.Flooded)
            {
                n.Flooded = true;
                EverFlooded++;
                OnCellFlooding?.Invoke(n.X, n.Z);
            }
            else if (!shouldFlood && n.Flooded)
            {
                n.Flooded = false;
                OnCellDrained?.Invoke(n.X, n.Z);
            }
        }

        public void GetStates(List<CellState> into)
        {
            into.Clear();
            foreach (var n in _nodes)
                into.Add(new CellState(n.X, n.Z, n.Water, n.Capacity, n.IsTank, n.Flooded,
                    n.Clogged, n.Sandbagged, n.IsValve, n.ValveOutlet));
        }

        public static int StarsFor(float litres, int floodedCells, GoalsData goals)
        {
            if (goals == null) return litres > 0f ? 1 : 0;
            if (litres >= goals.star3 - 0.01f && floodedCells <= goals.maxFloodedCells) return 3;
            if (litres >= goals.star2) return 2;
            if (litres >= goals.litresTarget * OneStarShare) return 1;
            return 0;
        }

        public SimResult BuildResult(GoalsData goals)
        {
            float litres = TankLitres;
            return new SimResult(litres, EverFlooded, OverflowLitres, StarsFor(litres, EverFlooded, goals));
        }

        // Runs every wave (no player taps) and then lets the pipes drain. Used by tests and the solvability check.
        public static SimResult Run(GridSystem grid, PlacementModel model, LevelData level)
        {
            var sim = new WaterSimulator(grid, model, level.sandbags);
            foreach (var wave in level.waves)
            {
                int ticks = (int)Math.Round(wave.durationSec / TickSeconds);
                var hazards = HazardsOf(wave);
                for (int i = 0; i < ticks; i++) sim.Step(wave.intensity, hazards);
            }
            for (int i = 0; i < DrainTicks; i++) sim.Step(0f);
            return sim.BuildResult(level.goals);
        }
    }
}
