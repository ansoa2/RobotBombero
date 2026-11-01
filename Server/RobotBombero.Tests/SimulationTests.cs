using System.Collections.Generic;
using RobotBombero.Simulation;
using Xunit;

namespace RobotBombero.Tests
{
    public class SimulationTests
    {
        private const uint Seed = 12345;

        private static Rule MakeRule(string[] actions, string battery = "Anyone", string temp = "Anyone",
            string light = "Anyone", string prox = "Anyone", string cell = "Anyone")
        {
            return new Rule
            {
                name = "regla",
                battery = battery,
                temp = temp,
                light = light,
                prox = prox,
                cell = cell,
                actions = actions
            };
        }

        private static Rule[] Rules(params Rule[] rules) => rules;

        private static string[] Actions(params string[] actions) => actions;

        private static GameSimulation NewSim(params Rule[] rules) => new GameSimulation(rules, Seed);

        private static void Manual(GameSimulation sim, params string[] actions)
        {
            foreach (string action in actions)
            {
                sim.ApplyManual(action);
            }
        }

        /// <summary>Deja el robot en (2,2), mirando hacia el fuego inicial de (1,2). Gasta 4 de batería.</summary>
        private static void GoInFrontOfInitialFire(GameSimulation sim)
        {
            Manual(sim, "TurnL", "Fwd", "TurnR", "Fwd");
            Assert.Equal(2, sim.RobotRow);
            Assert.Equal(2, sim.RobotCol);
            Assert.Equal(-1, sim.DirRow);
            Assert.Equal(0, sim.DirCol);
            Assert.Equal("Bright", sim.SensorLight);
        }

        /// <summary>Deja el robot sobre la base (4,3) mirando hacia abajo. Gasta 3 de batería.</summary>
        private static void GoToBase(GameSimulation sim)
        {
            Manual(sim, "TurnL", "TurnL", "Fwd");
            Assert.Equal(4, sim.RobotRow);
            Assert.Equal(3, sim.RobotCol);
            Assert.Equal("Base", sim.SensorCell);
        }

        private static void DrainWithTurns(GameSimulation sim, int targetBattery)
        {
            while (sim.Battery > targetBattery)
            {
                sim.ApplyManual("TurnL");
            }
            Assert.Equal(targetBattery, sim.Battery);
        }

        [Fact]
        public void InitialState_MatchesOriginal()
        {
            GameSimulation sim = NewSim();

            Assert.Equal(3, sim.RobotRow);
            Assert.Equal(3, sim.RobotCol);
            Assert.Equal(-1, sim.DirRow);
            Assert.Equal(0, sim.DirCol);
            Assert.Equal(50, sim.Battery);
            Assert.Equal(1, sim.FireRow);
            Assert.Equal(2, sim.FireCol);
            Assert.Equal(0, sim.Steps);
            Assert.Equal(0, sim.Fires);
            Assert.False(sim.IsEnded);
            Assert.Equal(EndReason.None, sim.EndReason);

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    CellState expected = CellState.Free;
                    if (row == 4 || col == 3) expected = CellState.Dark;
                    if (row == 4 && col == 3) expected = CellState.Base;
                    if ((row == 1 && col == 5) || (row == 5 && col == 1) || (row == 6 && col == 6)) expected = CellState.Obstacle;
                    if (row == 1 && col == 2) expected = CellState.Fire;
                    Assert.Equal(expected, sim.GetCell(row, col));
                }
            }

