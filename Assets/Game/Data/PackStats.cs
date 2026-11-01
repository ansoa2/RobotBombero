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
        /// <summary>Pasos de la mejor partida (la de más puntos; 0 si ninguna sumó puntos).</summary>
        public int bestSteps;
        /// <summary>Fuegos apagados en la mejor partida.</summary>
        public int bestFires;
        public long totalSteps;
        public long totalFires;

        public void Add(int steps, int fires)
        {
            if (steps < 0) steps = 0;
            if (fires < 0) fires = 0;
            // La mejor partida es la de más puntos; pasos y fuegos salen de esa misma partida.
            // Con puntos iguales se conserva la anterior; una partida de 0 puntos nunca es la mejor.
            long points = Score.Points(fires, steps);
            if (points > bestPoints)
            {
                bestPoints = points;
                bestSteps = steps;
                bestFires = fires;
            }
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
        /// <summary>Huella y nombre con los que el pack se subió por última vez; si cambian, hay que volver a subirlo.</summary>
        public string serverRulesHash;
        public string serverName;
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
