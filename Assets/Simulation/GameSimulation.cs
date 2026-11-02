using System;

namespace RobotBombero.Simulation
{
    /// <summary>
    /// Port de Robot.cs y MainView.NextStep sin dependencias de Unity. Los sensores se exponen con los
    /// mismos textos que usan las condiciones de las reglas.
    /// Coordenadas: fila = pos_y del original y columna = pos_x.
    /// </summary>
    public class GameSimulation
    {
        private const int BoardSize = SimConfig.BoardSize;
        private const int InitialBattery = SimConfig.InitialBattery;
        private const int PutOutCost = SimConfig.PutOutCost;

        private const string Anyone = "Anyone";
        private const string High = "High";
        private const string Medium = "Medium";
        private const string Low = "Low";
        private const string Up = "Up";
        private const string Down = "Down";
        private const string Normal = "Normal";
        private const string Bright = "Bright";
        private const string Dark = "Dark";
        private const string ObstacleYes = "Obstacle Yes";
        private const string ObstacleNo = "Obstacle No";
        private const string BaseCell = "Base";
        private const string Black = "Black";

        private readonly Rule[] _rules;
        private readonly DeterministicRandom _random;
        private readonly int _maxSteps;
        private readonly CellState[,] _board = new CellState[BoardSize, BoardSize];

        private int _cyclesSinceLastFire;

        public int Steps { get; private set; }
        public int Fires { get; private set; }
        public int Battery { get; private set; }
        public string BatteryState { get; private set; }
        public string SensorTemp { get; private set; }
        public string SensorLight { get; private set; }
        public string SensorProx { get; private set; }
        public string SensorCell { get; private set; }
        public int RobotRow { get; private set; }
        public int RobotCol { get; private set; }
        public int DirRow { get; private set; }
        public int DirCol { get; private set; }
        public int FireRow { get; private set; }
        public int FireCol { get; private set; }
        public bool IsEnded { get; private set; }
        public EndReason EndReason { get; private set; }

