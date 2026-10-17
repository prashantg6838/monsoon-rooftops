using System;
using System.Collections.Generic;
using MonsoonRooftops.Grid;
using MonsoonRooftops.Levels;
using MonsoonRooftops.Parts;
using MonsoonRooftops.Water;
using NUnit.Framework;

namespace MonsoonRooftops.Tests
{
    public class WaterSimulatorTests
    {
        // Row of 6: roof tier 1 (x0-2), ground (x3-4), tank on the ground at x5.
        private const string RowLevel = @"{
          ""id"":""row"",""world"":1,""name"":""row"",
          ""grid"":{""width"":6,""depth"":1,""maxHeight"":2},
          ""cells"":[
            {""x"":0,""z"":0,""h"":1,""surface"":""flat_roof""},{""x"":1,""z"":0,""h"":1,""surface"":""flat_roof""},
            {""x"":2,""z"":0,""h"":1,""surface"":""flat_roof""},{""x"":3,""z"":0,""h"":0,""surface"":""ground""},
            {""x"":4,""z"":0,""h"":0,""surface"":""ground""},{""x"":5,""z"":0,""h"":0,""surface"":""ground""}],
          ""fixedObjects"":[{""type"":""tank"",""x"":5,""z"":0,""h"":0,""capacityL"":100}],
          ""waves"":[{""durationSec"":60,""intensity"":1.0,""label"":""Rain""}],
          ""goals"":{""litresTarget"":100,""star2"":75,""star3"":100,""maxFloodedCells"":0}
        }";

        // 3x2 flat area: gutter (1,0) feeds a T-junction (1,1) with a tank on each side.
        private const string SplitLevel = @"{
          ""id"":""split"",""world"":1,""name"":""split"",
          ""grid"":{""width"":3,""depth"":2,""maxHeight"":1},
          ""cells"":[
            {""x"":0,""z"":0,""h"":0,""surface"":""flat_roof""},{""x"":1,""z"":0,""h"":0,""surface"":""flat_roof""},
            {""x"":2,""z"":0,""h"":0,""surface"":""flat_roof""},{""x"":0,""z"":1,""h"":0,""surface"":""flat_roof""},
            {""x"":1,""z"":1,""h"":0,""surface"":""flat_roof""},{""x"":2,""z"":1,""h"":0,""surface"":""flat_roof""}],
          ""fixedObjects"":[{""type"":""tank"",""x"":0,""z"":1,""h"":0,""capacityL"":1000},{""type"":""tank"",""x"":2,""z"":1,""h"":0,""capacityL"":1000}],
          ""waves"":[{""durationSec"":30,""intensity"":1.0,""label"":""Rain""}]
        }";

        private static PlacementModel Model(LevelData level, out GridSystem grid)
        {
            grid = new GridSystem(level);
            var limits = new Dictionary<PartType, int>();
            foreach (PartType t in Enum.GetValues(typeof(PartType))) limits[t] = 20;
            return new PlacementModel(grid, limits);
        }

        private static void BuildRow(PlacementModel m, bool completeGround)
        {
            m.Rotate(m.Place(PartType.GutterStraight, 0, 0));
            m.Rotate(m.Place(PartType.GutterStraight, 1, 0));
            m.Place(PartType.Downpipe, 2, 0, 3); // inlet west, outlet east
            m.Rotate(m.Place(PartType.GutterStraight, 3, 0));
            if (completeGround) m.Rotate(m.Place(PartType.GutterStraight, 4, 0));
        }

        private static WaterSimulator RunTicks(WaterSimulator sim, float intensity, int ticks)
        {
            for (int i = 0; i < ticks; i++) sim.Step(intensity);
            return sim;
        }

        [Test]
        public void ConnectedPath_FillsTank()
        {
            var level = LevelLoader.Parse(RowLevel);
            var model = Model(level, out var grid);
            BuildRow(model, true);

            var sim = RunTicks(new WaterSimulator(grid, model), 1f, 600);

            Assert.Greater(sim.TankLitres, 50f);
            Assert.AreEqual(0, sim.EverFlooded);
        }

        [Test]
        public void DisconnectedPath_FloodsAndDeliversNothing()
        {
            var level = LevelLoader.Parse(RowLevel);
            var model = Model(level, out var grid);
            BuildRow(model, false); // gap at (4,0)

            var sim = RunTicks(new WaterSimulator(grid, model), 1f, 600);

            Assert.AreEqual(0f, sim.TankLitres, 1e-4f);
            Assert.Greater(sim.EverFlooded, 0);
            Assert.Greater(sim.OverflowLitres, 0f);
        }

        [Test]
        public void Splitter_DividesWaterEvenly()
        {
            var level = LevelLoader.Parse(SplitLevel);
            var model = Model(level, out var grid);
            model.Place(PartType.GutterStraight, 1, 0);
            model.Place(PartType.TJunction, 1, 1);

            var sim = RunTicks(new WaterSimulator(grid, model), 1f, 300);
            var states = new List<CellState>();
            sim.GetStates(states);
            float left = 0f, right = 0f;
            foreach (var s in states)
            {
                if (!s.IsTank) continue;
                if (s.X == 0) left = s.Water; else right = s.Water;
            }

            Assert.Greater(left, 1f);
            Assert.AreEqual(left, right, 1e-3f);
        }

        [Test]
        public void Water_IsConserved()
        {
            var level = LevelLoader.Parse(RowLevel);
            var model = Model(level, out var grid);
            BuildRow(model, false);

            var sim = RunTicks(new WaterSimulator(grid, model), 1f, 400);

            float accounted = sim.StoredLitres + sim.OverflowLitres + sim.RunoffLitres;
            Assert.AreEqual(sim.TotalRainLitres, accounted, 0.05f);
        }

        [Test]
        public void TankNeverExceedsCapacity()
        {
            var level = LevelLoader.Parse(RowLevel);
            var model = Model(level, out var grid);
            BuildRow(model, true);

            var sim = RunTicks(new WaterSimulator(grid, model), 1f, 2000);

            Assert.LessOrEqual(sim.TankLitres, 100.001f);
        }

        [Test]
        public void SameInput_GivesSameOutput()
        {
            var level = LevelLoader.Parse(RowLevel);
            var model = Model(level, out var grid);
            BuildRow(model, true);

            var a = WaterSimulator.Run(grid, model, level);
            var b = WaterSimulator.Run(grid, model, level);

            Assert.AreEqual(a.Litres, b.Litres);
            Assert.AreEqual(a.OverflowLitres, b.OverflowLitres);
            Assert.AreEqual(a.FloodedCells, b.FloodedCells);
        }
    }
}
