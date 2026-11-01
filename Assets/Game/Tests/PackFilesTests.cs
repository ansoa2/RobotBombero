using System.Text;
using NUnit.Framework;
using RobotBombero.Simulation;

namespace RobotBombero.Game.Tests
{
    public class PackFilesTests
    {
        private static readonly string[] None = new string[0];

        [Test]
        public void Export_ContainsNoPlayerData()
        {
            var pack = TestData.Local("local-xyz", "Alpha", 99);
            pack.stats.Add(10, 2);
            string json = PackFiles.Export(pack);

            StringAssert.DoesNotContain("local-xyz", json);
            StringAssert.DoesNotContain("serverPackId", json);
            StringAssert.DoesNotContain("stats", json);
            StringAssert.DoesNotContain("playerKey", json);
            StringAssert.DoesNotContain("playerName", json);
            StringAssert.Contains("\"formatVersion\"", json);
        }

        [Test]
        public void Import_RoundTrip()
        {
            var original = TestData.Local("l", "Alpha", 5);
            ImportResult result = PackFiles.Import(PackFiles.Export(original), None);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Alpha", result.Pack.content.name);
            Assert.AreEqual(
                RulesHash.Compute(original.content.rules),
                RulesHash.Compute(result.Pack.content.rules));
        }

        [Test]
        public void Imported_HasNoServerId()
        {
            ImportResult result = PackFiles.Import(PackFiles.Export(TestData.Local("l", "Alpha", 5)), None);
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, result.Pack.serverPackId);
            Assert.IsFalse(string.IsNullOrEmpty(result.Pack.localId));
            Assert.AreEqual(0, result.Pack.stats.games);
        }

        [TestCase("garbage")]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{}")]
        [TestCase("{\"formatVersion\":1}")]
        [TestCase("[1,2,3]")]
        [TestCase("{\"formatVersion\":1,\"name\":\"x\",\"rules\":[")]
        public void Import_Corrupt_ReturnsError_NoChange(string json)
        {
            ImportResult result = PackFiles.Import(json, new[] { "Existing" });
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Pack);
            Assert.IsFalse(string.IsNullOrEmpty(result.Error));
        }

        [Test]
        public void Import_Null_ReturnsError()
        {
            Assert.IsFalse(PackFiles.Import(null, None).Success);
        }

        [Test]
        public void Import_OverMaxBytes_Rejected()
        {
            string json = PackFiles.Export(TestData.Local("l", "Alpha")) + new string(' ', PackLimits.MaxFileBytes);
            ImportResult result = PackFiles.Import(json, None);
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Pack);
        }

        [Test]
        public void Import_OverMaxBytes_CountsBytesNotChars()
        {
            // Pocos caracteres, muchos bytes UTF-8 (3 por caracter).
            string padding = new string('€', PackLimits.MaxFileBytes / 3 + 1);
            string json = "{\"formatVersion\":1,\"name\":\"" + padding + "\",\"rules\":[]}";
            Assert.Less(json.Length, PackLimits.MaxFileBytes);
            Assert.Greater(Encoding.UTF8.GetByteCount(json), PackLimits.MaxFileBytes);
            Assert.IsFalse(PackFiles.Import(json, None).Success);
        }

        [Test]
        public void Import_UnknownValue_Rejected()
        {
            var pack = TestData.Pack("Alpha");
            pack.rules[0].battery = "Nuclear";
            string json = UnityEngine.JsonUtility.ToJson(pack);
            ImportResult result = PackFiles.Import(json, None);
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Pack);
        }

        [Test]
        public void Import_DuplicateName_GetsSuffix()
        {
            string json = PackFiles.Export(TestData.Local("l", "Alpha"));
            ImportResult second = PackFiles.Import(json, new[] { "Alpha" });
            Assert.IsTrue(second.Success, second.Error);
            Assert.AreEqual("Alpha (2)", second.Pack.content.name);

            ImportResult third = PackFiles.Import(json, new[] { "Alpha", "alpha (2)" });
            Assert.AreEqual("Alpha (3)", third.Pack.content.name);
        }

        [Test]
        public void Import_DuplicateLongName_StaysWithinMaxNameLength()
        {
            string name = new string('n', PackLimits.MaxNameLength);
            string json = PackFiles.Export(TestData.Local("l", name));
            ImportResult result = PackFiles.Import(json, new[] { name });
            Assert.IsTrue(result.Success, result.Error);
            Assert.LessOrEqual(result.Pack.content.name.Length, PackLimits.MaxNameLength);
            Assert.IsTrue(result.Pack.content.name.EndsWith(" (2)"));
            Assert.IsTrue(PackValidator.Validate(result.Pack.content).IsValid);
        }

        [Test]
        public void Import_AcceptsUtf8Bom()
        {
            string json = "﻿" + PackFiles.Export(TestData.Local("l", "Alpha"));
            Assert.IsTrue(PackFiles.Import(json, None).Success);
        }
    }
}
