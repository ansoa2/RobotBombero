using System.Collections.Generic;
using System.Globalization;
using RobotBombero.Simulation;

namespace RobotBombero.Game
{
    /// <summary>
    /// Todos los textos en español de los valores de la simulación. Los valores guardados no cambian
    /// (Anyone, Fwd...): solo se traducen al mostrarlos. Es el único sitio donde se definen.
    /// </summary>
    public static class Labels
    {
        public static readonly string[] ConditionKeys = { "battery", "temp", "light", "prox", "cell" };
        public static readonly string[] ActionValues = { "Fwd", "TurnL", "TurnR", "Put Out" };

        private static readonly Dictionary<string, string> FieldTitles = new Dictionary<string, string>
        {
            { "battery", "Batería" },
            { "temp", "Temperatura" },
            { "light", "Luz" },
            { "prox", "Proximidad" },
            { "cell", "Celda" }
        };

        private static readonly Dictionary<string, string[]> Options = new Dictionary<string, string[]>
        {
            { "battery", new[] { "Anyone", "High", "Medium", "Low" } },
            { "temp", new[] { "Anyone", "Up", "Down", "Equal" } },
            { "light", new[] { "Anyone", "Normal", "Bright", "Dark" } },
            { "prox", new[] { "Anyone", "Obstacle Yes", "Obstacle No" } },
            { "cell", new[] { "Anyone", "Base", "Normal", "Black" } }
        };

        private static readonly Dictionary<string, string> Values = new Dictionary<string, string>
        {
            { "Anyone", "Cualquiera" },
            { "High", "Alta" },
            { "Medium", "Media" },
            { "Low", "Baja" },
            { "Up", "Sube" },
            { "Down", "Baja" },
            { "Equal", "Igual" },
            { "Normal", "Normal" },
            { "Bright", "Brillante" },
            { "Dark", "Oscura" },
            { "Obstacle Yes", "Con obstáculo" },
            { "Obstacle No", "Sin obstáculo" },
            { "Base", "Base" },
            { "Black", "Negra" },
            { "Fwd", "Avanzar" },
            { "TurnL", "Girar a la izquierda" },
            { "TurnR", "Girar a la derecha" },
            { "Put Out", "Apagar fuego" }
        };

        private static readonly Dictionary<string, string> Causes = new Dictionary<string, string>
        {
            { "Battery", "Sin batería" },
            { "WalkedIntoFire", "Avanzó hacia el fuego" },
            { "FireTimeout", SimConfig.FireTimeout + " ciclos sin apagar un fuego" },
            { "MissionComplete", "Misión superada: " + Number(SimConfig.DefaultMaxSteps) + " pasos" }
        };

        /// <summary>El texto de por qué una partida no entra en la clasificación; nulo si entra.</summary>
        public static string Unranked(UnrankedReason reason)
        {
            switch (reason)
            {
                case UnrankedReason.Manual:
                    return "Sin puntuación: esta partida usó los controles manuales de prueba, así que no cuenta en la clasificación ni en las estadísticas.";
                case UnrankedReason.Offline:
                    return "Sin conexión: esta partida no entra en la clasificación";
                case UnrankedReason.RateLimited:
                    return "Has alcanzado el límite de 60 partidas por hora. Esta no entra en la clasificación.";
                case UnrankedReason.Expired:
                    return "La partida caducó.";
                case UnrankedReason.Rejected:
                    return "El servidor rechazó la partida.";
                case UnrankedReason.FinishPending:
                    return "Sin conexión: se reintentará al volver a conectar";
                default:
                    return null;
            }
        }

        public static string Field(string key)
        {
            string title;
            return FieldTitles.TryGetValue(key, out title) ? title : key;
        }

        public static string[] ConditionValues(string key)
        {
            return Options[key];
        }

        public static bool TryValue(string value, out string label)
        {
            if (value != null && Values.TryGetValue(value, out label)) return true;
            label = value;
            return false;
        }

        /// <summary>Texto en español de un valor; si no se conoce, el propio valor.</summary>
        public static string Value(string value)
        {
            string label;
            TryValue(value, out label);
            return label;
        }

        public static string Cause(string endReason)
        {
            string text;
            return endReason != null && Causes.TryGetValue(endReason, out text) ? text : endReason;
        }

        /// <summary>Miles con punto, como se escribe en español.</summary>
        public static string Number(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');
        }

        /// <summary>Fuegos por paso con coma decimal y hasta cuatro decimales.</summary>
        public static string Ratio(int fires, int steps)
        {
            if (steps <= 0 || fires <= 0) return "0";
            double ratio = (double)fires / steps;
            return ratio.ToString("0.####", CultureInfo.InvariantCulture).Replace('.', ',');
        }

        /// <summary>Media con punto de millares, coma decimal y un decimal como máximo.</summary>
        public static string Average(float value)
        {
            return value.ToString("#,##0.#", CultureInfo.InvariantCulture).Replace(',', ' ').Replace('.', ',').Replace(' ', '.');
        }

        /// <summary>Nombre de elemento de interfaz para un valor: sin espacios.</summary>
        public static string Slug(string value)
        {
            return value.Replace(' ', '-');
        }
    }
}
