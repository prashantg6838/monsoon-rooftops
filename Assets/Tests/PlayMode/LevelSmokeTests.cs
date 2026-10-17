using MonsoonRooftops.Levels;
using NUnit.Framework;

namespace MonsoonRooftops.Tests
{
    public class LevelSmokeTests
    {
        [Test]
        public void Level1_LoadsFromResources()
        {
            var level = LevelLoader.LoadResource("w1_l01");
            Assert.AreEqual("w1_l01", level.id);
            Assert.Greater(level.cells.Length, 0);
        }
    }
}
