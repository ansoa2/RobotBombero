using System;

namespace RobotBombero.Simulation
{
    /// <summary>
    /// Regla del robot. Valores: battery Anyone, High, Medium, Low; temp Anyone, Up, Down, Equal;
    /// light Anyone, Normal, Bright, Dark; prox Anyone, Obstacle Yes, Obstacle No;
    /// cell Anyone, Base, Normal, Black; actions Fwd, Put Out, TurnL, TurnR.
    /// </summary>
    [Serializable]
    public class Rule
    {
        public string name;
        public string battery;
        public string temp;
        public string light;
        public string prox;
        public string cell;
        public string[] actions;
    }

    [Serializable]
    public class RulePackFile
    {
        public int formatVersion;
        public string name;
        public Rule[] rules;
    }

    public enum CellState
    {
        Free,
        Fire,
        Dark,
        Obstacle,
        Base
    }

    public enum EndReason
    {
        None,
        Battery,
        WalkedIntoFire,
        FireTimeout,
        MissionComplete
    }

    public static class SimConfig
    {
        public const int DefaultMaxSteps = 5000;
        public const int FireTimeout = 100;
        public const int MaxActionsPerRule = 5;
        public const int BoardSize = 8;
        public const int InitialBattery = 50;
        public const int MoveCost = 1;
        public const int TurnCost = 1;
        public const int PutOutCost = 5;
        /// <summary>Lo que pierde un ciclo en el que ninguna regla se cumple.</summary>
        public const int NoRuleCost = 1;
        /// <summary>Batería Alta: más que esto.</summary>
        public const int BatteryHighAbove = 30;
        /// <summary>Batería Media: desde esto hasta BatteryHighAbove; por debajo, Baja.</summary>
        public const int BatteryMediumFrom = 10;
    }
}
