using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RobotBombero.Simulation;

namespace RobotBombero.Game.Tests
{
    internal class FakeApi : IApiClient
    {
        public ApiResult<long> SavePackResult = ApiResult<long>.Ok(55);
        public ApiResult<StartedGame> StartResult = ApiResult<StartedGame>.Ok(new StartedGame { gameId = 9, seed = 42 });
        public ApiResult<GameResult> FinishResult = ApiResult<GameResult>.ConnectionFailed("sin red");

        public bool FinishNeverCompletes;
        public int SavePackCalls, StartCalls, FinishCalls;
        public string LastKey;

        public ApiResult<PlayerCreated> CreatePlayerResult = ApiResult<PlayerCreated>.ConnectionFailed("sin red");
        public int CreatePlayerCalls;
        public string LastPlayerName;

        public IEnumerator CreatePlayer(string name, Action<ApiResult<PlayerCreated>> done)
        {
            CreatePlayerCalls++;
            LastPlayerName = name;
            done(CreatePlayerResult);
            yield break;
        }

        public IEnumerator SavePack(string playerKey, long serverPackId, string name, string rulesHash, Action<ApiResult<long>> done)
        {
            SavePackCalls++;
            LastKey = playerKey;
            done(SavePackResult);
            yield break;
        }

        public ApiResult<bool> DeleteResult = ApiResult<bool>.Ok(true);
        public int DeleteCalls;
        public long LastDeletedPackId;

        public IEnumerator DeletePack(string playerKey, long serverPackId, Action<ApiResult<bool>> done)
        {
            DeleteCalls++;
            LastDeletedPackId = serverPackId;
            done(DeleteResult);
            yield break;
        }

        public IEnumerator StartGame(string playerKey, long serverPackId, Rule[] rules, Action<ApiResult<StartedGame>> done)
        {
            StartCalls++;
            LastKey = playerKey;
            done(StartResult);
            yield break;
        }

        public IEnumerator FinishGame(string playerKey, long gameId, Action<ApiResult<GameResult>> done)
        {
            FinishCalls++;
            while (FinishNeverCompletes) yield return null;
            done(FinishResult);
            yield break;
        }

        public ApiResult<LeaderboardRow[]> LeaderboardResult = ApiResult<LeaderboardRow[]>.ConnectionFailed("sin red");
        public int LeaderboardCalls;

        public IEnumerator GetLeaderboard(Action<ApiResult<LeaderboardRow[]>> done)
        {
            LeaderboardCalls++;
            done(LeaderboardResult);
            yield break;
        }
    }

