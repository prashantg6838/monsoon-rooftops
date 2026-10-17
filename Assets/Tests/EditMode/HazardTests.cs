using System;
using System.Collections.Generic;
using MonsoonRooftops.Grid;
using MonsoonRooftops.Levels;
using MonsoonRooftops.Parts;
using MonsoonRooftops.Water;
using NUnit.Framework;

namespace MonsoonRooftops.Tests
{
    public class HazardTests
    {
        // Row of 6: roof tier 1 (x0-2), ground (x3-4), tank on the ground at x5.
        private const string RowLevel = @"{
          ""id"":""row"",""world"":1,""name"":""row"",
          ""grid"":{""width"":6,""depth"":1,""maxHeight"":2},
          ""cells"":[
            {""x"":0,""z"":0,""h"":1,""surface"":""flat_roof""},{""x"":1,""z"":0,""h"":1,""surface"":""flat_roof""},
            {""x"":2,""z"":0,""h"":1,""surface"":""flat_roof""},{""x"":3,""z"":0,""h"":0,""surface"":""ground""},
            {""x"":4,""z"":0,""h"":0,""surface"":""ground""},{""x"":5,""z"":0,""h"":0,""surface"":""ground""}],
          ""fixedObjects"":[{""type"":""tank"",""x"":5,""z"":0,""h"":0,""capacityL"":1000}]
        }";

