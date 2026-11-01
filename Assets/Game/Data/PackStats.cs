using System;
using RobotBombero.Simulation;

namespace RobotBombero.Game
{
    /// <summary>Estadísticas locales de un pack. Se guardan en el dispositivo, nunca se exportan.</summary>
    [Serializable]
    public class PackStats
    {
        public int games;
        public long bestPoints;
        /// <summary>Menor número de pasos de una partida (0 si aún no hay partidas).</summary>
        public int bestSteps;
        /// <summary>Mayor número de fuegos apagados en una partida.</summary>
        public int bestFires;
        public long totalSteps;
        public long totalFires;

        public void Add(int steps, int fires)
        {
            if (steps < 0) steps = 0;
            if (fires < 0) fires = 0;
            if (games == 0 || steps < bestSteps) bestSteps = steps;
            if (fires > bestFires) bestFires = fires;
            games++;
            totalSteps += steps;
            totalFires += fires;
        }

        public float AverageSteps { get { return games == 0 ? 0f : (float)totalSteps / games; } }
        public float AverageFires { get { return games == 0 ? 0f : (float)totalFires / games; } }
    }

    /// <summary>Un pack guardado en este dispositivo. serverPackId es 0 mientras no se haya subido.</summary>
    [Serializable]
    public class LocalPack
    {
        public string localId;
        public long serverPackId;
        public RulePackFile content;
        public PackStats stats;
    }

    [Serializable]
    public class Profile
    {
        public string playerName;
        public string playerKey;
        public long playerId;
    }
}
