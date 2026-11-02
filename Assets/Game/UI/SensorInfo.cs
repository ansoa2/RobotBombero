using System.Collections.Generic;
using System.Linq;
using RobotBombero.Simulation;

namespace RobotBombero.Game
{
    public sealed class SensorOption
    {
        public string Value;
        public string Label;
        public string Meaning;
    }

    /// <summary>
    /// Qué mide cada sensor y qué significa cada opción, tal como los lee GameSimulation. Es la única fuente de este
    /// texto: la ventana de información del editor y la página de sensores del tutorial leen de aquí.
    /// </summary>
    public sealed class SensorInfo
    {
        public string Key;
        public string Title;
        public string Measures;
        public SensorOption[] Options;

        private static readonly string[] Keys = Labels.ConditionKeys;
        private static Dictionary<string, SensorInfo> _all;

        public static IEnumerable<SensorInfo> All
        {
            get { return Keys.Select(For); }
        }

        public static SensorInfo For(string key)
        {
            if (_all == null) Build();
            SensorInfo info;
            return _all.TryGetValue(key, out info) ? info : null;
        }

        private static void Build()
        {
            var all = new Dictionary<string, SensorInfo>();
            all["battery"] = Make("battery",
                "La carga que le queda al robot. Empieza con " + SimConfig.InitialBattery + " y se recarga en la base.",
                "Alta", "Más de " + SimConfig.BatteryHighAbove + " de batería.",
                "Media", "De " + SimConfig.BatteryMediumFrom + " a " + SimConfig.BatteryHighAbove + " de batería.",
                "Baja", "Menos de " + SimConfig.BatteryMediumFrom + " de batería. Con 0 el robot se apaga y la partida termina.");
            all["temp"] = Make("temp",
                "Si el fuego está más cerca o más lejos después de avanzar una casilla. Solo mira en el eje en que mira el robot (la fila si mira a los lados, la columna si mira arriba o abajo), no en diagonal.",
                "Sube", "Al avanzar una casilla, el robot queda más cerca del fuego en ese eje.",
                "Baja", "Al avanzar una casilla, el robot no se acerca: se aleja o queda igual de lejos en ese eje.",
                "Igual", "No hay fuego en el tablero. Como siempre hay un fuego, en la práctica nunca ocurre.");
            all["light"] = Make("light",
                "Lo que hay en la casilla de delante del robot.",
                "Brillante", "Delante hay fuego. Avanzar ahora acabaría la partida.",
                "Oscura", "Delante hay una casilla oscura de la cruz, o la base.",
                "Normal", "Cualquier otra cosa: una casilla normal o con obstáculo, y también cuando delante está el borde del tablero.");
            all["prox"] = Make("prox",
                "Si el paso de delante está bloqueado.",
                "Con obstáculo", "Delante hay un obstáculo o el borde del tablero. Avanzar no mueve al robot pero gasta batería.",
                "Sin obstáculo", "Delante hay paso libre (puede haber fuego o una casilla oscura).");
            all["cell"] = Make("cell",
                "La casilla en la que está el robot ahora mismo.",
                "Base", "El robot está sobre la base, donde se recarga.",
                "Negra", "El robot está sobre una casilla oscura de la cruz (la base no cuenta como negra).",
                "Normal", "Cualquier otra casilla.");
            _all = all;
        }

        // pares (texto, significado) de las opciones de cada sensor, sin "Cualquiera", que se añade igual en todos.
        private static SensorInfo Make(string key, string measures, params string[] labelAndMeaning)
        {
            var options = new List<SensorOption>();
            foreach (string value in Labels.ConditionValues(key))
            {
                if (value == "Anyone")
                {
                    options.Add(new SensorOption { Value = value, Label = Labels.Value(value), Meaning = "No importa el valor de este sensor: acepta cualquier valor." });
                    continue;
                }
                string label = Labels.Value(value);
                string meaning = null;
                for (int i = 0; i + 1 < labelAndMeaning.Length; i += 2)
                    if (labelAndMeaning[i] == label) meaning = labelAndMeaning[i + 1];
                options.Add(new SensorOption { Value = value, Label = label, Meaning = meaning });
            }
            return new SensorInfo { Key = key, Title = Labels.Field(key), Measures = measures, Options = options.ToArray() };
        }
    }
}