        public GameSimulation(Rule[] rules, uint seed, int maxSteps = SimConfig.DefaultMaxSteps)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }
            if (maxSteps <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSteps));
            }

            _rules = rules;
            _random = new DeterministicRandom(seed);
            _maxSteps = maxSteps;

            BuildBoard();

            Battery = InitialBattery;
            RobotRow = 3;
            RobotCol = 3;
            DirRow = -1;
            DirCol = 0;

            UpdateSensors();
        }

        public CellState GetCell(int row, int col)
        {
            return _board[row, col];
        }

        /// <summary>Un ciclo de MainView.NextStep.</summary>
        public void Step()
        {
            if (IsEnded)
            {
                return;
            }

            _cyclesSinceLastFire++;
            if (_cyclesSinceLastFire >= SimConfig.FireTimeout)
            {
                // Como en el original, este ciclo ya no cuenta como paso.
                End(EndReason.FireTimeout);
                return;
            }

            Rule rule = FindFirstMatchingRule();
            if (rule == null)
            {
                Battery -= SimConfig.NoRuleCost;
            }
            else
            {
                foreach (string action in rule.actions)
                {
                    Execute(action);
                    if (IsEnded)
                    {
                        break;
                    }
                }
            }

            Steps++;
            UpdateSensors();

            if (Steps >= _maxSteps)
            {
                End(EndReason.MissionComplete);
            }
        }

        public void RunToEnd()
        {
            while (!IsEnded)
            {
                Step();
            }
        }

        /// <summary>
        /// Los botones manuales del original: ejecutan la acción sin contar paso ni avanzar el límite
        /// del fuego. Son para el modo de prueba, sin puntuación.
        /// </summary>
        public void ApplyManual(string action)
        {
            if (IsEnded)
            {
                return;
            }

            Execute(action);
        }

        private void BuildBoard()
        {
            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    _board[row, col] = row == 4 || col == 3 ? CellState.Dark : CellState.Free;
                }
            }

            _board[4, 3] = CellState.Base;
            _board[1, 5] = CellState.Obstacle;
            _board[5, 1] = CellState.Obstacle;
            _board[6, 6] = CellState.Obstacle;

            FireRow = 1;
            FireCol = 2;
            _board[FireRow, FireCol] = CellState.Fire;
        }

        private Rule FindFirstMatchingRule()
        {
            foreach (Rule rule in _rules)
            {
                if (Matches(rule.battery, BatteryState)
                    && Matches(rule.temp, SensorTemp)
                    && Matches(rule.light, SensorLight)
                    && Matches(rule.prox, SensorProx)
                    && Matches(rule.cell, SensorCell))
                {
                    return rule;
                }
            }
            return null;
        }

        // Un valor desconocido no coincide nunca, igual que los switch sin caso del original.
        private static bool Matches(string condition, string sensor)
        {
            return condition == Anyone || condition == sensor;
        }

        private void Execute(string action)
        {
            switch (action)
            {
                case "Fwd":
                    Forward();
                    break;
                case "Put Out":
                    PutOut();
                    break;
                case "TurnL":
                    TurnLeft();
                    break;
                case "TurnR":
                    TurnRight();
                    break;
            }

            UpdateSensors();
        }

        private void Forward()
        {
            if (SensorLight == Bright)
            {
                End(EndReason.WalkedIntoFire);
                return;
            }

            if (SensorProx == ObstacleYes)
            {
                Battery -= SimConfig.MoveCost;
                return;
            }

            Battery -= SimConfig.MoveCost;
            RechargeIfOnBase();
            RobotRow += DirRow;
            RobotCol += DirCol;
        }

        private void TurnLeft()
        {
            RechargeIfOnBase();
            int previousDirCol = DirCol;
            DirCol = DirRow;
            DirRow = -previousDirCol;
            Battery -= SimConfig.TurnCost;
        }

        private void TurnRight()
        {
            RechargeIfOnBase();
            int previousDirCol = DirCol;
            DirCol = -DirRow;
            DirRow = previousDirCol;
            Battery -= SimConfig.TurnCost;
        }

        private void PutOut()
        {
            Battery -= PutOutCost;

            int row = RobotRow + DirRow;
            int col = RobotCol + DirCol;
            if (IsOnBoard(row, col) && _board[row, col] == CellState.Fire)
            {
                Fires++;
                _board[row, col] = CellState.Free;
                InsertFire();
            }
        }

        private void InsertFire()
        {
            _cyclesSinceLastFire = 0;

            while (true)
            {
                int row = _random.Next(BoardSize);
                int col = _random.Next(BoardSize);

                if (_board[row, col] == CellState.Free && row != RobotRow && col != RobotCol)
                {
                    _board[row, col] = CellState.Fire;
                    FireRow = row;
                    FireCol = col;
                    return;
                }
            }
        }

        private void RechargeIfOnBase()
        {
            if (SensorCell == BaseCell)
            {
                Battery = InitialBattery;
            }
        }

        private void UpdateSensors()
        {
            SensorTemp = ReadTemp();
            SensorLight = ReadLight();
            SensorProx = ReadProx();
            SensorCell = ReadCell();

            if (Battery > SimConfig.BatteryHighAbove)
            {
                BatteryState = High;
            }
            else if (Battery >= SimConfig.BatteryMediumFrom)
            {
                BatteryState = Medium;
            }
            else
            {
                BatteryState = Low;
            }

            if (Battery <= 0)
            {
                End(EndReason.Battery);
            }
        }

        // Siempre hay un fuego activo, así que Equal no llega a darse aunque las reglas puedan pedirlo.
        private string ReadTemp()
        {
            bool closer = DirCol != 0
                ? Math.Abs(FireCol - RobotCol) > Math.Abs(FireCol - (RobotCol + DirCol))
                : Math.Abs(FireRow - RobotRow) > Math.Abs(FireRow - (RobotRow + DirRow));
            return closer ? Up : Down;
        }

        private string ReadLight()
        {
            int row = RobotRow + DirRow;
            int col = RobotCol + DirCol;
            if (!IsOnBoard(row, col))
            {
                return Normal;
            }

            switch (_board[row, col])
            {
                case CellState.Fire:
                    return Bright;
                case CellState.Dark:
                case CellState.Base:
                    return Dark;
                default:
                    return Normal;
            }
        }

        private string ReadProx()
        {
            int row = RobotRow + DirRow;
            int col = RobotCol + DirCol;
            return !IsOnBoard(row, col) || _board[row, col] == CellState.Obstacle ? ObstacleYes : ObstacleNo;
        }

        private string ReadCell()
        {
            switch (_board[RobotRow, RobotCol])
            {
                case CellState.Dark:
                    return Black;
                case CellState.Base:
                    return BaseCell;
                default:
                    return Normal;
            }
        }

        private static bool IsOnBoard(int row, int col)
        {
            return row >= 0 && row < BoardSize && col >= 0 && col < BoardSize;
        }

        // La primera causa manda: una muerte en el último paso no se convierte en misión superada.
        private void End(EndReason reason)
        {
            if (IsEnded)
            {
                return;
            }

            IsEnded = true;
            EndReason = reason;
        }
    }
}
