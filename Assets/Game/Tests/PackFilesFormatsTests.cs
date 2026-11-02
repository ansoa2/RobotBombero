using System.IO;
using System.Linq;
using NUnit.Framework;
using RobotBombero.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game.Tests
{
    /// <summary>Importación del formato del juego antiguo, nombre de archivo de exportación y botones nativos.</summary>
    public class PackFilesFormatsTests : UiFixture
    {
        private static readonly string[] None = new string[0];
        private const string ReferencePath = @"D:\ProyectosClaude\RobotBombero\Documentación\pack-reglas-antiguas.json";

        private static string FixturePath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "pruebas", "reglas-antiguas-rules"));
        }

        private static string Old(string ruleBody)
        {
            return "{\"rulemodel_list\":[" + ruleBody + "]}";
        }

        private static string OldRule(string battery = "Medium", string action = "\"Put Out\"")
        {
            return "{\"id_rule\":1,\"name\":\"Rule 1\",\"battery_state\":\"" + battery + "\",\"sensor_temp\":\"Anyone\",\"sensor_light\":\"Bright\",\"sensor_prox\":\"Anyone\",\"sensor_cell\":\"Anyone\",\"action\":[" + action + "]}";
        }

        [Test]
        public void Import_OldFormat_RealFile_Gives13RulesInOrder()
        {
            if (!File.Exists(ReferencePath)) Assert.Ignore("Falta la referencia " + ReferencePath);
            RulePackFile expected = JsonUtility.FromJson<RulePackFile>(File.ReadAllText(ReferencePath));

            ImportResult result = PackFiles.Import(File.ReadAllText(FixturePath()), None);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Reglas importadas", result.Pack.content.name);
            Assert.AreEqual(PackLimits.CurrentFormatVersion, result.Pack.content.formatVersion);
            Rule[] rules = result.Pack.content.rules;
            Assert.AreEqual(13, rules.Length);
            Assert.AreEqual(expected.rules.Length, rules.Length);
            for (int i = 0; i < rules.Length; i++)
            {
                Assert.AreEqual(expected.rules[i].name, rules[i].name, "nombre " + i);
                Assert.AreEqual(expected.rules[i].battery, rules[i].battery, "batería " + i);
                Assert.AreEqual(expected.rules[i].temp, rules[i].temp, "temp " + i);
                Assert.AreEqual(expected.rules[i].light, rules[i].light, "luz " + i);
                Assert.AreEqual(expected.rules[i].prox, rules[i].prox, "prox " + i);
                Assert.AreEqual(expected.rules[i].cell, rules[i].cell, "celda " + i);
                CollectionAssert.AreEqual(expected.rules[i].actions, rules[i].actions, "acciones " + i);
            }
            Assert.IsTrue(PackValidator.Validate(result.Pack.content).IsValid);
        }

        [Test]
        public void Import_OldFormat_DuplicateName_GetsSuffix()
        {
            ImportResult result = PackFiles.Import(Old(OldRule()), new[] { "Reglas importadas" });
            Assert.IsTrue(result.Success);
            Assert.AreEqual("Reglas importadas (2)", result.Pack.content.name);
        }

        [Test]
        public void Import_OldFormat_UnknownValue_Rejected()
        {
            ImportResult result = PackFiles.Import(Old(OldRule("Turbo")), None);
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Pack);
        }

        [Test]
        public void Import_OldFormat_TooManyActions_Rejected()
        {
            string six = string.Join(",", Enumerable.Repeat("\"Fwd\"", SimConfig.MaxActionsPerRule + 1).ToArray());
            Assert.IsFalse(PackFiles.Import(Old(OldRule("High", six)), None).Success);
        }

        [Test]
        public void Import_OldFormat_Empty_Rejected()
        {
            Assert.IsFalse(PackFiles.Import("{\"rulemodel_list\":[]}", None).Success);
            Assert.IsFalse(PackFiles.Import("{\"rulemodel_list\":null}", None).Success);
            Assert.IsFalse(PackFiles.Import("", None).Success);
        }

        [Test]
        public void Import_OldFormat_TooManyRules_Rejected()
        {
            string many = string.Join(",", Enumerable.Repeat(OldRule(), PackLimits.MaxRules + 1).ToArray());
            Assert.IsFalse(PackFiles.Import(Old(many), None).Success);
        }

        [Test]
        public void Import_NewFormat_StillWorks()
        {
            var original = TestData.Local("l", "Alpha");
            ImportResult result = PackFiles.Import(PackFiles.Export(original), None);
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Alpha", result.Pack.content.name);
            Assert.AreEqual(original.content.rules.Length, result.Pack.content.rules.Length);
        }

        [Test]
        public void Import_Garbage_RejectedWithExistingMessage()
        {
            ImportResult result = PackFiles.Import("{ esto no es un pack", None);
            Assert.IsFalse(result.Success);
            StringAssert.Contains("no es un pack válido", result.Error);
        }

        [TestCase("Alpha", "Alpha.json")]
        [TestCase("Mi pack 2", "Mi pack 2.json")]
        [TestCase("a/b\\c:d*e?f\"g<h>i|j", "a_b_c_d_e_f_g_h_i_j.json")]
        [TestCase("con", "_con.json")]
        [TestCase("COM1", "_COM1.json")]
        [TestCase("   ", "pack.json")]
        [TestCase("...", "pack.json")]
        [TestCase("nombre.", "nombre_.json")]
        public void Export_FileName_Sanitized(string packName, string expected)
        {
            Assert.AreEqual(expected, PackFiles.ExportFileName(packName));
        }

        [Test]
        public void Export_FileName_HasNoInvalidCharacters()
        {
            string name = PackFiles.ExportFileName("x\t\ny\u0001z/..\\..");
            Assert.AreEqual(-1, name.IndexOfAny(Path.GetInvalidFileNameChars()));
            Assert.IsTrue(name.EndsWith(".json"));
        }

        [Test]
        public void Export_ThenImport_RoundTrip()
        {
            var original = TestData.Local("l", "Ñandú ✓");
            string json = PackFiles.Export(original);
            ImportResult result = PackFiles.Import(json, None);
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(json, PackFiles.Export(result.Pack));
            StringAssert.DoesNotContain("stats", json);
        }

        [Test]
        public void Import_ArmsOnOrdinaryClick()
        {
            Start();
            VisualElement view = Load<VisualTreeAsset>("Screens/Rules.uxml").CloneTree();
            var rules = new RulesController(view, Context, id => { });
            rules.OnShow();
            Click(view.Q<Button>("rules-import"));
            Assert.AreEqual(1, Files.PickCalls);
        }

        [Test]
        public void Export_Cancelled_ShowsNothing()
        {
            Start();
            AddPack("p1", "Alpha");
            Files.DownloadCancelled = true;
            Router.Show(ScreenId.Rules);

            Click("pack-export-0");

            Assert.IsFalse(IsShown(Lbl("rules-message")));
        }

        [Test]
        public void NativeFileService_EnablesBothButtons_WithNoComingSoonNote()
        {
            Start();
            AddPack("p1", "Uno");
            var context = new GameContext(Store, Api, Drain, new FileBridgeService());
            VisualElement view = Load<VisualTreeAsset>("Screens/Rules.uxml").CloneTree();
            var rules = new RulesController(view, context, id => { });
            rules.OnShow();

            Assert.IsTrue(IsShown(view.Q<Button>("rules-import")));
            Assert.IsNotNull(view.Q<Button>("pack-export-0"));
            Assert.IsFalse(IsShown(view.Q<Label>("rules-files-note")));
            StringAssert.DoesNotContain("Próximamente", view.Q<Label>("rules-files-note").text);
        }
    }
}
