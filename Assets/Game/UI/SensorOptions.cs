using System.Linq;

namespace RobotBombero.Game
{
    /// <summary>Qué opciones de cada sensor se ofrecen al jugador. El código de la simulación no cambia.</summary>
    public static class SensorOptions
    {
        // "Igual" (temp) significa que no hay fuego, y siempre hay uno, así que nunca se cumple. Se oculta del editor y del
        // tutorial solo desde aquí; pasar a true lo devuelve (por si una versión futura tiene momentos sin fuego).
        public static readonly bool ShowTempEqual = false;

        public static bool IsOffered(string key, string value)
        {
            return ShowTempEqual || !(key == "temp" && value == "Equal");
        }

        public static string[] Offered(string key)
        {
            return Labels.ConditionValues(key).Where(v => IsOffered(key, v)).ToArray();
        }
    }
}
