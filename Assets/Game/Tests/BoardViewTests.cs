using System.Linq;
using NUnit.Framework;
using RobotBombero.Simulation;
using UnityEngine.UIElements;

namespace RobotBombero.Game.Tests
{
    /// <summary>El tablero 8 x 8: contenido correcto, siempre cuadrado y sin repintar lo que no cambia.</summary>
    public class BoardViewTests
    {
        private VisualElement _frame;
        private VisualElement _grid;
        private BoardView _board;
        private GameSimulation _sim;

        [SetUp]
        public void SetUp()
        {
            _frame = new VisualElement();
            _grid = new VisualElement();
            _frame.Add(_grid);
            _board = new BoardView(_frame, _grid);
            _sim = new GameSimulation(new[] { TurnRule() }, 42);
        }

        private static Rule TurnRule()
        {
            return new Rule { name = "g", battery = "Anyone", temp = "Anyone", light = "Anyone", prox = "Anyone", cell = "Anyone", actions = new[] { "TurnR" } };
        }

        private VisualElement Cell(int row, int col)
        {
            return _grid[row * BoardView.Size + col];
        }

        [Test]
        public void Board_HasSixtyFourCells()
        {
            Assert.AreEqual(64, _grid.childCount);
        }

        [Test]
        public void Render_DrawsRobot_Fire_Base_Obstacles_AndDarkCells()
        {
            _board.Render(_sim);

            Assert.IsTrue(Cell(3, 3).Q(className: "cell__icon").ClassListContains("cell__icon--robot"));
            Assert.IsTrue(Cell(_sim.FireRow, _sim.FireCol).ClassListContains("cell--fire"));
            Assert.IsTrue(Cell(_sim.FireRow, _sim.FireCol).Q(className: "cell__icon").ClassListContains("cell__icon--fire"));
            Assert.IsTrue(Cell(4, 3).ClassListContains("cell--base"));
            Assert.IsTrue(Cell(1, 5).ClassListContains("cell--obstacle"));
            Assert.IsTrue(Cell(5, 1).ClassListContains("cell--obstacle"));
            Assert.IsTrue(Cell(6, 6).ClassListContains("cell--obstacle"));
            Assert.IsTrue(Cell(4, 0).ClassListContains("cell--dark"));
            Assert.IsTrue(Cell(0, 0).ClassListContains("cell--free"));
        }

        [Test]
        public void Render_WithoutChanges_RepaintsNothing()
        {
            _board.Render(_sim);
            int after = _board.CellUpdates;

            _board.Render(_sim);
            _board.Render(_sim);

            Assert.AreEqual(after, _board.CellUpdates);
        }

        [Test]
        public void Render_AfterOneStep_OnlyTouchesWhatChanged()
        {
            _board.Render(_sim);
            int before = _board.CellUpdates;

            _sim.Step();
            _board.Render(_sim);

            Assert.LessOrEqual(_board.CellUpdates - before, 3, "Un paso mueve como mucho el robot");
        }

        [Test]
        public void Render_RobotMoves_MarkerFollowsIt_AndLeavesNoTrace()
        {
            _board.Render(_sim);

            _sim.ApplyManual("Fwd");
            _board.Render(_sim);

            Assert.IsTrue(Cell(2, 3).Q(className: "cell__icon").ClassListContains("cell__icon--robot"));
            Assert.IsFalse(Cell(3, 3).Q(className: "cell__icon").ClassListContains("cell__icon--robot"));
            Assert.AreEqual(1, _grid.Query(className: "cell__icon--robot").ToList().Count);
        }

        [Test]
        public void Render_PointerTurnsWithDirection()
        {
            _board.Render(_sim);
            VisualElement pointer = Cell(3, 3).Q(className: "cell__pointer");
            Assert.AreEqual(0f, pointer.style.rotate.value.angle.value, 0.01f, "Mirando arriba");

            _sim.ApplyManual("TurnR");
            _board.Render(_sim);

            Assert.AreEqual(90f, pointer.style.rotate.value.angle.value, 0.01f, "Mirando a la derecha");
        }

        [TestCase(300f, 500f, 296f)]
        [TestCase(500f, 300f, 296f)]
        [TestCase(361f, 361f, 360f)]
        public void Fit_KeepsTheBoardSquare_InMultiplesOfEight(float width, float height, float expected)
        {
            _board.Fit(width, height);

            Assert.AreEqual(expected, _frame.style.width.value.value);
            Assert.AreEqual(expected, _frame.style.height.value.value);
        }

        [Test]
        public void Reset_RedrawsEverything_WithoutLeftoverClasses()
        {
            _board.Render(_sim);
            _board.Reset();

            for (int i = 0; i < _grid.childCount; i++)
                Assert.IsFalse(_grid[i].GetClasses().Any(c => c.StartsWith("cell--")), "Clase sobrante tras Reset en la celda " + i);
            Assert.AreEqual(0, _grid.Query(className: "cell__icon--fire").ToList().Count);
            Assert.AreEqual(0, _grid.Query(className: "cell__icon--robot").ToList().Count);

            // Otra partida con el fuego en otro sitio: cada celda debe tener exactamente una clase de estado.
            int before = _board.CellUpdates;
            var other = new GameSimulation(new[] { TurnRule() }, 99);
            other.ApplyManual("TurnL");
            _board.Render(other);

            Assert.AreEqual(64 + 1, _board.CellUpdates - before);
            for (int i = 0; i < _grid.childCount; i++)
                Assert.AreEqual(1, _grid[i].GetClasses().Count(c => c.StartsWith("cell--")), "Celda " + i);
            Assert.AreEqual(1, _grid.Query(className: "cell--fire").ToList().Count);
        }
    }
}
