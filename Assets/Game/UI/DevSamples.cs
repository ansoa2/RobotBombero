#if DEVELOPMENT_BUILD || UNITY_EDITOR || RB_CAPTURE
using System;
using System.Collections;
using RobotBombero.Simulation;

namespace RobotBombero.Game
{
    /// <summary>
    /// Solo para capturar pantallas con datos creíbles (compilado únicamente en desarrollo, en el editor o con
    /// RB_CAPTURE): un servidor de mentira sin red y unos packs de ejemplo. Nunca existe en una compilación de entrega.
    /// -rb-api demo | demo-offline     servidor de mentira
    /// -rb-rank new|weaker|out        posición del resultado de ejemplo
    /// -rb-sample                      crea packs de ejemplo con estadísticas y prepara resultado y pack de partida
    /// </summary>
    internal static class DevSamples
    {
        public static IApiClient DemoApi(string flag)
        {
            if (flag == "demo") return new DemoApiClient(false);
            if (flag == "demo-offline") return new DemoApiClient(true);
            return null;
        }

        public static void Seed(GameContext context)
        {
            if (context.Packs.All.Count > 0) return;
            context.Packs.Add(Pack("Sigue la pared", 4, new[]
            {
                Rule("Apagar", "Anyone", "Anyone", "Bright", "Anyone", "Anyone", "Put Out"),
                Rule("Obstáculo", "Anyone", "Anyone", "Anyone", "Obstacle Yes", "Anyone", "TurnR", "Fwd"),
                Rule("Recargar", "Low", "Anyone", "Anyone", "Anyone", "Anyone", "TurnL", "TurnL", "Fwd"),
                Rule("Acercarse", "Anyone", "Up", "Anyone", "Obstacle No", "Anyone", "Fwd"),
                Rule("Buscar", "Anyone", "Anyone", "Anyone", "Anyone", "Anyone", "TurnL")
            }, 12, 18420, 1840, 11));
            context.Packs.Add(Pack("Apagafuegos", 2, new[]
            {
                Rule("Apagar", "Anyone", "Anyone", "Bright", "Anyone", "Anyone", "Put Out"),
                Rule("Avanzar", "High", "Anyone", "Anyone", "Obstacle No", "Anyone", "Fwd")
            }, 3, 1300, 260, 5));
            context.Packs.Add(Pack("Mi primer pack", 1, new[] { Rule("Siempre", "Anyone", "Anyone", "Anyone", "Anyone", "Anyone", "Fwd") }, 0, 0, 0, 0));
            context.PlayPack = context.Packs.All[0];
            context.EditingPack = context.Packs.All[0];
            // -rb-rank new (por defecto), weaker (no mejora la marca) u out (sin fila en el top 100)
            string rank = DevFlags.Get("-rb-rank");
            context.LastResult = new SessionResult { Steps = 1840, Fires = 11, Points = 20240, EndReason = "WalkedIntoFire", Registered = true, FromServer = true };
            if (rank == "weaker") context.LastResult.PlayerRank = 4;
            else if (rank != "out") context.LastResult.Rank = 7;
            context.LastResultPack = context.Packs.All[0].content.name;
        }

        private static Rule Rule(string name, string battery, string temp, string light, string prox, string cell, params string[] actions)
        {
            return new Rule { name = name, battery = battery, temp = temp, light = light, prox = prox, cell = cell, actions = actions };
        }

        private static LocalPack Pack(string name, int id, Rule[] rules, int games, long bestPoints, int bestSteps, int bestFires)
        {
            var pack = new LocalPack
            {
                localId = "demo-" + id,
                content = new RulePackFile { formatVersion = PackLimits.CurrentFormatVersion, name = name, rules = rules },
                stats = new PackStats()
            };
            for (int i = 0; i < games; i++) pack.stats.Add(300 + i * 90, 2 + i % 6);
            if (games > 0) pack.stats.Add(bestSteps, bestFires);
            return pack;
        }

        private sealed class DemoApiClient : IApiClient
        {
            private readonly bool _offline;

            public DemoApiClient(bool offline) { _offline = offline; }

            private ApiResult<T> Reply<T>(T value) { return _offline ? ApiResult<T>.ConnectionFailed("sin red") : ApiResult<T>.Ok(value); }

            public IEnumerator CreatePlayer(string name, Action<ApiResult<PlayerCreated>> done)
            {
                done(Reply(new PlayerCreated { playerId = 1, name = name, key = "demostracion" }));
                yield break;
            }

            public IEnumerator SavePack(string playerKey, long serverPackId, string name, string rulesHash, Action<ApiResult<long>> done)
            {
                done(Reply(serverPackId > 0 ? serverPackId : 1));
                yield break;
            }

            public IEnumerator DeletePack(string playerKey, long serverPackId, Action<ApiResult<bool>> done)
            {
                done(Reply(true));
                yield break;
            }

            public IEnumerator StartGame(string playerKey, long serverPackId, Rule[] rules, Action<ApiResult<StartedGame>> done)
            {
                done(Reply(new StartedGame { gameId = 1, seed = 42 }));
                yield break;
            }

            public IEnumerator FinishGame(string playerKey, long gameId, Action<ApiResult<GameResult>> done)
            {
                done(ApiResult<GameResult>.ConnectionFailed("demostración"));
                yield break;
            }

            public IEnumerator GetLeaderboard(Action<ApiResult<LeaderboardRow[]>> done)
            {
                done(Reply(new[]
                {
                    Row(1, "Capitán_Brasa", "Resistente", 41250, 2750, 15),
                    Row(2, "Maria-Fuego", "Ruta en espiral", 33800, 2600, 13),
                    Row(3, "Ana_Bombera", "Apaga todo", 25410, 2310, 11),
                    Row(4, "Bombera_7", "Sigue la pared", 18420, 1840, 10),
                    Row(5, "Luis_99", "Primer intento", 12100, 1100, 11),
                    Row(6, "Sofía_R", "Doble giro", 9800, 980, 10),
                    Row(7, "Nacho", "Pack largo con un nombre muy largo", 7200, 900, 8),
                    Row(8, "Eva-M", "Recarga", 5600, 700, 8),
                    Row(9, "Pepe_01", "Base", 3300, 660, 5),
                    Row(10, "Lola", "Cero", 1000, 500, 2)
                }));
                yield break;
            }

            private static LeaderboardRow Row(int rank, string player, string pack, long points, int steps, int fires)
            {
                return new LeaderboardRow { rank = rank, playerName = player, packName = pack, points = points, steps = steps, fires = fires };
            }
        }
    }
}
#endif
