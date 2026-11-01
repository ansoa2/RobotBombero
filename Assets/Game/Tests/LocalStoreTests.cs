using System.IO;
using NUnit.Framework;

namespace RobotBombero.Game.Tests
{
    public class LocalStoreTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rb-store-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void SaveAndReload_PreservesPacksAndStats()
        {
            var store = new LocalStore(_root);
            var pack = TestData.Local("a1", "Alpha", 7);
            pack.stats.Add(100, 3);
            pack.stats.Add(50, 5);
            store.SavePack(pack);
            store.SaveProfile(new Profile { playerName = "Ana", playerKey = "k-secret", playerId = 42 });

            var reloaded = new LocalStore(_root);
            var packs = reloaded.LoadPacks();
            Assert.AreEqual(1, packs.Count);
            Assert.AreEqual("a1", packs[0].localId);
            Assert.AreEqual(7, packs[0].serverPackId);
            Assert.AreEqual("Alpha", packs[0].content.name);
            Assert.AreEqual(2, packs[0].content.rules.Length);
            Assert.AreEqual("Put Out", packs[0].content.rules[1].actions[0]);
            Assert.AreEqual(2, packs[0].stats.games);
            Assert.AreEqual(150, packs[0].stats.totalSteps);
            Assert.AreEqual(8, packs[0].stats.totalFires);
            Assert.AreEqual(100, packs[0].stats.bestSteps);
            Assert.AreEqual(3, packs[0].stats.bestFires);
            Assert.AreEqual(300, packs[0].stats.bestPoints);
            Assert.AreEqual(75f, packs[0].stats.AverageSteps, 0.001f);
            Assert.AreEqual(4f, packs[0].stats.AverageFires, 0.001f);

            var profile = reloaded.LoadProfile();
            Assert.AreEqual("Ana", profile.playerName);
            Assert.AreEqual("k-secret", profile.playerKey);
            Assert.AreEqual(42, profile.playerId);
        }

        [Test]
        public void LoadProfile_WhenMissing_ReturnsEmptyProfile()
        {
            var profile = new LocalStore(_root).LoadProfile();
            Assert.IsNotNull(profile);
            Assert.IsTrue(string.IsNullOrEmpty(profile.playerKey));
        }

        [Test]
        public void CorruptPackFile_IsSkipped_OthersLoad()
        {
            var store = new LocalStore(_root);
            store.SavePack(TestData.Local("good", "Good"));
            store.SavePack(TestData.Local("bad", "Bad"));
            string badFile = Directory.GetFiles(_root, "*bad*", SearchOption.AllDirectories)[0];
            File.WriteAllText(badFile, "{{{ not json");

            var packs = new LocalStore(_root).LoadPacks();
            Assert.AreEqual(1, packs.Count);
            Assert.AreEqual("good", packs[0].localId);
            Assert.IsTrue(File.Exists(badFile), "El archivo corrupto no se borra.");
        }

        [Test]
        public void Delete_RemovesOnlyThatPack()
        {
            var store = new LocalStore(_root);
            store.SavePack(TestData.Local("one", "One"));
            store.SavePack(TestData.Local("two", "Two"));
            store.DeletePack("one");

            var packs = store.LoadPacks();
            Assert.AreEqual(1, packs.Count);
            Assert.AreEqual("two", packs[0].localId);
        }

        [Test]
        public void SavePack_Overwrite_ReplacesAndLeavesNoTempFiles()
        {
            var store = new LocalStore(_root);
            var pack = TestData.Local("x", "First");
            store.SavePack(pack);
            pack.content.name = "Second";
            store.SavePack(pack);

            Assert.AreEqual("Second", store.LoadPacks()[0].content.name);
            Assert.AreEqual(0, Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories).Length);
        }

        [Test]
        public void PathTraversalId_IsRejected()
        {
            var store = new LocalStore(_root);
            Assert.Throws<System.ArgumentException>(() => store.SavePack(TestData.Local("../evil", "Evil")));
            Assert.Throws<System.ArgumentException>(() => store.DeletePack("..\\evil"));
        }
    }
}
