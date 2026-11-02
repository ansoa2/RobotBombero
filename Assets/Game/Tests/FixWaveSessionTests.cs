using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RobotBombero.Simulation;

namespace RobotBombero.Game.Tests
{
    /// <summary>Partida perezosa, motivos de no clasificar, cierres pendientes y recuperaciones.</summary>
    public class FixWaveSessionTests
    {
        private string _root;
        private LocalStore _store;
        private FakeApi _api;
        private Profile _profile;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rb-fix-" + Guid.NewGuid().ToString("N"));
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
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) stack.Push(nested);
            }
        }

        private LocalPack Synced(long id = 55)
        {
            LocalPack pack = TestData.Local("p1", "Alpha", id);
            pack.serverRulesHash = RulesHash.Compute(pack.content.rules);
            pack.serverName = pack.content.name;
            return pack;
        }

        private GameSession Session(LocalPack pack, Profile profile = null, LocalStore store = null, Func<bool> exists = null)
        {
            return new GameSession(pack, profile ?? _profile, _api, store ?? _store, () => 42u, packStillExists: exists);
        }

        private static void ToEnd(GameSession s)
        {
            int guard = 0;
            while (!s.IsEnded && guard++ < 100000) s.StepOnce();
        }

        private static SessionResult FinishAndGet(GameSession s)
        {
            SessionResult result = null;
            s.OnResult += r => result = r;
            Run(s.Finish());
            return result;
        }

        // --- Partida perezosa ---

        [Test]
        public void Constructing_MakesNoServerCall_AndPreviewIsAvailable()
        {
            GameSession s = Session(Synced());
            Assert.IsNotNull(s.Preview);
            Assert.IsFalse(s.IsStarted);
            Assert.AreEqual(0, _api.SavePackCalls + _api.StartCalls + _api.FinishCalls);
        }

        [Test]
        public void Preview_LooksLikeTheStartedGame_ForAnySeed()
        {
            foreach (uint seed in new uint[] { 1u, 42u, 12345u, 4000000000u })
            {
                var preview = new GameSession(Synced(), _profile, null, null, () => seed).Preview;
                var real = new GameSimulation(Synced().content.rules, seed);
                for (int r = 0; r < 8; r++)
                    for (int c = 0; c < 8; c++) Assert.AreEqual(real.GetCell(r, c), preview.GetCell(r, c), "(" + r + "," + c + ") con la semilla " + seed);
                Assert.AreEqual(real.Battery, preview.Battery);
                Assert.AreEqual(real.SensorTemp, preview.SensorTemp);
                Assert.AreEqual(real.SensorLight, preview.SensorLight);
                Assert.AreEqual(real.SensorProx, preview.SensorProx);
                Assert.AreEqual(real.SensorCell, preview.SensorCell);
                Assert.AreEqual(real.FireRow, preview.FireRow);
                Assert.AreEqual(real.FireCol, preview.FireCol);
            }
        }

        [Test]
        public void Begin_IsIdempotent()
        {
            GameSession s = Session(Synced());
            Run(s.Begin());
            Run(s.Begin());
            Assert.AreEqual(1, _api.StartCalls);
        }

        // --- Sin identidad ---

        [Test]
        public void NoIdentity_PlaysLocally_WithoutAnyServerCall_AndNeverSubmits()
        {
            GameSession s = Session(TestData.Local("p1", "Alpha"), new Profile { offline = true });
            Run(s.Begin());
            Assert.IsFalse(s.IsRanked);
            Assert.AreEqual(UnrankedReason.Offline, s.StartReason);
            Assert.AreEqual(42u, s.Seed);
            ToEnd(s);
            SessionResult r = FinishAndGet(s);
            Assert.AreEqual(UnrankedReason.Offline, r.Reason);
            Assert.IsFalse(r.Registered);
            Assert.AreEqual(0, _api.SavePackCalls + _api.StartCalls + _api.FinishCalls);
            Assert.AreEqual(0, _store.LoadPendingFinishes().Count);
        }

        [Test]
        public void ConnectionFailureAtStart_IsOffline_AndNeverSubmittedLater()
        {
            _api.SavePackResult = ApiResult<long>.ConnectionFailed("sin red");
            GameSession s = Session(TestData.Local("p1", "Alpha"));
            Run(s.Begin());
            ToEnd(s);
            SessionResult r = FinishAndGet(s);
            Assert.AreEqual(UnrankedReason.Offline, r.Reason);
            Assert.AreEqual(0, _api.FinishCalls);
            Assert.AreEqual(0, _store.LoadPendingFinishes().Count);
            Assert.IsNotNull(_profile.playerKey, "Un fallo de conexión no borra la identidad");
        }

        // --- Motivos de rechazo ---

        [TestCase(429, UnrankedReason.RateLimited)]
        [TestCase(400, UnrankedReason.Rejected)]
        [TestCase(500, UnrankedReason.Rejected)]
        public void StartRejected_GivesItsReason(int status, UnrankedReason expected)
        {
            _api.StartResult = ApiResult<StartedGame>.Rejected(status, null);
            GameSession s = Session(Synced());
            Run(s.Begin());
            Assert.IsFalse(s.IsRanked);
            ToEnd(s);
            Assert.AreEqual(expected, FinishAndGet(s).Reason);
        }

        [TestCase(410, UnrankedReason.Expired)]
        [TestCase(403, UnrankedReason.Rejected)]
        [TestCase(404, UnrankedReason.Rejected)]
        [TestCase(400, UnrankedReason.Rejected)]
        public void FinishRejected_GivesItsReason_AndDropsThePendingFinish(int status, UnrankedReason expected)
        {
            _api.StartResult = ApiResult<StartedGame>.Ok(new StartedGame { gameId = 9, seed = 42 });
            _api.FinishResult = ApiResult<GameResult>.Rejected(status, null);
            GameSession s = Session(Synced());
            Run(s.Begin());
            ToEnd(s);
            SessionResult r = FinishAndGet(s);
            Assert.AreEqual(expected, r.Reason);
            Assert.IsFalse(r.Registered);
            Assert.AreEqual(0, _store.LoadPendingFinishes().Count);
            Assert.AreEqual(1, s.Pack.stats.games, "Las estadísticas siguen contando una vez");
        }

        [Test]
        public void Labels_GiveTheAgreedSpanishTexts()
        {
            Assert.AreEqual("Has alcanzado el límite de 60 partidas por hora. Esta no entra en la clasificación.", Labels.Unranked(UnrankedReason.RateLimited));
            Assert.AreEqual("La partida caducó.", Labels.Unranked(UnrankedReason.Expired));
            Assert.AreEqual("El servidor rechazó la partida.", Labels.Unranked(UnrankedReason.Rejected));
            Assert.AreEqual("Sin conexión: se reintentará al volver a conectar", Labels.Unranked(UnrankedReason.FinishPending));
            Assert.AreEqual("Sin conexión: esta partida no entra en la clasificación", Labels.Unranked(UnrankedReason.Offline));
            Assert.IsNull(Labels.Unranked(UnrankedReason.None));
        }

        // --- Cierre pendiente ---

        [Test]
        public void Finish_RecordsThePendingFinishBeforeCalling_AndRemovesItOnSuccess()
        {
            List<PendingFinish> during = null;
            _api.OnFinish = () => during = _store.LoadPendingFinishes();
            _api.FinishResult = ApiResult<GameResult>.Ok(new GameResult { Steps = 1, Fires = 0, Points = 0, EndReason = "Battery" });
            GameSession s = Session(Synced());
            Run(s.Begin());
            ToEnd(s);
            FinishAndGet(s);
            Assert.AreEqual(1, during.Count);
            Assert.AreEqual(55, during[0].serverPackId);
            Assert.AreEqual(9, during[0].gameId);
            Assert.AreEqual(0, _store.LoadPendingFinishes().Count);
        }

        [TestCase(-1)]
        [TestCase(500)]
        public void FinishTransientFailure_KeepsThePendingFinish(int status)
        {
            _api.FinishResult = status < 0 ? ApiResult<GameResult>.ConnectionFailed("sin red") : ApiResult<GameResult>.Rejected(status, null);
            GameSession s = Session(Synced());
            Run(s.Begin());
            ToEnd(s);
            SessionResult r = FinishAndGet(s);
            Assert.AreEqual(UnrankedReason.FinishPending, r.Reason);
            Assert.AreEqual(9, r.Pending.gameId);
            Assert.AreEqual(1, _store.LoadPendingFinishes().Count);
            Assert.AreEqual(9, _store.LoadPendingFinishes()[0].gameId);
        }

        [Test]
        public void PendingFinishes_RoundTrip_AndUnreadableCountsAsEmpty()
        {
            _store.AddPendingFinish(new PendingFinish { serverPackId = 3, gameId = 4 });
            _store.AddPendingFinish(new PendingFinish { serverPackId = 3, gameId = 4 });
            Assert.AreEqual(1, new LocalStore(_root).LoadPendingFinishes().Count);
            File.WriteAllText(Path.Combine(_root, "pending-finishes.json"), "{no es json");
            Assert.AreEqual(0, _store.LoadPendingFinishes().Count);
        }

        // --- Recuperaciones ---

        [Test]
        public void Start404_RecreatesThePackOnce_AndStartsRanked()
        {
            _api.StartQueue.Enqueue(ApiResult<StartedGame>.Rejected(404, null));
            _api.StartQueue.Enqueue(ApiResult<StartedGame>.Ok(new StartedGame { gameId = 21, seed = 7 }));
            _api.SavePackResult = ApiResult<long>.Ok(99);
            LocalPack pack = Synced(55);
            GameSession s = Session(pack);
            Run(s.Begin());
            Assert.IsTrue(s.IsRanked);
            Assert.AreEqual(2, _api.StartCalls);
            Assert.AreEqual(1, _api.SavePackCalls, "El pack se vuelve a crear");
            Assert.AreEqual(99, pack.serverPackId);
        }

        [Test]
        public void Start404Twice_PlaysUnranked_WithoutLooping()
        {
            _api.StartResult = ApiResult<StartedGame>.Rejected(404, null);
            GameSession s = Session(Synced());
            Run(s.Begin());
            Assert.IsFalse(s.IsRanked);
            Assert.AreEqual(2, _api.StartCalls);
            Assert.AreEqual(UnrankedReason.Rejected, s.StartReason);
        }

        [Test]
        public void SavePack403_RecreatesThePack()
        {
            _api.SavePackQueue.Enqueue(ApiResult<long>.Rejected(403, null));
            _api.SavePackQueue.Enqueue(ApiResult<long>.Ok(77));
            LocalPack pack = TestData.Local("p1", "Alpha", 55);
            pack.serverRulesHash = "viejo";
            GameSession s = Session(pack);
            Run(s.Begin());
            Assert.IsTrue(s.IsRanked);
            Assert.AreEqual(2, _api.SavePackCalls);
            Assert.AreEqual(77, pack.serverPackId);
        }

        // --- Packs borrados y fallos de escritura ---

        [Test]
        public void PackDeletedDuringBegin_IsNotRestoredToDisk()
        {
            LocalPack pack = TestData.Local("p1", "Alpha", 0);
            _store.SavePack(pack);
            _store.DeletePack("p1");
            bool exists = true;
            _api.OnSavePack = () => { exists = false; };
            GameSession s = Session(pack, exists: () => exists);
            Run(s.Begin());
            Assert.AreEqual(0, _store.LoadPacks().Count, "Salir durante Begin con el pack borrado no devuelve el archivo");
        }

        [Test]
        public void FinishAfterPackDeleted_StillCompletes_AndDoesNotRestoreTheFile()
        {
            LocalPack pack = Synced();
            bool exists = true;
            GameSession s = Session(pack, exists: () => exists);
            Run(s.Begin());
            exists = false;
            ToEnd(s);
            SessionResult r = FinishAndGet(s);
            Assert.IsNotNull(r);
            Assert.AreEqual(0, _store.LoadPacks().Count);
            Assert.AreEqual(1, _api.FinishCalls);
        }

        [Test]
        public void WriteFailureAfterSteps_DoesNotStopSubmission()
        {
            string blocker = Path.Combine(Path.GetTempPath(), "rb-fix-file-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(blocker, "x");
            try
            {
                _api.FinishResult = ApiResult<GameResult>.Ok(new GameResult { Steps = 1, Fires = 0, Points = 0, EndReason = "Battery" });
                GameSession s = Session(Synced(), store: new LocalStore(blocker));
                Run(s.Begin());
                ToEnd(s);
                SessionResult r = FinishAndGet(s);
                Assert.AreEqual(1, _api.FinishCalls);
                Assert.IsTrue(r.Registered);
            }
            finally { File.Delete(blocker); }
        }

        [Test]
        public void SentKey_IsTheKeyAtCreation()
        {
            var profile = new Profile { playerName = "Ana", playerKey = "vieja" };
            GameSession s = Session(Synced(), profile);
            profile.playerKey = "nueva";
            Run(s.Begin());
            Assert.AreEqual("vieja", s.SentKey);
            Assert.AreEqual("vieja", _api.LastKey);
        }

        [Test]
        public void StartTimeout_IsShorterThanTheNormalOne()
        {
            Assert.Less(ApiClient.StartTimeoutSeconds, ApiClient.TimeoutSeconds);
        }
    }
}