    public class GameSessionTests
    {
        private string _root;
        private LocalStore _store;
        private FakeApi _api;
        private Profile _profile;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rb-session-" + Guid.NewGuid().ToString("N"));
            _store = new LocalStore(_root);
            _api = new FakeApi();
            _profile = new Profile { playerName = "Ana", playerKey = "k-secret", playerId = 1 };
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private static void Run(IEnumerator routine)
        {
            // Como StartCoroutine: un IEnumerator cedido se ejecuta entero antes de seguir.
            var stack = new System.Collections.Generic.Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) stack.Push(nested);
            }
        }

        private LocalPack SyncedPack()
        {
            LocalPack pack = TestData.Local("p1", "Alpha", 55);
            pack.serverRulesHash = RulesHash.Compute(pack.content.rules);
            pack.serverName = pack.content.name;
            return pack;
        }

        private GameSession Started(LocalPack pack, Profile profile = null)
        {
            var session = new GameSession(pack, profile ?? _profile, _api, _store, () => 42u);
            Run(session.Begin());
            return session;
        }

        private static void StepToEnd(GameSession s)
        {
            int guard = 0;
            while (!s.IsEnded && guard++ < 100000) s.StepOnce();
        }

        [Test]
        public void StepOnce_AdvancesOneStep()
        {
            GameSession s = Started(SyncedPack());
            Assert.AreEqual(0, s.Simulation.Steps);
            s.StepOnce();
            Assert.AreEqual(1, s.Simulation.Steps);
            Assert.AreEqual(SessionMode.Stepping, s.Mode);
        }

        [Test]
        public void Auto_RunsStepsAccordingToSpeed()
        {
            GameSession s = Started(SyncedPack());
            Assert.AreEqual(20, s.Speed);
            s.SetAuto(true);
            s.Tick(0.5f);
            Assert.AreEqual(10, s.Simulation.Steps);

            s.SetSpeed(0);
            s.Tick(2f);
            Assert.AreEqual(12, s.Simulation.Steps);
        }

        [Test]
        public void Tick_WhenNotAuto_DoesNothing()
        {
            GameSession s = Started(SyncedPack());
            s.Tick(5f);
            Assert.AreEqual(0, s.Simulation.Steps);
        }

        [Test]
        public void Auto_LongFrame_DoesNotLoseSteps()
        {
            var s = new GameSession(SyncedPack(), _profile, _api, _store, () => 42u, 5);
            Run(s.Begin());
            s.SetAuto(true);
            s.Tick(1f); // 20 pendientes, 5 por fotograma
            Assert.AreEqual(5, s.Simulation.Steps);
            s.Tick(0f);
            s.Tick(0f);
            s.Tick(0f);
            Assert.AreEqual(20, s.Simulation.Steps);
            s.Tick(0f);
            Assert.AreEqual(20, s.Simulation.Steps);
        }

        [Test]
        public void Ended_StopsAuto()
        {
            GameSession s = Started(SyncedPack());
            int ended = 0;
            s.OnEnded += () => ended++;
            s.SetSpeed(5);
            s.SetAuto(true);
            for (int i = 0; i < 100 && !s.IsEnded; i++) s.Tick(1f);
            Assert.IsTrue(s.IsEnded);
            Assert.AreEqual(SessionMode.Stepping, s.Mode);
            Assert.AreEqual(1, ended);

            int steps = s.Simulation.Steps;
            s.SetAuto(true);
            s.Tick(1f);
            s.StepOnce();
            Assert.AreEqual(steps, s.Simulation.Steps);
            Assert.AreEqual(SessionMode.Stepping, s.Mode);
            Assert.AreEqual(1, ended);
        }

        [Test]
        public void SpeedChange_DoesNotChangeResult()
        {
            var unrankedProfile = new Profile();
            GameSession a = Started(SyncedPack(), unrankedProfile);
            StepToEnd(a);

            GameSession b = Started(SyncedPack(), unrankedProfile);
            b.SetAuto(true);
            int[] speeds = { 0, 3, 5, 1, 4 };
            for (int i = 0; i < 1000 && !b.IsEnded; i++)
            {
                b.SetSpeed(speeds[i % speeds.Length]);
                b.Tick(0.1f);
            }

            Assert.IsTrue(b.IsEnded);
            Assert.AreEqual(a.Simulation.Steps, b.Simulation.Steps);
            Assert.AreEqual(a.Simulation.Fires, b.Simulation.Fires);
            Assert.AreEqual(a.Simulation.EndReason, b.Simulation.EndReason);
        }

        [Test]
        public void StartFails_GameContinuesUnranked()
        {
            _api.StartResult = ApiResult<StartedGame>.ConnectionFailed("sin red");
            GameSession s = Started(SyncedPack());
            Assert.IsFalse(s.IsRanked);
            Assert.IsNotNull(s.Simulation);
            StepToEnd(s);
            Run(s.Finish());
            Assert.AreEqual(0, _api.FinishCalls);
            Assert.IsFalse(s.Result.Registered);
            Assert.AreEqual(1, s.Pack.stats.games);
        }

        [Test]
        public void StartRejected_429_GameContinuesUnranked()
        {
            _api.StartResult = ApiResult<StartedGame>.Rejected(429, "límite");
            GameSession s = Started(SyncedPack());
            Assert.IsFalse(s.IsRanked);
            Assert.IsNotNull(s.Simulation);
        }

        [Test]
        public void Start_Succeeds_UsesServerSeedAndIsRanked()
        {
            _api.StartResult = ApiResult<StartedGame>.Ok(new StartedGame { gameId = 9, seed = 1234 });
            var s = new GameSession(SyncedPack(), _profile, _api, _store, () => 42u);
            Run(s.Begin());
            Assert.IsTrue(s.IsRanked);
            Assert.AreEqual(1234u, s.Seed);
            Assert.AreEqual("k-secret", _api.LastKey);
        }

        [Test]
        public void FinishFails_ShowsNotRegistered_KeepsLocalStats()
        {
            GameSession s = Started(SyncedPack());
            Assert.IsTrue(s.IsRanked);
            StepToEnd(s);
            Run(s.Finish());

            Assert.AreEqual(1, _api.FinishCalls);
            Assert.IsFalse(s.Result.Registered);
            Assert.IsFalse(s.Result.FromServer);
            Assert.IsNull(s.Result.Rank);
            Assert.AreEqual(s.Simulation.Steps, s.Result.Steps);
            Assert.AreEqual(1, s.Pack.stats.games);
            Assert.AreEqual(s.Simulation.Steps, s.Pack.stats.totalSteps);
            Assert.AreEqual(1, _store.LoadPacks()[0].stats.games);
        }

        [Test]
        public void Ranked_LocalAndServerDiffer_UsesServerResult()
        {
            _api.FinishResult = ApiResult<GameResult>.Ok(new GameResult
            {
                Steps = 11, Fires = 4, Points = 999999, EndReason = "Battery", Rank = 3
            });
            GameSession s = Started(SyncedPack());
            StepToEnd(s);
            SessionResult received = null;
            s.OnResult += r => received = r;
            Run(s.Finish());

            Assert.IsTrue(s.Result.Registered);
            Assert.IsTrue(s.Result.FromServer);
            Assert.AreEqual(999999, s.Result.Points);
            Assert.AreEqual(3, s.Result.Rank);
            Assert.AreSame(s.Result, received);
            Assert.AreEqual(1, s.Pack.stats.games);
            // Las estadísticas usan el resultado local, anotado al terminar; la respuesta del servidor no las cambia ni las duplica.
            Assert.AreEqual(s.Simulation.Steps, s.Pack.stats.totalSteps);
            Assert.AreEqual(s.Simulation.Fires, s.Pack.stats.totalFires);
            Assert.AreEqual(1, _store.LoadPacks()[0].stats.games);
        }

        [Test]
        public void Finish_HostDiesDuringServerCall_StatsAlreadySaved()
        {
            _api.FinishNeverCompletes = true;
            GameSession s = Started(SyncedPack());
            StepToEnd(s);

            var stack = new System.Collections.Generic.Stack<IEnumerator>();
            stack.Push(s.Finish());
            for (int i = 0; i < 20 && stack.Count > 0; i++)
            {
                IEnumerator top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) stack.Push(nested);
            }
            Assert.AreEqual(2, stack.Count); // sigue esperando al servidor
            // El anfitrión muere aquí: la corrutina se abandona sin terminar.

            Assert.AreEqual(1, _api.FinishCalls);
            Assert.IsNull(s.Result);
            Assert.AreEqual(1, s.Pack.stats.games);
            Assert.AreEqual(s.Simulation.Steps, s.Pack.stats.totalSteps);
            LocalPack saved = _store.LoadPacks()[0];
            Assert.AreEqual(1, saved.stats.games);
            Assert.AreEqual(s.Simulation.Fires, saved.stats.totalFires);
        }

        [Test]
        public void Parse_StartGame_SeedAtUintMax()
        {
            StartedGame g = ApiParsing.ParseStarted("{\"gameId\":3,\"seed\":4294967295}");
            Assert.AreEqual(3, g.gameId);
            Assert.AreEqual(4294967295u, g.seed);
        }

        [Test]
        public void Parse_StartGame_SeedAboveIntMax()
        {
            Assert.AreEqual(2147483648u, ApiParsing.ParseStarted("{\"gameId\":3,\"seed\":2147483648}").seed);
        }

        [Test]
        public void Finish_CalledTwice_CountsStatsOnce()
        {
            GameSession s = Started(SyncedPack());
            StepToEnd(s);
            Run(s.Finish());
            Run(s.Finish());
            Assert.AreEqual(1, _api.FinishCalls);
            Assert.AreEqual(1, s.Pack.stats.games);
        }

        [Test]
        public void Finish_BeforeEnd_DoesNothing()
        {
            GameSession s = Started(SyncedPack());
            s.StepOnce();
            Run(s.Finish());
            Assert.AreEqual(0, _api.FinishCalls);
            Assert.AreEqual(0, s.Pack.stats.games);
            Assert.IsNull(s.Result);
        }

        [Test]
        public void ManualAction_MakesGameUnranked_NeverFinishes()
        {
            GameSession s = Started(SyncedPack());
            Assert.IsTrue(s.IsRanked);
            s.ApplyManual("TurnL");
            Assert.IsFalse(s.IsRanked);
            StepToEnd(s);
            Run(s.Finish());
            Assert.AreEqual(0, _api.FinishCalls);
            Assert.AreEqual(0, s.Pack.stats.games);
            Assert.IsFalse(s.Result.Registered);
        }

        [Test]
        public void Begin_PackWithoutServerId_SyncsAndStoresId()
        {
            _api.SavePackResult = ApiResult<long>.Ok(77);
            LocalPack pack = TestData.Local("p2", "Beta");
            _store.SavePack(pack);
            GameSession s = Started(pack);

            Assert.AreEqual(1, _api.SavePackCalls);
            Assert.IsTrue(s.IsRanked);
            Assert.AreEqual(77, _store.LoadPacks()[0].serverPackId);
        }

        [Test]
        public void Begin_PackAlreadySynced_DoesNotSyncAgain()
        {
            Started(SyncedPack());
            Assert.AreEqual(0, _api.SavePackCalls);
            Assert.AreEqual(1, _api.StartCalls);
        }

        [Test]
        public void Begin_RulesChanged_SyncsAgain()
        {
            LocalPack pack = SyncedPack();
            pack.content.rules[0].actions = new[] { "TurnR" };
            GameSession s = Started(pack);
            Assert.AreEqual(1, _api.SavePackCalls);
            Assert.AreEqual(RulesHash.Compute(pack.content.rules), s.Pack.serverRulesHash);
        }

        [Test]
        public void Begin_SyncFails_GameContinuesUnranked()
        {
            _api.SavePackResult = ApiResult<long>.ConnectionFailed("sin red");
            LocalPack pack = TestData.Local("p3", "Gamma");
            GameSession s = Started(pack);
            Assert.IsFalse(s.IsRanked);
            Assert.AreEqual(0, _api.StartCalls);
            Assert.IsNotNull(s.Simulation);
            Assert.AreEqual(42u, s.Seed);
        }

        [Test]
        public void Begin_NoPlayerKey_PlaysUnrankedWithoutCallingServer()
        {
            GameSession s = Started(SyncedPack(), new Profile());
            Assert.IsFalse(s.IsRanked);
            Assert.AreEqual(0, _api.SavePackCalls + _api.StartCalls);
        }

        [Test]
        public void Parse_FinishResponse_NullRank()
        {
            GameResult r = ApiParsing.ParseFinish("{\"steps\":40,\"fires\":2,\"points\":80,\"endReason\":\"Battery\",\"rank\":null}");
            Assert.AreEqual(40, r.Steps);
            Assert.AreEqual(2, r.Fires);
            Assert.AreEqual(80, r.Points);
            Assert.AreEqual("Battery", r.EndReason);
            Assert.IsNull(r.Rank);
        }

        [Test]
        public void Parse_FinishResponse_WithRank()
        {
            GameResult r = ApiParsing.ParseFinish("{\"steps\":40,\"fires\":2,\"points\":80,\"endReason\":\"MissionComplete\",\"rank\":7}");
            Assert.AreEqual(7, r.Rank);
            Assert.AreEqual("MissionComplete", r.EndReason);
        }

        [Test]
        public void Parse_Leaderboard_Array()
        {
            LeaderboardRow[] rows = ApiParsing.ParseLeaderboard(
                "[{\"rank\":1,\"playerName\":\"Ana\",\"packName\":\"Alpha\",\"points\":500,\"steps\":50,\"fires\":10,\"date\":\"2026-11-01T10:00:00.0000000Z\"}," +
                "{\"rank\":2,\"playerName\":\"Luis\",\"packName\":\"B\",\"points\":90,\"steps\":30,\"fires\":3,\"date\":\"2026-11-01T11:00:00.0000000Z\"}]");
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("Ana", rows[0].playerName);
            Assert.AreEqual(500, rows[0].points);
            Assert.AreEqual(2, rows[1].rank);
            Assert.AreEqual("B", rows[1].packName);
        }

        [Test]
        public void Parse_Leaderboard_Empty()
        {
            Assert.AreEqual(0, ApiParsing.ParseLeaderboard("[]").Length);
        }

        [Test]
        public void StartBody_KeepsRuleFieldNames()
        {
            string json = ApiParsing.StartBodyJson(5, TestData.Pack("x").rules);
            StringAssert.Contains("\"packId\":5", json);
            StringAssert.Contains("\"rules\":[", json);
            StringAssert.Contains("\"battery\":\"High\"", json);
            StringAssert.Contains("\"actions\":[\"Put Out\",\"TurnL\"]", json);
        }
    }
}
