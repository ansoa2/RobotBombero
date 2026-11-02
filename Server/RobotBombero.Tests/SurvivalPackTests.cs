using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using RobotBombero.Simulation;
using Xunit;

namespace RobotBombero.Tests
{
    /// <summary>
    /// Vigila que el pack de docs/pruebas/pack-supervivencia.json siga sobreviviendo: si estos umbrales
    /// se incumplen, ha cambiado la simulación o el pack. Medidas y explicación en pack-supervivencia.md.
    /// </summary>
    public class SurvivalPackTests
    {
        private const string PackPath = "docs/pruebas/pack-supervivencia.json";
        private const int SeedCount = 200;

        // Con estas 200 semillas: 5000 pasos de media y 397,44 fuegos de media.
        private const double MinAverageSteps = 4990;
        private const double MinAverageFires = 390;

        private static readonly RulePackFile Pack = LoadPack();

        private static RulePackFile LoadPack()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string path = Path.Combine(dir.FullName, PackPath);
                if (File.Exists(path))
                {
                    // Los nombres deben coincidir exactamente: un campo mal escrito queda nulo y lo rechaza el validador.
                    return JsonSerializer.Deserialize<RulePackFile>(File.ReadAllText(path),
                        new JsonSerializerOptions { IncludeFields = true });
                }
            }
            throw new FileNotFoundException("No se encuentra " + PackPath);
        }

        /// <summary>Las primeras 200 semillas de las 1000 usadas en la medición del documento.</summary>
        private static uint SeedAt(int index)
        {
            return unchecked((uint)(index + 1) * 2654435761u + 12345u);
        }

        [Fact]
        public void Pack_Passes_Validator()
        {
            ValidationResult result = PackValidator.Validate(Pack);
            Assert.True(result.IsValid, result.Error);
            Assert.Equal("Supervivencia", Pack.name);
            Assert.Equal(1, Pack.formatVersion);
        }

        [Fact]
        public void SameSeed_Gives_SameGame_StepByStep()
        {
            uint seed = SeedAt(0);
            var first = new GameSimulation(Pack.rules, seed);
            var second = new GameSimulation(Pack.rules, seed);

            while (!first.IsEnded && !second.IsEnded)
            {
                first.Step();
                second.Step();

                bool same = first.Steps == second.Steps && first.Fires == second.Fires
                    && first.Battery == second.Battery
                    && first.RobotRow == second.RobotRow && first.RobotCol == second.RobotCol
                    && first.DirRow == second.DirRow && first.DirCol == second.DirCol
                    && first.FireRow == second.FireRow && first.FireCol == second.FireCol;
                Assert.True(same, "Las partidas divergen en el paso " + first.Steps);
            }

            Assert.True(first.IsEnded && second.IsEnded);
            Assert.Equal(first.EndReason, second.EndReason);
        }

        [Fact]
        public void Across_FixedSeeds_Survival_StaysAboveThresholds()
        {
            var games = Enumerable.Range(0, SeedCount).Select(i =>
            {
                var game = new GameSimulation(Pack.rules, SeedAt(i));
                game.RunToEnd();
                return game;
            }).ToArray();

            double averageSteps = games.Average(g => g.Steps);
            double averageFires = games.Average(g => g.Fires);

            Assert.True(averageSteps >= MinAverageSteps, "Pasos de media: " + averageSteps);
            Assert.True(averageFires >= MinAverageFires, "Fuegos de media: " + averageFires);
        }
    }
}
