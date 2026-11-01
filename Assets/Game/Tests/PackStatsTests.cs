using NUnit.Framework;

namespace RobotBombero.Game.Tests
{
    public class PackStatsTests
    {
        [Test]
        public void Add_BestComesFromSingleGame()
        {
            var s = new PackStats();
            s.Add(10, 3);   // 30 puntos
            s.Add(100, 2);  // 200 puntos: la mejor
            s.Add(5, 20);   // 100 puntos

            Assert.AreEqual(200, s.bestPoints);
            Assert.AreEqual(100, s.bestSteps);
            Assert.AreEqual(2, s.bestFires);
            Assert.AreEqual(3, s.games);
        }

        [Test]
        public void Add_ZeroPointGame_NotBest()
        {
            var s = new PackStats();
            s.Add(50, 0);
            s.Add(0, 4);

            Assert.AreEqual(0, s.bestPoints);
            Assert.AreEqual(0, s.bestSteps);
            Assert.AreEqual(0, s.bestFires);
            Assert.AreEqual(2, s.games);
            Assert.AreEqual(50, s.totalSteps);
            Assert.AreEqual(4, s.totalFires);
            Assert.AreEqual(25f, s.AverageSteps, 0.001f);
            Assert.AreEqual(2f, s.AverageFires, 0.001f);
        }

        [Test]
        public void Add_EqualPoints_KeepsEarlier()
        {
            var s = new PackStats();
            s.Add(10, 6);  // 60
            s.Add(20, 3);  // 60
            s.Add(6, 10);  // 60

            Assert.AreEqual(60, s.bestPoints);
            Assert.AreEqual(10, s.bestSteps);
            Assert.AreEqual(6, s.bestFires);
        }

        [Test]
        public void Add_PointsBeyondIntRange_StayCorrect()
        {
            var s = new PackStats();
            s.Add(100000, 100000); // 10^10 > int.MaxValue

            Assert.AreEqual(10000000000L, s.bestPoints);
            Assert.AreEqual(100000, s.bestSteps);
            Assert.AreEqual(100000, s.bestFires);
        }
    }
}