        // 3x2 flat area: gutter (1,0) feeds the centre (1,1) with a tank on each side.
        private const string SplitLevel = @"{
          ""id"":""split"",""world"":1,""name"":""split"",
          ""grid"":{""width"":3,""depth"":2,""maxHeight"":1},
          ""cells"":[
            {""x"":0,""z"":0,""h"":0,""surface"":""flat_roof""},{""x"":1,""z"":0,""h"":0,""surface"":""flat_roof""},
            {""x"":2,""z"":0,""h"":0,""surface"":""flat_roof""},{""x"":0,""z"":1,""h"":0,""surface"":""flat_roof""},
            {""x"":1,""z"":1,""h"":0,""surface"":""flat_roof""},{""x"":2,""z"":1,""h"":0,""surface"":""flat_roof""}],
          ""fixedObjects"":[{""type"":""tank"",""x"":0,""z"":1,""h"":0,""capacityL"":1000},{""type"":""tank"",""x"":2,""z"":1,""h"":0,""capacityL"":1000}]
        }";

        // 3x4 flat area with a tank at (1,0). Rows z=1..3 are open roof.
        private const string TarpLevel = @"{
          ""id"":""tarp"",""world"":2,""name"":""tarp"",
          ""grid"":{""width"":3,""depth"":4,""maxHeight"":1},
          ""cells"":[
            {""x"":0,""z"":0,""h"":0,""surface"":""flat_roof""},{""x"":1,""z"":0,""h"":0,""surface"":""ground""},{""x"":2,""z"":0,""h"":0,""surface"":""flat_roof""},
            {""x"":0,""z"":1,""h"":0,""surface"":""flat_roof""},{""x"":1,""z"":1,""h"":0,""surface"":""flat_roof""},{""x"":2,""z"":1,""h"":0,""surface"":""flat_roof""},
            {""x"":0,""z"":2,""h"":0,""surface"":""flat_roof""},{""x"":1,""z"":2,""h"":0,""surface"":""flat_roof""},{""x"":2,""z"":2,""h"":0,""surface"":""flat_roof""},
            {""x"":0,""z"":3,""h"":0,""surface"":""flat_roof""},{""x"":1,""z"":3,""h"":0,""surface"":""flat_roof""},{""x"":2,""z"":3,""h"":0,""surface"":""flat_roof""}],
          ""fixedObjects"":[{""type"":""tank"",""x"":1,""z"":0,""h"":0,""capacityL"":5000}]
        }";

        private static PlacementModel Model(string json, out GridSystem grid)
        {
            grid = new GridSystem(LevelLoader.Parse(json));
            var limits = new Dictionary<PartType, int>();
            foreach (PartType t in Enum.GetValues(typeof(PartType))) limits[t] = 20;
            return new PlacementModel(grid, limits);
        }

        private static void BuildRow(PlacementModel m, bool completeGround = true)
        {
            m.Rotate(m.Place(PartType.GutterStraight, 0, 0));
            m.Rotate(m.Place(PartType.GutterStraight, 1, 0));
            m.Place(PartType.Downpipe, 2, 0, 3);
            m.Rotate(m.Place(PartType.GutterStraight, 3, 0));
            if (completeGround) m.Rotate(m.Place(PartType.GutterStraight, 4, 0));
        }

        private static void Run(WaterSimulator sim, int ticks, float intensity = 1f, Hazards hazards = Hazards.None)
        {
            for (int i = 0; i < ticks; i++) sim.Step(intensity, hazards);
        }

        private static CellState StateAt(WaterSimulator sim, int x, int z)
        {
            var states = new List<CellState>();
            sim.GetStates(states);
            foreach (var s in states)
                if (s.X == x && s.Z == z) return s;
            throw new InvalidOperationException("no node at " + x + "," + z);
        }

        // ---- Leaves ----

        [Test]
        public void Leaves_ClogAGutterAtAFixedTime()
        {
            var model = Model(RowLevel, out var grid);
            BuildRow(model);
            var sim = new WaterSimulator(grid, model);

            Run(sim, WaterSimulator.LeafFirstTicks - 1, 1f, Hazards.Leaves);
            Assert.AreEqual(0, sim.ClogCount);
            Run(sim, 1, 1f, Hazards.Leaves);
            Assert.AreEqual(1, sim.ClogCount);
        }

        [Test]
        public void Leaves_NeverClogWithoutTheHazard()
        {
            var model = Model(RowLevel, out var grid);
            BuildRow(model);
            var sim = new WaterSimulator(grid, model);
            Run(sim, 500);
            Assert.AreEqual(0, sim.ClogCount);
        }

        [Test]
        public void Leaves_ClogBacksUpAndFloods_TapClearsIt()
        {
            var model = Model(RowLevel, out var grid);
            BuildRow(model);
            var sim = new WaterSimulator(grid, model);
            int cx = -1, cz = -1;
            sim.OnClogged += (x, z) => { cx = x; cz = z; };

            Run(sim, WaterSimulator.LeafFirstTicks, 1f, Hazards.Leaves);
            Assert.IsTrue(sim.IsClogged(cx, cz));

            Run(sim, 150); // rain keeps falling on the blocked gutter
            Assert.IsTrue(StateAt(sim, cx, cz).Flooded, "blocked gutter should flood");

            Assert.IsTrue(sim.ClearClog(cx, cz));
            Run(sim, 60);
            Assert.IsFalse(sim.IsClogged(cx, cz));
            Assert.IsFalse(StateAt(sim, cx, cz).Flooded, "cleared gutter drains");
        }

        [Test]
        public void Leaves_SameScheduleEveryRun()
        {
            var model = Model(RowLevel, out var grid);
            BuildRow(model);
            var first = new List<(int, int)>();
            var second = new List<(int, int)>();
            var a = new WaterSimulator(grid, model);
            var b = new WaterSimulator(grid, model);
            a.OnClogged += (x, z) => first.Add((x, z));
            b.OnClogged += (x, z) => second.Add((x, z));

            Run(a, 400, 1f, Hazards.Leaves);
            Run(b, 400, 1f, Hazards.Leaves);

            Assert.Greater(first.Count, 1);
            CollectionAssert.AreEqual(first, second);
        }

        // ---- Valve ----

        private static void TankLevels(WaterSimulator sim, out float left, out float right)
        {
            left = StateAt(sim, 0, 1).Water;
            right = StateAt(sim, 2, 1).Water;
        }

        [Test]
        public void Valve_SendsAllWaterToOneSide_TapSwitchesSide()
        {
            var model = Model(SplitLevel, out var grid);
            model.Place(PartType.GutterStraight, 1, 0);
            model.Place(PartType.Valve, 1, 1); // inlet south, starts sending east
            var sim = new WaterSimulator(grid, model);

            Run(sim, 100);
            TankLevels(sim, out var left, out var right);
            Assert.Greater(right, 1f);
            Assert.AreEqual(0f, left, 1e-4f);

            Assert.IsTrue(sim.ToggleValve(1, 1));
            Run(sim, 100);
            TankLevels(sim, out var left2, out var right2);
            Assert.Greater(left2, 1f, "after the tap the west tank fills");
            Assert.AreEqual(right, right2, 0.5f, "east tank stops gaining (only leftover drains)");
        }

        [Test]
        public void Valve_ToggleOnNonValveDoesNothing()
        {
            var model = Model(SplitLevel, out var grid);
            model.Place(PartType.GutterStraight, 1, 0);
            var sim = new WaterSimulator(grid, model);
            Assert.IsFalse(sim.ToggleValve(1, 0));
        }

        // ---- Sandbags ----

        [Test]
        public void Sandbag_StopsAFloodingCell()
        {
            var model = Model(RowLevel, out var grid);
            BuildRow(model, false); // gap: everything backs up and floods
            var sim = new WaterSimulator(grid, model, 1);

            Run(sim, 200);
            Assert.IsTrue(StateAt(sim, 0, 0).Flooded);
            int before = sim.FloodedNow;

            Assert.IsTrue(sim.PlaceSandbag(0, 0));
            Run(sim, 100);
            Assert.IsFalse(StateAt(sim, 0, 0).Flooded);
            Assert.IsTrue(StateAt(sim, 0, 0).Sandbagged);
            Assert.AreEqual(before - 1, sim.FloodedNow);
        }

        [Test]
        public void Sandbag_LimitedCount()
        {
            var model = Model(RowLevel, out var grid);
            BuildRow(model, false);
            var sim = new WaterSimulator(grid, model, 1);

            Assert.IsTrue(sim.PlaceSandbag(0, 0));
            Assert.IsFalse(sim.PlaceSandbag(1, 0), "no sandbags left");
            Assert.AreEqual(0, sim.SandbagsLeft);
        }

        [Test]
        public void Sandbag_CannotGoOnTankOrEmptyCell()
        {
            var model = Model(RowLevel, out var grid);
            BuildRow(model, false);
            var sim = new WaterSimulator(grid, model, 3);
            Assert.IsFalse(sim.PlaceSandbag(5, 0), "tank");
            Assert.IsFalse(sim.PlaceSandbag(4, 0), "empty cell");
            Assert.AreEqual(3, sim.SandbagsLeft);
        }

        // ---- Tarp ----

        [Test]
        public void Tarp_CatchesMoreRainThanAGutterInTheSameSpot()
        {
            var withTarp = Model(TarpLevel, out var gridA);
            withTarp.Place(PartType.GutterStraight, 1, 1); // north-south, feeds the tank at (1,0)
            withTarp.Place(PartType.Tarp, 1, 2);           // spout faces south into the gutter
            var a = new WaterSimulator(gridA, withTarp);

            var withGutter = Model(TarpLevel, out var gridB);
            withGutter.Place(PartType.GutterStraight, 1, 1);
            withGutter.Place(PartType.GutterStraight, 1, 2);
            var b = new WaterSimulator(gridB, withGutter);

            Run(a, 300);
            Run(b, 300);

            Assert.Greater(a.TankLitres, b.TankLitres);
            Assert.Less(a.RunoffLitres, b.RunoffLitres);
        }

        [Test]
        public void Tarp_WithoutConnectedSpout_Floods()
        {
            var model = Model(TarpLevel, out var grid);
            model.Rotate(model.Place(PartType.Tarp, 1, 2)); // spout faces west into open roof
            var sim = new WaterSimulator(grid, model);

            Run(sim, 200);

            Assert.AreEqual(0f, sim.TankLitres, 1e-4f);
            Assert.Greater(sim.EverFlooded, 0);
        }
    }
}
