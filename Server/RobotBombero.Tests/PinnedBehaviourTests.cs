using System;
using System.IO;
using System.Text.Json;
using RobotBombero.Simulation;
using Xunit;

namespace RobotBombero.Tests
{
    /// <summary>
    /// Fija el comportamiento exacto de la simulación y de la huella: si cambia una sola regla del juego,
    /// estas pruebas fallan. Los valores salen de la versión actual, que es la que el servidor y las apps repiten.
    /// </summary>
    public class PinnedBehaviourTests
    {
        private static Rule R(string[] actions, string light = "Anyone", string temp = "Anyone", string prox = "Anyone")
        {
            return new Rule { name = "r", battery = "Anyone", temp = temp, light = light, prox = prox, cell = "Anyone", actions = actions };
        }

        private static RulePackFile LoadSurvival()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string path = Path.Combine(dir.FullName, "docs/pruebas/pack-supervivencia.json");
                if (File.Exists(path))
                    return JsonSerializer.Deserialize<RulePackFile>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true });
            }
            throw new FileNotFoundException("No se encuentra el pack de supervivencia.");
        }

        private static Rule[] Seeker()
        {
            return new[] { R(new[] { "Put Out" }, light: "Bright"), R(new[] { "Fwd" }, temp: "Up", prox: "Obstacle No"), R(new[] { "TurnL" }) };
        }

        private static void AssertGame(Rule[] rules, uint seed, int steps, int fires, EndReason reason)
        {
            var game = new GameSimulation(rules, seed);
            game.RunToEnd();
            Assert.Equal(steps, game.Steps);
            Assert.Equal(fires, game.Fires);
            Assert.Equal(reason, game.EndReason);
        }

        [Theory]
        [InlineData(2654448106u, 5000, 391)]
        [InlineData(4055629249u, 5000, 389)]
        [InlineData(2732532213u, 5000, 400)]
        [InlineData(12345u, 5000, 410)]
        public void SurvivalPack_ExactResults(uint seed, int steps, int fires)
        {
            AssertGame(LoadSurvival().rules, seed, steps, fires, EndReason.MissionComplete);
        }

        [Theory]
        [InlineData(12345u, 31, 5)]
        [InlineData(1u, 55, 2)]
        [InlineData(987654321u, 43, 5)]
        public void SeekerPack_ExactResults(uint seed, int steps, int fires)
        {
            AssertGame(Seeker(), seed, steps, fires, EndReason.Battery);
        }

        [Fact]
        public void ForwardOnlyPack_DiesOfBatteryAt50Steps()
        {
            AssertGame(new[] { R(new[] { "Fwd" }) }, 5u, 50, 0, EndReason.Battery);
        }

        [Fact]
        public void RulesHash_KnownVector()
        {
            // SHA-256 de "1|6:Anyone|6:Anyone|6:Anyone|6:Anyone|6:Anyone|1|3:Fwd|", calculado aparte.
            Assert.Equal("e136ad793055265c03f8bcd256d1ee3d6916021b4d770a77dc2ce831a9108405",
                RulesHash.Compute(new[] { R(new[] { "Fwd" }) }));
        }

        [Fact]
        public void NewFire_LandsOnlyOnFreeCells_AndNotInTheRobotsRowOrColumn()
        {
            var game = new GameSimulation(LoadSurvival().rules, 12345u);
            int checkedFires = 0;
            while (!game.IsEnded)
            {
                var before = new CellState[8, 8];
                for (int r = 0; r < 8; r++)
                    for (int c = 0; c < 8; c++) before[r, c] = game.GetCell(r, c);
                int oldRow = game.FireRow, oldCol = game.FireCol;
                int fires = game.Fires;

                game.Step();

                if (game.Fires == fires) continue;
                checkedFires++;
                bool wasFree = before[game.FireRow, game.FireCol] == CellState.Free
                    || (game.FireRow == oldRow && game.FireCol == oldCol);
                Assert.True(wasFree, "El fuego nuevo cayó en (" + game.FireRow + "," + game.FireCol + ") que era " + before[game.FireRow, game.FireCol]);
                Assert.NotEqual(game.RobotRow, game.FireRow);
                Assert.NotEqual(game.RobotCol, game.FireCol);
                Assert.Equal(CellState.Fire, game.GetCell(game.FireRow, game.FireCol));
            }
            Assert.True(checkedFires > 100, "Fuegos comprobados: " + checkedFires);
        }

        [Fact]
        public void FatalPutOut_StillCountsItsFire()
        {
            var game = new GameSimulation(new[] { R(new[] { "TurnL" }) }, 7u);
            foreach (string a in new[] { "TurnL", "Fwd", "TurnR", "Fwd" }) game.ApplyManual(a);
            Assert.Equal("Bright", game.SensorLight);
            // 46 de batería tras llegar; 40 giros (10 vueltas) dejan 6; un giro a cada lado deja 4 mirando al fuego.
            // Apagar cuesta 5, así que el robot muere en la misma acción que apaga el fuego.
            for (int i = 0; i < 40; i++) game.ApplyManual("TurnL");
            game.ApplyManual("TurnL");
            game.ApplyManual("TurnR");
            Assert.Equal(4, game.Battery);
            Assert.Equal("Bright", game.SensorLight);
            Assert.False(game.IsEnded);
            Assert.Equal(0, game.Fires);

            game.ApplyManual("Put Out");

            Assert.Equal(1, game.Fires);
            Assert.True(game.IsEnded);
            Assert.Equal(EndReason.Battery, game.EndReason);
        }
    }
}
