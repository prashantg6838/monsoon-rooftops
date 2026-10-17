using System.Collections.Generic;
using MonsoonRooftops.Grid;
using MonsoonRooftops.Levels;
using MonsoonRooftops.Parts;
using MonsoonRooftops.Water;
using NUnit.Framework;

namespace MonsoonRooftops.Tests
{
    // Every shipped level must carry a reference solution that reaches at least 1 star with no player taps.
    public class LevelSolvabilityTests
    {
        private static IEnumerable<string> ShippedLevels() => LevelIndex.Load().Ordered;

        private static PlacementModel Model(LevelData level, out GridSystem grid)
        {
            grid = new GridSystem(level);
            var limits = new Dictionary<PartType, int>();
            foreach (var p in level.availableParts)
            {
                Assert.IsTrue(PartCatalog.TryParse(p.type, out var t), level.id + ": unknown part " + p.type);
                limits[t] = p.count;
            }
            return new PlacementModel(grid, limits);
        }

        [Test]
        public void Index_ShipsTwelveLevelsInTwoWorlds()
        {
            var index = LevelIndex.Load();
            Assert.AreEqual(2, index.Data.worlds.Length);
            Assert.AreEqual(12, index.Ordered.Count);
        }

        [TestCaseSource(nameof(ShippedLevels))]
        public void ReferenceSolution_ReachesAtLeastOneStar(string id)
        {
            var level = LevelLoader.LoadResource(id);
            Assert.IsNotEmpty(level.solution, id + ": no reference solution stored");
            var model = Model(level, out var grid);
            foreach (var s in level.solution)
            {
                Assert.IsTrue(PartCatalog.TryParse(s.type, out var t), id + ": unknown part " + s.type);
                Assert.IsNotNull(model.Place(t, s.x, s.z, s.rot), id + ": cannot place " + s.type + " at " + s.x + "," + s.z);
            }

            var result = WaterSimulator.Run(grid, model, level);

            Assert.GreaterOrEqual(result.Stars, 1, id + ": only " + result.Litres + " L");
        }

        [TestCaseSource(nameof(ShippedLevels))]
        public void EmptyBoard_EarnsNoStars(string id)
        {
            var level = LevelLoader.LoadResource(id);
            var model = Model(level, out var grid);
            Assert.AreEqual(0, WaterSimulator.Run(grid, model, level).Stars);
        }
    }
}