            // Sensores calculados tras colocar robot y fuego.
            Assert.Equal("High", sim.BatteryState);
            Assert.Equal("Up", sim.SensorTemp);
            Assert.Equal("Dark", sim.SensorLight);
            Assert.Equal("Obstacle No", sim.SensorProx);
            Assert.Equal("Black", sim.SensorCell);
        }

        [Fact]
        public void Battery_Thresholds()
        {
            GameSimulation sim = NewSim();

            DrainWithTurns(sim, 31);
            Assert.Equal("High", sim.BatteryState);
            DrainWithTurns(sim, 30);
            Assert.Equal("Medium", sim.BatteryState);
            DrainWithTurns(sim, 11);
            Assert.Equal("Medium", sim.BatteryState);
            DrainWithTurns(sim, 10);
            Assert.Equal("Medium", sim.BatteryState);
            DrainWithTurns(sim, 9);
            Assert.Equal("Low", sim.BatteryState);
        }

        [Fact]
        public void Fwd_Blocked_CostsOneWithoutMoving()
        {
            // Bloqueado por un obstáculo: (1,5) visto desde (2,5).
            GameSimulation sim = NewSim();
            Manual(sim, "TurnR", "Fwd", "Fwd", "TurnL", "Fwd");
            Assert.Equal(2, sim.RobotRow);
            Assert.Equal(5, sim.RobotCol);
            Assert.Equal("Obstacle Yes", sim.SensorProx);
            int before = sim.Battery;

            sim.ApplyManual("Fwd");

            Assert.Equal(before - 1, sim.Battery);
            Assert.Equal(2, sim.RobotRow);
            Assert.Equal(5, sim.RobotCol);

            // Bloqueado por el borde: desde (0,3) mirando hacia arriba.
            sim = NewSim();
            Manual(sim, "Fwd", "Fwd", "Fwd");
            Assert.Equal(0, sim.RobotRow);
            Assert.Equal("Obstacle Yes", sim.SensorProx);
            before = sim.Battery;

            sim.ApplyManual("Fwd");

            Assert.Equal(before - 1, sim.Battery);
            Assert.Equal(0, sim.RobotRow);
            Assert.Equal(3, sim.RobotCol);
        }

        [Fact]
        public void Fwd_IntoFire_EndsWithoutCost()
        {
            GameSimulation sim = NewSim(MakeRule(Actions("Fwd")));
            GoInFrontOfInitialFire(sim);
            int before = sim.Battery;

            sim.Step();

            Assert.True(sim.IsEnded);
            Assert.Equal(EndReason.WalkedIntoFire, sim.EndReason);
            Assert.Equal(before, sim.Battery);
            Assert.Equal(2, sim.RobotRow);
            Assert.Equal(1, sim.Steps);
        }

        [Fact]
        public void Turn_OnBase_RechargesThenCosts()
        {
            GameSimulation sim = NewSim();
            GoToBase(sim);
            Assert.Equal(47, sim.Battery);

            sim.ApplyManual("TurnR");
            Assert.Equal(49, sim.Battery);

            sim.ApplyManual("TurnL");
            Assert.Equal(49, sim.Battery);
        }

        [Fact]
        public void Fwd_LeavingBase_EndsWithBattery50()
        {
            GameSimulation sim = NewSim();
            GoToBase(sim);
            Assert.Equal(47, sim.Battery);

            sim.ApplyManual("Fwd");

            Assert.Equal(5, sim.RobotRow);
            Assert.Equal(3, sim.RobotCol);
            Assert.Equal(50, sim.Battery);
        }

        [Fact]
        public void PutOut_Costs5_AndCountsFire_ThenNewFireOutsideRobotRowAndCol()
        {
            GameSimulation sim = NewSim();
            GoInFrontOfInitialFire(sim);
            int before = sim.Battery;

            sim.ApplyManual("Put Out");

            Assert.Equal(before - 5, sim.Battery);
            Assert.Equal(1, sim.Fires);
            Assert.False(sim.IsEnded);
            Assert.Equal(CellState.Free, sim.GetCell(1, 2));
            Assert.NotEqual(sim.RobotRow, sim.FireRow);
            Assert.NotEqual(sim.RobotCol, sim.FireCol);
            Assert.Equal(CellState.Fire, sim.GetCell(sim.FireRow, sim.FireCol));

            int fireCells = 0;
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    if (sim.GetCell(row, col) == CellState.Fire) fireCells++;
                }
            }
            Assert.Equal(1, fireCells);
        }

        [Fact]
        public void PutOut_WithoutFire_StillCosts5()
        {
            GameSimulation sim = NewSim();

            sim.ApplyManual("Put Out");

            Assert.Equal(45, sim.Battery);
            Assert.Equal(0, sim.Fires);
            Assert.Equal(1, sim.FireRow);
            Assert.Equal(2, sim.FireCol);
        }

        [Fact]
        public void Rule_FirstMatchOnly()
        {
            GameSimulation sim = NewSim(
                MakeRule(Actions("Fwd"), battery: "Low"),
                MakeRule(Actions("TurnL")),
                MakeRule(Actions("Fwd")));

            sim.Step();

            Assert.Equal(3, sim.RobotRow);
            Assert.Equal(3, sim.RobotCol);
            Assert.Equal(0, sim.DirRow);
            Assert.Equal(-1, sim.DirCol);
            Assert.Equal(49, sim.Battery);
        }

        [Fact]
        public void NoRuleMatches_CostsOne_AndCountsStep()
        {
            GameSimulation sim = NewSim(MakeRule(Actions("Fwd"), cell: "Base"));

            sim.Step();

            Assert.Equal(49, sim.Battery);
            Assert.Equal(1, sim.Steps);
            Assert.Equal(3, sim.RobotRow);
            Assert.False(sim.IsEnded);
        }

        [Fact]
        public void FireTimeout_EndsOnHundredthCycle()
        {
            // Sobre la base, cada giro deja la batería en 49: sobrevive hasta el límite del fuego.
            GameSimulation sim = NewSim(
                MakeRule(Actions("TurnL"), cell: "Base"),
                MakeRule(Actions("TurnL", "TurnL", "Fwd")));

            for (int i = 0; i < 99; i++)
            {
                sim.Step();
                Assert.False(sim.IsEnded);
            }
            Assert.Equal(99, sim.Steps);

            sim.Step();

            Assert.True(sim.IsEnded);
            Assert.Equal(99, sim.Steps);
            Assert.Equal(EndReason.FireTimeout, sim.EndReason);
        }

        [Fact]
        public void FireTimeout_ResetsAfterPutOut()
        {
            GameSimulation sim = NewSim(MakeRule(Actions()));
            GoInFrontOfInitialFire(sim);

            for (int i = 0; i < 99; i++)
            {
                sim.Step();
            }
            Assert.False(sim.IsEnded);

            sim.ApplyManual("Put Out");

            for (int i = 0; i < 99; i++)
            {
                sim.Step();
                Assert.False(sim.IsEnded);
            }
            Assert.Equal(198, sim.Steps);

            sim.Step();

            Assert.True(sim.IsEnded);
            Assert.Equal(198, sim.Steps);
            Assert.Equal(EndReason.FireTimeout, sim.EndReason);
        }

        [Fact]
        public void MaxSteps_EndsAsMissionComplete()
        {
            GameSimulation sim = new GameSimulation(Rules(MakeRule(Actions())), Seed, 50);

            sim.RunToEnd();

            Assert.True(sim.IsEnded);
            Assert.Equal(50, sim.Steps);
            Assert.Equal(EndReason.MissionComplete, sim.EndReason);
        }

        [Fact]
        public void Rule_StopsWhenRobotDies()
        {
            // Muerte por fuego: la segunda acción no se ejecuta.
            GameSimulation sim = NewSim(MakeRule(Actions("Fwd", "TurnL")));
            GoInFrontOfInitialFire(sim);
            int before = sim.Battery;

            sim.Step();

            Assert.Equal(EndReason.WalkedIntoFire, sim.EndReason);
            Assert.Equal(-1, sim.DirRow);
            Assert.Equal(0, sim.DirCol);
            Assert.Equal(before, sim.Battery);

            // Muerte por batería: tampoco se ejecuta la segunda acción.
            sim = NewSim(MakeRule(Actions("TurnL", "TurnL")));
            DrainWithTurns(sim, 1);
            int dirRow = sim.DirRow;
            int dirCol = sim.DirCol;

            sim.Step();

            Assert.Equal(EndReason.Battery, sim.EndReason);
            Assert.Equal(0, sim.Battery);
            Assert.Equal(-dirCol, sim.DirRow);
            Assert.Equal(dirRow, sim.DirCol);
        }

        [Fact]
        public void SameSeedAndRules_GiveIdenticalResult()
        {
            Rule[] rules = Rules(
                MakeRule(Actions("Put Out"), light: "Bright"),
                MakeRule(Actions("Fwd"), temp: "Up", prox: "Obstacle No"),
                MakeRule(Actions("TurnL")));

            List<string> first = Trace(new GameSimulation(rules, Seed));
            List<string> second = Trace(new GameSimulation(rules, Seed));

            Assert.Equal(first, second);

            // La prueba solo vale si el generador ha llegado a usarse.
            GameSimulation played = new GameSimulation(rules, Seed);
            played.RunToEnd();
            Assert.True(played.Fires >= 2, "Fuegos apagados: " + played.Fires);
        }

        private static List<string> Trace(GameSimulation sim)
        {
            List<string> trace = new List<string>();
            while (!sim.IsEnded)
            {
                sim.Step();
                trace.Add(string.Join(",",
                    sim.Steps, sim.Fires, sim.Battery, sim.BatteryState, sim.SensorTemp, sim.SensorLight,
                    sim.SensorProx, sim.SensorCell, sim.RobotRow, sim.RobotCol, sim.DirRow, sim.DirCol,
                    sim.FireRow, sim.FireCol, sim.EndReason));
            }
            return trace;
        }

        [Fact]
        public void DeterministicRandom_KnownSequence()
        {
            DeterministicRandom random = new DeterministicRandom(12345);

            int[] values = new int[5];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = random.Next(8);
            }

            Assert.Equal(new[] { 2, 7, 0, 2, 3 }, values);
        }

        [Theory]
        [InlineData(1, 5, 5L)]
        [InlineData(6, 300, 1800L)]
        [InlineData(12, 300, 3600L)]
        [InlineData(12, 600, 7200L)]
        [InlineData(0, 300, 0L)]
        [InlineData(0, 0, 0L)]
        public void Score_Points(int fires, int steps, long expected)
        {
            Assert.Equal(expected, Score.Points(fires, steps));
        }

        // Comprobaciones añadidas a la lista del plan: comportamiento que el plan exige y que ninguna
        // de las pruebas anteriores cubre.

        [Fact]
        public void DeterministicRandom_SeedZero_IsReplaced()
        {
            DeterministicRandom random = new DeterministicRandom(0);

            bool anyNonZero = false;
            for (int i = 0; i < 20; i++)
            {
                anyNonZero |= random.Next(8) != 0;
            }

            Assert.True(anyNonZero);
        }

        [Fact]
        public void BatteryDeath_EndsOnSameStep()
        {
            GameSimulation sim = NewSim();

            for (int i = 0; i < 49; i++)
            {
                sim.Step();
            }
            Assert.False(sim.IsEnded);

            sim.Step();

            Assert.True(sim.IsEnded);
            Assert.Equal(50, sim.Steps);
            Assert.Equal(0, sim.Battery);
            Assert.Equal(EndReason.Battery, sim.EndReason);
        }

        [Fact]
        public void DeathOnLastStep_ReportsTheDeath()
        {
            GameSimulation sim = new GameSimulation(Rules(), Seed, 50);

            sim.RunToEnd();

            Assert.Equal(50, sim.Steps);
            Assert.Equal(EndReason.Battery, sim.EndReason);
        }

        [Fact]
        public void Step_AfterEnd_DoesNothing()
        {
            GameSimulation sim = new GameSimulation(Rules(MakeRule(Actions("TurnL"))), Seed, 1);
            sim.RunToEnd();
            int battery = sim.Battery;

            sim.Step();
            sim.ApplyManual("TurnL");

            Assert.Equal(1, sim.Steps);
            Assert.Equal(battery, sim.Battery);
        }

        [Fact]
        public void ApplyManual_DoesNotCountStepsOrAdvanceFireTimeout()
        {
            GameSimulation sim = NewSim(MakeRule(Actions()));

            for (int i = 0; i < 20; i++)
            {
                sim.ApplyManual("TurnL");
            }
            Assert.Equal(0, sim.Steps);

            for (int i = 0; i < 99; i++)
            {
                sim.Step();
            }
            Assert.False(sim.IsEnded);
        }

        [Fact]
        public void Sensors_FollowRobot()
        {
            GameSimulation sim = NewSim();

            // Mirando hacia abajo desde (3,3): la base está delante y el fuego detrás.
            Manual(sim, "TurnR", "TurnR");
            Assert.Equal("Dark", sim.SensorLight);
            Assert.Equal("Down", sim.SensorTemp);

            // Sobre una celda normal.
            sim = NewSim();
            Manual(sim, "TurnL", "Fwd");
            Assert.Equal("Normal", sim.SensorCell);

            // Mirando fuera del tablero: proximidad sí y luz normal.
            sim = NewSim();
            Manual(sim, "Fwd", "Fwd", "Fwd");
            Assert.Equal("Obstacle Yes", sim.SensorProx);
            Assert.Equal("Normal", sim.SensorLight);
        }

        [Fact]
        public void Rule_ConditionsMatchEachSensor()
        {
            // Estado inicial: High, Up, Dark, Obstacle No, Black.
            Rule full = MakeRule(Actions("TurnL"), "High", "Up", "Dark", "Obstacle No", "Black");
            GameSimulation sim = NewSim(full);
            sim.Step();
            Assert.Equal(0, sim.DirRow);

            string[] wrong = { "Medium", "Equal", "Bright", "Obstacle Yes", "Base" };
            for (int i = 0; i < wrong.Length; i++)
            {
                string[] values = { "High", "Up", "Dark", "Obstacle No", "Black" };
                values[i] = wrong[i];
                sim = NewSim(MakeRule(Actions("TurnL"), values[0], values[1], values[2], values[3], values[4]));

                sim.Step();

                Assert.Equal(-1, sim.DirRow);
            }
        }
    }
}
