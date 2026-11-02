using System.Collections.Generic;
using System.Linq;
using RobotBombero.Simulation;

namespace RobotBombero.Game
{
    public sealed class TutorialBlock
    {
        /// <summary>Título corto del bloque; puede ser nulo.</summary>
        public string Heading;
        public string[] Lines;
    }

    public sealed class TutorialPage
    {
        public string Title;
        public TutorialBlock[] Blocks;
    }

    /// <summary>
    /// El texto de "Cómo se juega". Cada dato sale de GameSimulation (costes, límites y sensores en SimConfig y SensorInfo),
    /// para que el tutorial no se desfase de lo que de verdad hace el robot.
    /// </summary>
    public static class TutorialPages
    {
        private static IList<TutorialPage> _all;

        public static IList<TutorialPage> All
        {
            get { return _all ?? (_all = Build()); }
        }

        private static TutorialBlock Block(string heading, params string[] lines)
        {
            return new TutorialBlock { Heading = heading, Lines = lines };
        }

        private static TutorialPage Page(string title, params TutorialBlock[] blocks)
        {
            return new TutorialPage { Title = title, Blocks = blocks };
        }

        private static IList<TutorialPage> Build()
        {
            string turnL = Labels.Value("TurnL");
            string turnR = Labels.Value("TurnR");
            string forward = Labels.Value("Fwd");
            string putOut = Labels.Value("Put Out");
            string anyone = Labels.Value("Anyone");

            return new List<TutorialPage>
            {
                Page("La meta",
                    Block(null,
                        "En este juego no controlas al robot directamente. Tú escribes reglas y el robot las sigue solo.",
                        "Con buenas reglas, el robot sobrevive y apaga fuegos. Tu trabajo es pensar qué debe hacer en cada situación."),
                    Block("Cuánto vale una partida",
                        "Cada fuego apagado y cada paso que el robot aguanta con vida te dan puntos.")),

                Page("Cómo funcionan las reglas",
                    Block("Cinco condiciones y varias acciones",
                        "Una regla dice: si se cumplen estas cinco condiciones (" + string.Join(", ", Labels.ConditionKeys.Select(Labels.Field).ToArray()) + "), haz estas acciones.",
                        "Cada regla puede llevar hasta " + SimConfig.MaxActionsPerRule + " acciones, y el robot las hace en orden."),
                    Block("Qué regla se aplica",
                        "En cada ciclo el robot lee sus sensores y recorre las reglas de arriba abajo. Ejecuta la primera cuyas cinco condiciones se cumplen.",
                        "Las reglas de arriba ganan a las de abajo. Pon arriba las más específicas y abajo las más generales.",
                        "Si ninguna regla se cumple, el robot no hace nada y pierde " + SimConfig.NoRuleCost + " de batería."),
                    Block(anyone,
                        anyone + " acepta cualquier valor: esa condición siempre se cumple. Una regla con todo en " + anyone + " se cumple siempre.")),

                Page("El tablero",
                    Block(null,
                        "El tablero tiene " + SimConfig.BoardSize + " x " + SimConfig.BoardSize + " casillas.",
                        "Una cruz de casillas oscuras lo atraviesa, y la base está en el centro de la cruz. Allí el robot se recarga.",
                        "Hay obstáculos repartidos por el tablero: el robot no puede pasar por ellos.",
                        "Siempre hay un solo fuego a la vez. Cuando el robot lo apaga, aparece otro fuego en otra casilla libre.",
                        "El robot empieza con " + SimConfig.InitialBattery + " de batería.")),

                SensorsPage(),

                Page("Las acciones",
                    Block(forward,
                        "Cuesta " + SimConfig.MoveCost + " de batería y mueve al robot una casilla hacia donde mira.",
                        "Si delante hay un obstáculo o el borde del tablero, no se mueve, pero gasta la batería igualmente.",
                        "Si delante hay fuego, el robot avanza hacia él y la partida termina."),
                    Block(turnL + " y " + turnR,
                        "Cada giro cuesta " + SimConfig.TurnCost + " de batería. El robot gira un cuarto de vuelta y se queda en su casilla."),
                    Block(putOut,
                        "Cuesta " + SimConfig.PutOutCost + " de batería. Apaga el fuego solo si está justo en la casilla de delante; si no, la batería se gasta sin efecto."),
                    Block("Recargar",
                        "Si el robot se mueve o gira estando sobre la base, la batería se recarga hasta " + SimConfig.InitialBattery + " (un giro gasta " + SimConfig.TurnCost + " justo después). Un Avanzar bloqueado por un obstáculo o por el borde no recarga, ni tampoco apagar fuego o quedarse sin regla.")),

                Page("Cómo termina la partida",
                    Block(null,
                        "La partida termina cuando ocurre una de estas cuatro cosas:",
                        "1. Se acaba la batería: llega a 0.",
                        "2. El robot avanza hacia un fuego.",
                        "3. Pasan " + SimConfig.FireTimeout + " ciclos sin apagar un fuego.",
                        "4. Llega a " + Labels.Number(SimConfig.DefaultMaxSteps) + " pasos: es la misión superada, el mejor final.")),

                Page("Puntos y clasificación",
                    Block("Cómo se puntúa",
                        "Puntos = fuegos x pasos. Es decir: fuegos apagados multiplicados por los pasos que el robot aguantó.",
                        "Apagar muchos fuegos y durar mucho tiempo da la mejor puntuación."),
                    Block("Clasificación",
                        "La clasificación global muestra una fila por jugador.",
                        "Las partidas sin conexión no cuentan para la clasificación.")),

                Page("Tu primera regla",
                    Block("Una idea para empezar",
                        "Crea una regla con Luz en Brillante y todo lo demás en " + anyone + ". Como acción, pon " + putOut + ".",
                        "Luz Brillante significa que hay fuego justo delante, así que el robot lo apaga en cuanto lo tiene delante.",
                        "Esa regla sola no mueve al robot. Añade debajo otras reglas para que avance y gire hasta encontrar el fuego, y vigila la batería.",
                        "Prueba, mira cómo se comporta y ajusta las reglas."))
            };
        }

        private static TutorialPage SensorsPage()
        {
            var blocks = new List<TutorialBlock>();
            blocks.Add(Block(null, "El robot usa cinco sensores. Cada condición de una regla mira uno de ellos. En el editor, el botón con una i junto a cada sensor te lo recuerda."));
            foreach (SensorInfo info in SensorInfo.All)
            {
                var lines = new List<string> { info.Measures };
                lines.AddRange(info.Options.Select(o => o.Label + ": " + o.Meaning));
                blocks.Add(new TutorialBlock { Heading = info.Title, Lines = lines.ToArray() });
            }
            return new TutorialPage { Title = "Los sensores", Blocks = blocks.ToArray() };
        }
    }
}
