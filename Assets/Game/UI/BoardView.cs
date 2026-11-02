using RobotBombero.Simulation;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>
    /// Tablero 8 x 8 hecho con elementos. Crea las 64 celdas una vez y, en cada paso, solo toca las que cambiaron
    /// (a 50 pasos por segundo no se puede recomponer todo). Siempre cuadrado: lo ajusta Fit.
    /// </summary>
    public sealed class BoardView
    {
        public const int Size = 8;

        private static readonly string[] StateClasses = { "cell--free", "cell--fire", "cell--dark", "cell--obstacle", "cell--base" };

        private readonly VisualElement _frame;
        private readonly VisualElement[] _cells = new VisualElement[Size * Size];
        private readonly VisualElement[] _icons = new VisualElement[Size * Size];
        private readonly CellState?[] _states = new CellState?[Size * Size];
        private readonly VisualElement _pointer;
        private int _robot = -1;

        /// <summary>Celdas que se han vuelto a pintar desde el principio; sirve para comprobar que solo se toca lo que cambia.</summary>
        public int CellUpdates { get; private set; }
        private int _direction = -1;

        public BoardView(VisualElement frame, VisualElement grid)
        {
            _frame = frame;
            for (int i = 0; i < _cells.Length; i++)
            {
                VisualElement cell = UiKit.Box("cell");
                VisualElement icon = UiKit.Box("cell__icon");
                icon.pickingMode = PickingMode.Ignore;
                cell.Add(icon);
                grid.Add(cell);
                _cells[i] = cell;
                _icons[i] = icon;
            }

            // Marca de dirección: un elemento que cubre la celda del robot y gira; su punta apunta al lado de delante.
            _pointer = UiKit.Box("cell__pointer");
            _pointer.pickingMode = PickingMode.Ignore;
            _pointer.Add(UiKit.Box("cell__pointer-tip"));
        }

        /// <summary>Olvida lo dibujado: el siguiente Render pinta todo.</summary>
        public void Reset()
        {
            for (int i = 0; i < _states.Length; i++)
            {
                // Sin esto, una celda que cambie de estado tras reiniciar conservaría la clase de la partida anterior.
                foreach (string c in StateClasses) _cells[i].RemoveFromClassList(c);
                _icons[i].RemoveFromClassList("cell__icon--fire");
                _states[i] = null;
            }
            if (_robot >= 0) _icons[_robot].RemoveFromClassList("cell__icon--robot");
            _pointer.RemoveFromHierarchy();
            _robot = -1;
            _direction = -1;
        }

        /// <summary>Deja el tablero cuadrado dentro del espacio disponible, en múltiplos de 8 para que no haya costuras.</summary>
        public void Fit(float width, float height)
        {
            float side = System.Math.Min(width, height);
            int px = (int)(side / Size) * Size;
            if (px < Size) px = Size;
            _frame.style.width = px;
            _frame.style.height = px;
        }

        public void Render(GameSimulation sim)
        {
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    int i = row * Size + col;
                    CellState state = sim.GetCell(row, col);
                    if (_states[i] == state) continue;
                    if (_states[i].HasValue) _cells[i].RemoveFromClassList(StateClasses[(int)_states[i].Value]);
                    _cells[i].AddToClassList(StateClasses[(int)state]);
                    _icons[i].EnableInClassList("cell__icon--fire", state == CellState.Fire);
                    _states[i] = state;
                    CellUpdates++;
                }
            }

            int robot = sim.RobotRow * Size + sim.RobotCol;
            if (robot != _robot)
            {
                if (_robot >= 0) _icons[_robot].RemoveFromClassList("cell__icon--robot");
                _icons[robot].AddToClassList("cell__icon--robot");
                _pointer.RemoveFromHierarchy();
                _cells[robot].Add(_pointer);
                _robot = robot;
                CellUpdates++;
            }

            int direction = DirectionKey(sim.DirRow, sim.DirCol);
            if (direction != _direction)
            {
                _direction = direction;
                _pointer.style.rotate = new Rotate(new Angle(DegreesFor(direction)));
            }
        }

        private static int DirectionKey(int dirRow, int dirCol)
        {
            return (dirRow + 1) * 3 + (dirCol + 1);
        }

        // Arriba (-1, 0) no gira; derecha (0, 1) 90; abajo (1, 0) 180; izquierda (0, -1) 270.
        private static float DegreesFor(int key)
        {
            switch (key)
            {
                case 5: return 90f;
                case 7: return 180f;
                case 3: return 270f;
                default: return 0f;
            }
        }
    }
}
