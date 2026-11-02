using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RobotBombero.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game.Tests
{
    /// <summary>Tutorial "Cómo se juega" y ventanas de información de los sensores.</summary>
    public class TutorialTests : UiFixture
    {
        private static string AllTutorialText()
        {
            IEnumerable<string> body = TutorialPages.All.SelectMany(p => p.Blocks).SelectMany(b => new[] { b.Heading }.Concat(b.Lines));
            return string.Join("\n", TutorialPages.All.Select(p => p.Title).Concat(body).Where(s => s != null).ToArray());
        }

        // --- Una sola fuente para el texto de los sensores ---

        [Test]
        public void SensorInfo_ExplainsEverySensorOptionOfLabels()
        {
            foreach (string key in Labels.ConditionKeys)
            {
                SensorInfo info = SensorInfo.For(key);
                Assert.IsNotNull(info, key);
                Assert.AreEqual(Labels.Field(key), info.Title, key);
                Assert.IsNotEmpty(info.Measures, key);
                foreach (string value in Labels.ConditionValues(key))
                {
                    SensorOption option = info.Options.FirstOrDefault(o => o.Value == value);
                    Assert.IsNotNull(option, key + " / " + value);
                    Assert.AreEqual(Labels.Value(value), option.Label, key + " / " + value);
                    Assert.IsNotEmpty(option.Meaning, key + " / " + value);
                }
                Assert.AreEqual(Labels.ConditionValues(key).Length, info.Options.Length, key + ": ninguna opción de más");
            }
        }

        [Test]
        public void SensorInfo_NumbersComeFromSimConfig()
        {
            string battery = string.Join(" ", SensorInfo.For("battery").Options.Select(o => o.Meaning));
            StringAssert.Contains(SimConfig.BatteryHighAbove.ToString(), battery);
            StringAssert.Contains(SimConfig.BatteryMediumFrom.ToString(), battery);
            StringAssert.Contains(SimConfig.InitialBattery.ToString(), SensorInfo.For("battery").Measures);
        }

        [Test]
        public void SensorInfo_StatesTheFactsOfTheSimulation()
        {
            SensorOption equal = SensorInfo.For("temp").Options.First(o => o.Value == "Equal");
            StringAssert.Contains("No hay fuego", equal.Meaning);
            StringAssert.Contains("borde", SensorInfo.For("light").Options.First(o => o.Value == "Normal").Meaning);
            StringAssert.Contains("base", SensorInfo.For("light").Options.First(o => o.Value == "Dark").Meaning);
            StringAssert.Contains("borde", SensorInfo.For("prox").Options.First(o => o.Value == "Obstacle Yes").Meaning);
            StringAssert.Contains("eje", SensorInfo.For("temp").Measures);
        }

        [Test]
        public void SimulationCosts_MatchTheConstantsThatTheTutorialQuotes()
        {
            var rules = new[] { new Rule { name = "r", battery = "Anyone", temp = "Anyone", light = "Anyone", prox = "Anyone", cell = "Anyone", actions = new[] { "Put Out" } } };
            var sim = new GameSimulation(rules, 1);
            sim.Step();
            Assert.AreEqual(SimConfig.InitialBattery - SimConfig.PutOutCost, sim.Battery);

            var none = new GameSimulation(new[] { new Rule { name = "r", battery = "Low", temp = "Anyone", light = "Anyone", prox = "Anyone", cell = "Anyone", actions = new[] { "Fwd" } } }, 1);
            none.Step();
            Assert.AreEqual(SimConfig.InitialBattery - SimConfig.NoRuleCost, none.Battery);

            var turn = new GameSimulation(new[] { new Rule { name = "r", battery = "Anyone", temp = "Anyone", light = "Anyone", prox = "Anyone", cell = "Base", actions = new[] { "TurnL" } } }, 1);
            turn.Step();
            Assert.AreEqual(SimConfig.InitialBattery - SimConfig.NoRuleCost, turn.Battery);
        }

        // --- Contenido del tutorial ---

        [Test]
        public void Tutorial_NumbersComeFromSimConfig()
        {
            string text = AllTutorialText();
            StringAssert.Contains(SimConfig.FireTimeout + " ciclos", text);
            StringAssert.Contains(Labels.Number(SimConfig.DefaultMaxSteps), text);
            StringAssert.Contains("hasta " + SimConfig.MaxActionsPerRule + " acciones", text);
            StringAssert.Contains(SimConfig.BoardSize + " x " + SimConfig.BoardSize, text);
            StringAssert.Contains("Cuesta " + SimConfig.PutOutCost, text);
            StringAssert.Contains("pierde " + SimConfig.NoRuleCost, text);
            StringAssert.Contains(SimConfig.InitialBattery.ToString(), text);
        }

        [Test]
        public void Tutorial_CoversEverythingTheBriefAsks()
        {
            string text = AllTutorialText();
            foreach (string needle in new[] { "no controlas", "Cualquiera", "primera regla", "cruz", "base", "obstáculos", "Avanzar", "Girar a la izquierda", "Girar a la derecha", "Apagar fuego",
                "fuegos x pasos", "una fila por jugador", "sin conexión", "Luz", "Brillante", "misión superada" })
                StringAssert.Contains(needle, text, needle);
            foreach (SensorInfo info in SensorInfo.All) StringAssert.Contains(info.Measures, text);
        }

        [Test]
        public void Tutorial_PagesAreShort_AndHaveTitles()
        {
            Assert.GreaterOrEqual(TutorialPages.All.Count, 6);
            foreach (TutorialPage page in TutorialPages.All)
            {
                Assert.IsNotEmpty(page.Title);
                Assert.IsNotEmpty(page.Blocks);
            }
        }

        // --- Navegación ---

        [Test]
        public void Tutorial_OpensFromTheMenu_AndNavigatesBothWays()
        {
            Start();
            Click("menu-tutorial");
            Assert.AreEqual(ScreenId.Tutorial, Router.Current);
            int count = TutorialPages.All.Count;

            Assert.IsFalse(Btn("tutorial-prev").enabledSelf, "En la primera página no hay anterior");
            Assert.AreEqual(TutorialPages.All[0].Title, Lbl("tutorial-page-title").text);
            StringAssert.Contains("1 de " + count, Lbl("tutorial-counter").text);

            Click("tutorial-next");
            Assert.AreEqual(TutorialPages.All[1].Title, Lbl("tutorial-page-title").text);
            StringAssert.Contains("2 de " + count, Lbl("tutorial-counter").text);
            Assert.IsTrue(Btn("tutorial-prev").enabledSelf);

            Click("tutorial-prev");
            Assert.AreEqual(TutorialPages.All[0].Title, Lbl("tutorial-page-title").text);

            for (int i = 0; i < count - 1; i++) Click("tutorial-next");
            Assert.AreEqual(TutorialPages.All[count - 1].Title, Lbl("tutorial-page-title").text);
            Assert.IsFalse(Btn("tutorial-next").enabledSelf, "En la última página no hay siguiente");

            Click("tutorial-back");
            Assert.AreEqual(ScreenId.Menu, Router.Current);
        }

        [Test]
        public void Tutorial_ReopeningStartsAtTheFirstPage()
        {
            Start();
            Click("menu-tutorial");
            Click("tutorial-next");
            Router.HandleBack();
            Click("menu-tutorial");
            Assert.AreEqual(TutorialPages.All[0].Title, Lbl("tutorial-page-title").text);
        }

        [Test]
        public void Tutorial_WorksWithoutConnection_AndBackGoesToTheMenu()
        {
            Store.SaveProfile(new Profile { offline = true });
            Start(false);
            Assert.AreEqual(ScreenId.Menu, Router.Current);

            Click("menu-tutorial");
            Assert.AreEqual(ScreenId.Tutorial, Router.Current);
            Click("tutorial-next");
            Assert.IsTrue(Router.HandleBack());
            Assert.AreEqual(ScreenId.Menu, Router.Current);
        }

        [Test]
        public void Tutorial_TextIsNeverRichText()
        {
            Start();
            Click("menu-tutorial");
            for (int i = 0; i < TutorialPages.All.Count; i++)
            {
                foreach (Label l in Root.Q("tutorial-body").Query<Label>().ToList()) Assert.IsFalse(l.enableRichText, l.text);
                Click("tutorial-next");
            }
        }

        // --- Ventanas de los sensores ---

        private LocalPack EditFirstRule()
        {
            LocalPack pack = AddPack("p1", "Alpha");
            Context.EditingPack = pack;
            Router.Show(ScreenId.RuleEditor);
            Click("rule-edit-0");
            return pack;
        }

        [Test]
        public void RuleEditor_HasAnInfoButtonBesideEachSensor()
        {
            Start();
            EditFirstRule();
            foreach (string key in Labels.ConditionKeys)
            {
                Button info = Btn("sensor-info-" + key);
                Assert.IsTrue(info.ClassListContains("info-btn"), key);
                Assert.IsTrue(info.parent.ClassListContains("field-block__head"), key);
                Assert.AreEqual(Labels.Field(key), info.parent.Q<Label>().text, key);
            }
        }

        [Test]
        public void SensorPopup_ShowsTheExplanation_AndCloseLeavesTheRuleUnchanged()
        {
            Start();
            LocalPack pack = EditFirstRule();
            string before = JsonUtility.ToJson(pack.content);

            foreach (string key in Labels.ConditionKeys)
            {
                Click("sensor-info-" + key);
                Assert.IsTrue(Dialog.IsOpen(Root), key);
                Assert.AreEqual(Labels.Field(key), Lbl("dialog-title").text);
                string shown = string.Join("\n", Root.Q("dialog").Query<Label>().ToList().Select(l => l.text));
                StringAssert.Contains(SensorInfo.For(key).Measures, shown);
                foreach (SensorOption o in SensorInfo.For(key).Options)
                {
                    StringAssert.Contains(o.Label, shown, key);
                    StringAssert.Contains(o.Meaning, shown, key);
                }
                foreach (Label l in Root.Q("dialog").Query<Label>().ToList()) Assert.IsFalse(l.enableRichText, l.text);

                Click("dialog-close");
                Assert.IsFalse(Dialog.IsOpen(Root), key);
                Assert.AreEqual(before, JsonUtility.ToJson(pack.content), key);
            }
            Assert.AreEqual(ScreenId.RuleEditor, Router.Current);
        }

        [Test]
        public void SensorPopup_BackClosesIt_WithoutLeavingTheEditor()
        {
            Start();
            LocalPack pack = EditFirstRule();
            string before = JsonUtility.ToJson(pack.content);

            Click("sensor-info-light");
            Assert.IsTrue(Router.HandleBack());

            Assert.IsFalse(Dialog.IsOpen(Root));
            Assert.AreEqual(ScreenId.RuleEditor, Router.Current);
            Assert.IsNotNull(Root.Q("detail-conditions"));
            Assert.AreEqual(before, JsonUtility.ToJson(pack.content));
            Assert.IsFalse(Context.Packs.NeedsSync(pack), "Abrir y cerrar no cuenta como un cambio");
        }

        [Test]
        public void InfoButton_HasA44PixelTouchTarget()
        {
            string css = File.ReadAllText("Assets/Game/UI/Screens.uss");
            int start = css.IndexOf(".info-btn {");
            Assert.GreaterOrEqual(start, 0);
            string rule = css.Substring(start, css.IndexOf('}', start) - start);
            StringAssert.Contains("min-width: 44px", rule);
            StringAssert.Contains("min-height: 44px", rule);
        }
    }
}
