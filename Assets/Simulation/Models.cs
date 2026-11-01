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
    }
}
