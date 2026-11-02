using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RobotBombero.Simulation;
using UnityEditor;
using UnityEngine;

namespace RobotBombero.Game.Tests
{
    /// <summary>Un solo mapa de textos en español para los valores de la simulación, que se guardan sin cambios.</summary>
    public class LabelsTests
    {
        private static string[] ValidatorValues(string field)
        {
            FieldInfo info = typeof(PackValidator).GetField(field, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(info, "PackValidator ya no tiene " + field + "; actualiza la prueba");
            return (string[])info.GetValue(null);
        }

        [TestCase("Battery", "battery")]
        [TestCase("Temp", "temp")]
        [TestCase("Light", "light")]
        [TestCase("Prox", "prox")]
        [TestCase("Cell", "cell")]
        public void EveryConditionValue_HasLabel_AndAppearsInTheEditor(string validatorField, string key)
        {
            string[] values = ValidatorValues(validatorField);

            CollectionAssert.AreEquivalent(values, Labels.ConditionValues(key));
            foreach (string value in values)
            {
                string label;
                Assert.IsTrue(Labels.TryValue(value, out label), "Sin etiqueta: " + value);
                Assert.IsNotEmpty(label, value);
            }
        }

        [Test]
        public void EveryAction_HasLabel()
        {
            string[] actions = ValidatorValues("Actions");

            CollectionAssert.AreEquivalent(actions, Labels.ActionValues);
            foreach (string action in actions)
                Assert.IsTrue(Labels.TryValue(action, out _), action);
        }

        [Test]
        public void Labels_AreUniqueWithinEachField()
        {
            foreach (string key in Labels.ConditionKeys)
            {
                string[] texts = Labels.ConditionValues(key).Select(Labels.Value).ToArray();
                Assert.AreEqual(texts.Length, texts.Distinct().Count(), key);
            }
            string[] actions = Labels.ActionValues.Select(Labels.Value).ToArray();
            Assert.AreEqual(actions.Length, actions.Distinct().Count());
        }

        [Test]
        public void EverySensorTextFromTheSimulation_HasLabel()
        {
            var seen = new HashSet<string>();
            for (uint seed = 1; seed <= 20; seed++)
            {
                var rules = new[]
                {
                    new Rule { name = "x", battery = "Anyone", temp = "Anyone", light = "Anyone", prox = "Obstacle Yes", cell = "Anyone", actions = new[] { "TurnL" } },
                    new Rule { name = "y", battery = "Anyone", temp = "Anyone", light = "Anyone", prox = "Anyone", cell = "Anyone", actions = new[] { "Fwd", "TurnR" } }
                };
                var sim = new GameSimulation(rules, seed);
                for (int i = 0; i < 300 && !sim.IsEnded; i++)
                {
                    seen.Add(sim.BatteryState);
                    seen.Add(sim.SensorTemp);
                    seen.Add(sim.SensorLight);
                    seen.Add(sim.SensorProx);
                    seen.Add(sim.SensorCell);
                    sim.Step();
                }
            }

            foreach (string value in seen)
                Assert.IsTrue(Labels.TryValue(value, out _), "Sin etiqueta: " + value);
        }

        [TestCase("Battery", "Sin batería")]
        [TestCase("WalkedIntoFire", "Avanzó hacia el fuego")]
        [TestCase("FireTimeout", "100 ciclos sin apagar un fuego")]
        [TestCase("MissionComplete", "Misión superada: 5.000 pasos")]
        public void EveryEndReason_HasPlainSpanishCause(string reason, string expected)
        {
            Assert.AreEqual(expected, Labels.Cause(reason));
        }

        [Test]
        public void EveryEndReasonOfTheSimulation_IsCovered()
        {
            foreach (EndReason reason in Enum.GetValues(typeof(EndReason)))
            {
                if (reason == EndReason.None) continue;
                Assert.AreNotEqual(reason.ToString(), Labels.Cause(reason.ToString()), reason.ToString());
            }
            Assert.AreEqual("Algo raro", Labels.Cause("Algo raro"), "Un texto desconocido se muestra tal cual");
        }

        [TestCase(0, "0")]
        [TestCase(999, "999")]
        [TestCase(1234, "1.234")]
        [TestCase(1234567, "1.234.567")]
        public void Number_UsesSpanishThousands(long value, string expected)
        {
            Assert.AreEqual(expected, Labels.Number(value));
        }

        [TestCase(0, 0, "0")]
        [TestCase(10, 1234, "0,0081")]
        [TestCase(1, 4, "0,25")]
        [TestCase(5, 5, "1")]
        public void Ratio_UsesDecimalComma(int fires, int steps, string expected)
        {
            Assert.AreEqual(expected, Labels.Ratio(fires, steps));
        }

        [Test]
        public void Fonts_HaveTheGlyphsUsedByControls()
        {
            foreach (string path in new[] { "Assets/Game/UI/Fonts/Inter-Regular.ttf", "Assets/Game/UI/Fonts/Inter-SemiBold.ttf" })
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                foreach (char c in "↑↓×←▶·")
                    Assert.IsTrue(font.HasCharacter(c), path + " no tiene '" + c + "'");
            }
        }
    }
}
