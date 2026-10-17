using System.Collections.Generic;
using MonsoonRooftops.Grid;
using MonsoonRooftops.Levels;
using MonsoonRooftops.Parts;
using NUnit.Framework;

namespace MonsoonRooftops.Tests
{
    public class PlacementModelTests
    {
        private const string Json = @"{
          ""id"":""t"",""world"":1,""name"":""t"",
          ""grid"":{""width"":4,""depth"":3,""maxHeight"":2},
          ""cells"":[
            {""x"":0,""z"":0,""h"":1,""surface"":""flat_roof""},{""x"":1,""z"":0,""h"":1,""surface"":""flat_roof""},
            {""x"":2,""z"":0,""h"":0,""surface"":""ground""},{""x"":3,""z"":0,""h"":0,""surface"":""ground""}],
          ""fixedObjects"":[{""type"":""tank"",""x"":3,""z"":0,""h"":0,""capacityL"":100}],
          ""availableParts"":[{""type"":""gutter_straight"",""count"":2},{""type"":""elbow_90"",""count"":1},{""type"":""downpipe"",""count"":1}]
        }";

        private static PlacementModel Create(out GridSystem grid)
        {
            var level = LevelLoader.Parse(Json);
            grid = new GridSystem(level);
            var limits = new Dictionary<PartType, int>();
            foreach (var p in level.availableParts)
            {
                Assert.IsTrue(PartCatalog.TryParse(p.type, out var type));
                limits[type] = p.count;
            }
            return new PlacementModel(grid, limits);
        }

        [Test]
        public void Loader_ParsesGridCellsAndTank()
        {
            Create(out var grid);
            Assert.AreEqual(4, grid.Width);
            Assert.IsTrue(grid.HasTank(3, 0));
            Assert.IsTrue(grid.TryGetCell(1, 0, out var cell));
            Assert.AreEqual(1, cell.h);
        }

        [Test]
        public void Place_RespectsLimitAndOccupancy()
        {
            var model = Create(out _);
            Assert.IsNotNull(model.Place(PartType.GutterStraight, 0, 0));
            Assert.IsNull(model.Place(PartType.GutterStraight, 0, 0), "cell occupied");
            Assert.IsNotNull(model.Place(PartType.GutterStraight, 1, 0));
            Assert.AreEqual(0, model.Remaining(PartType.GutterStraight));
            Assert.IsNull(model.Place(PartType.GutterStraight, 2, 0), "limit reached");
        }

        [Test]
        public void Place_RejectsTankOffGridAndMissingCells()
        {
            var model = Create(out _);
            Assert.IsFalse(model.CanPlace(PartType.GutterStraight, 3, 0), "tank cell");
            Assert.IsFalse(model.CanPlace(PartType.GutterStraight, -1, 0), "off grid");
            Assert.IsFalse(model.CanPlace(PartType.GutterStraight, 0, 2), "no cell defined");
        }

        [Test]
        public void Rotate_CyclesPortsClockwise()
        {
            var model = Create(out _);
            var part = model.Place(PartType.GutterStraight, 0, 0);
            Assert.IsTrue(part.HasPort(Direction.North));
            model.Rotate(part);
            Assert.IsTrue(part.HasPort(Direction.East));
            Assert.IsTrue(part.HasPort(Direction.West));
            Assert.IsFalse(part.HasPort(Direction.North));
        }

        [Test]
        public void Ports_ConnectOnSameTierAndToTank()
        {
            var model = Create(out _);
            var a = model.Place(PartType.GutterStraight, 2, 0);
            model.Rotate(a); // east-west on the ground
            Assert.IsTrue(model.IsPortConnected(a, Direction.East), "tank at 3,0");
            Assert.IsFalse(model.IsPortConnected(a, Direction.West), "1,0 is empty");
        }

        [Test]
        public void Ports_DoNotConnectAcrossTiersWithoutDownpipe()
        {
            var model = Create(out _);
            var roof = model.Place(PartType.GutterStraight, 1, 0);
            model.Rotate(roof);
            var ground = model.Place(PartType.GutterStraight, 2, 0);
            model.Rotate(ground);
            Assert.IsFalse(model.IsPortConnected(roof, Direction.East));
            Assert.IsFalse(model.IsPortConnected(ground, Direction.West));
        }

        [Test]
        public void Downpipe_DropsWaterFromRoofGutterToGroundGutter()
        {
            var model = Create(out _);
            var roofGutter = model.Place(PartType.GutterStraight, 0, 0);
            model.Rotate(roofGutter); // east-west
            var pipe = model.Place(PartType.Downpipe, 1, 0, 3); // inlet faces west, outlet faces east
            var groundGutter = model.Place(PartType.GutterStraight, 2, 0);
            model.Rotate(groundGutter);

            Assert.IsTrue(pipe.IsOutlet(Direction.East));
            Assert.IsTrue(model.IsPortConnected(pipe, Direction.West), "inlet meets roof gutter");
            Assert.IsTrue(model.IsPortConnected(pipe, Direction.East), "outlet drops onto ground gutter");
            Assert.IsTrue(model.IsPortConnected(roofGutter, Direction.East));
            Assert.IsTrue(model.IsPortConnected(groundGutter, Direction.West));
            Assert.IsTrue(model.IsPortConnected(groundGutter, Direction.East), "ground gutter reaches tank");
        }

        [Test]
        public void Downpipe_OutletFacingSameTierDoesNotConnect()
        {
            var model = Create(out _);
            var pipe = model.Place(PartType.Downpipe, 0, 0, 3); // outlet faces east
            var next = model.Place(PartType.GutterStraight, 1, 0);
            model.Rotate(next);
            Assert.IsFalse(model.IsPortConnected(pipe, Direction.East), "same tier: outlet cannot drop");
        }

        [Test]
        public void Undo_RestoresPlaceRemoveRotateMove()
        {
            var model = Create(out _);
            var part = model.Place(PartType.GutterStraight, 0, 0);
            model.Rotate(part);
            model.Move(part, 1, 0);
            Assert.AreEqual(3, model.UndoCount);

            model.Undo();
            Assert.AreEqual(0, part.X);
            model.Undo();
            Assert.AreEqual(0, part.Rotation);
            model.Undo();
            Assert.AreEqual(0, model.Parts.Count);
            Assert.AreEqual(2, model.Remaining(PartType.GutterStraight));
        }

        [Test]
        public void Remove_ReturnsPartToTrayAndUndoPutsItBack()
        {
            var model = Create(out _);
            var part = model.Place(PartType.Elbow90, 0, 0);
            model.Remove(part);
            Assert.AreEqual(1, model.Remaining(PartType.Elbow90));
            model.Undo();
            Assert.AreEqual(0, model.Remaining(PartType.Elbow90));
        }

        [Test]
        public void Undo_IsCappedAtTenActions()
        {
            var model = Create(out _);
            var part = model.Place(PartType.GutterStraight, 0, 0);
            for (int i = 0; i < 15; i++) model.Rotate(part);
            Assert.AreEqual(PlacementModel.MaxUndo, model.UndoCount);
        }
    }
}
