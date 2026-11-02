using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RobotBombero.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game.Tests
{
    /// <summary>Juego sin conexión, servidor oculto, partida perezosa, volver atrás y pantalla encendida.</summary>
    public class OfflineUiTests : UiFixture
    {
        private const string BuildAddress = "192.168.1.149:5080";

        private void StartNative(bool loggedIn)
        {
            if (loggedIn) Store.SaveProfile(new Profile { playerName = "Ana", playerKey = "k-secret", playerId = 3 });
            Context = new GameContext(Store, Api, Drain, Files, new ServerConfig(true, BuildAddress));
            Router = AppScreens.Compose(Root, Assets(new Texture2D(2, 4), new Texture2D(4, 2)), Context);
            Router.ShowStart();
        }

        private LocalPack PlayLocal(string id = "p1", long serverId = 55)
        {
            LocalPack pack = AddPack(id, "Alpha", serverId);
            Context.PlayPack = pack;
            Router.Show(ScreenId.Game);
            return pack;
        }

        private void StepUntilLeft()
        {
            int guard = 0;
            while (Router.Current == ScreenId.Game && guard++ < 6000) Click("game-step");
            Assert.AreNotEqual(ScreenId.Game, Router.Current);
        }

        private IEnumerable<string> AllTexts()
        {
            foreach (Label l in Root.Query<Label>().ToList()) if (!string.IsNullOrEmpty(l.text)) yield return l.text;
            foreach (Button b in Root.Query<Button>().ToList()) if (!string.IsNullOrEmpty(b.text)) yield return b.text;
        }

        // --- Servidor oculto ---

        [Test]
        public void Login_HasNoServerField_AndNoAddressAnywhere()
        {
            StartNative(false);
            Assert.IsNull(Root.Q("login-server"));
            Assert.IsNull(Root.Q("login-server-block"));
            foreach (string t in AllTexts()) StringAssert.DoesNotContain("192.168", t);
        }

        [Test]
        public void Login_ConnectionFailure_IsGeneric_AndKeepsTheOfflineOption()
        {
            StartNative(false);
            Root.Q<TextField>("login-name").value = "Bombera_7";
            Click("login-enter");

            Assert.AreEqual("No se puede conectar con el servidor.", Lbl("login-message").text);
            Assert.IsTrue(IsShown(Btn("login-retry")));
            Assert.IsTrue(IsShown(Btn("login-offline")));
            foreach (string t in AllTexts())
            {
                StringAssert.DoesNotContain("192.168", t);
                StringAssert.DoesNotContain("http", t);
            }
        }

        [Test]
        public void Menu_ShowsNoServerButton_AndNoAddress()
        {
            StartNative(true);
            Assert.IsNull(Root.Q<Button>("menu-server"));
            foreach (string t in AllTexts()) StringAssert.DoesNotContain("192.168", t);
        }

        // --- Sin conexión ---

        [Test]
        public void NoIdentityAndNoData_LoginOffersToPlayOffline()
        {
            Start(false);
            Assert.AreEqual(ScreenId.Login, Router.Current);
            Assert.IsTrue(IsShown(Btn("login-offline")));
            Assert.AreEqual("Jugar sin conexión", Btn("login-offline").text);
        }

        [Test]
        public void PlayOffline_GoesToTheMenu_WithoutTouchingTheNetwork_AndIsRemembered()
        {
            Start(false);
            Click("login-offline");

            Assert.AreEqual(ScreenId.Menu, Router.Current);
            Assert.AreEqual(0, Api.CreatePlayerCalls + Api.SavePackCalls + Api.StartCalls + Api.DeleteCalls);
            Assert.AreEqual("Sin conexión", Lbl("menu-player").text);
            Assert.IsTrue(IsShown(Btn("menu-register")));
            Assert.AreEqual("Registrar nombre", Btn("menu-register").text);
            Assert.IsTrue(Store.LoadProfile().offline);

            // Próximo arranque: directo al menú.
            var root2 = Load<VisualTreeAsset>("Root.uxml").CloneTree();
            var context2 = new GameContext(Store, Api, Drain, Files);
            var router2 = AppScreens.Compose(root2, Assets(new Texture2D(2, 4), new Texture2D(4, 2)), context2);
            router2.ShowStart();
            Assert.AreEqual(ScreenId.Menu, router2.Current);
        }

        [Test]
        public void LocalDataWithoutIdentity_OpensTheOfflineMenuDirectly()
        {
            Store.SavePack(TestData.Local("p1", "Alpha"));
            Start(false);
            Assert.AreEqual(ScreenId.Menu, Router.Current);
            Assert.IsTrue(Context.IsOffline);
            Assert.IsTrue(IsShown(Btn("menu-register")));
        }

        [Test]
        public void WithIdentity_MenuShowsNoOfflineIndicator()
        {
            Start();
            Assert.AreEqual("Jugando como Ana", Lbl("menu-player").text);
            Assert.IsFalse(IsShown(Btn("menu-register")));
        }

        [Test]
        public void Register_FromTheOfflineMenu_KeepsPacksAndStats()
        {
            Start(false);
            Click("login-offline");
            LocalPack pack = AddPack("p1", "Alpha", 0);
            pack.stats.Add(40, 2);
            Context.Packs.Save(pack);

            Click("menu-register");
            Assert.AreEqual(ScreenId.Login, Router.Current);
            Api.CreatePlayerResult = ApiResult<PlayerCreated>.Ok(new PlayerCreated { playerId = 8, name = "Ana", key = "k-nueva" }, 201);
            Root.Q<TextField>("login-name").value = "Ana";
            Click("login-enter");

            Assert.AreEqual(ScreenId.Menu, Router.Current);
            Assert.IsTrue(Context.HasIdentity);
            Assert.AreEqual("Jugando como Ana", Lbl("menu-player").text);
            Assert.IsFalse(IsShown(Btn("menu-register")));
            Assert.AreEqual(1, Store.LoadPacks().Single().stats.games);
            Assert.IsFalse(Store.LoadProfile().offline);
        }

        [Test]
        public void Register_NameTaken_JustAsksForAnother()
        {
            Start(false);
            Click("login-offline");
            Click("menu-register");
            Api.CreatePlayerResult = ApiResult<PlayerCreated>.Rejected(409, null);
            Root.Q<TextField>("login-name").value = "Ana";
            Click("login-enter");

            Assert.AreEqual(ScreenId.Login, Router.Current);
            StringAssert.Contains("ya está en uso", Lbl("login-message").text);
            Assert.IsFalse(Context.HasIdentity);
            Assert.IsTrue(Context.IsOffline, "Sigue jugando sin conexión");
        }

        [Test]
        public void OfflineGame_NeverCallsTheServer_AndSaysItIsNotRanked()
        {
            Start(false);
            Click("login-offline");
            LocalPack pack = PlayLocal("p1", 0);
            Assert.IsTrue(IsShown(Root.Q("game-badge")));

            StepUntilLeft();

            Assert.AreEqual(ScreenId.Result, Router.Current);
            Assert.AreEqual(0, Api.SavePackCalls + Api.StartCalls + Api.FinishCalls);
            Assert.AreEqual("Sin conexión: esta partida no entra en la clasificación", Lbl("result-note").text);
            Assert.AreEqual("No registrada", Lbl("result-rank").text);
            Assert.AreEqual(1, pack.stats.games);
            Assert.AreEqual(0, Store.LoadPendingFinishes().Count, "Nunca se envía después");
        }

        [Test]
        public void Leaderboard_WithoutConnection_SaysSoAndOffersRetry()
        {
            Start(false);
            Click("login-offline");
            Click("menu-leaderboard");
            Assert.AreEqual("Sin conexión", Lbl("lb-message").text);
            Assert.IsTrue(IsShown(Btn("lb-retry")));
            int calls = Api.LeaderboardCalls;
            Click("lb-retry");
            Assert.AreEqual(calls + 1, Api.LeaderboardCalls);
        }

        [Test]
        public void ConnectionFailure_NeverDeletesTheIdentityOrData()
        {
            Start();
            LocalPack pack = AddPack("p1", "Alpha");
            pack.serverRulesHash = "viejo";
            Api.SavePackResult = ApiResult<long>.ConnectionFailed("sin red");
            Api.DeleteResult = ApiResult<bool>.ConnectionFailed("sin red");
            Store.SavePendingDeletes(new List<long> { 77 });
            Context.Run(Context.Packs.SyncPending());

            Assert.IsTrue(Context.HasIdentity);
            Assert.AreEqual("k-secret", Store.LoadProfile().playerKey);
            Assert.AreEqual(55, Store.LoadPacks().Single().serverPackId);
            Assert.AreEqual(1, Store.LoadPendingDeletes().Count);
        }

        // --- Partida perezosa ---

        [Test]
        public void OpeningAndLeavingTheGame_MakesNoServerCall()
        {
            Start();
            PlayLocal();
            Assert.AreEqual(0, Api.SavePackCalls + Api.StartCalls + Api.FinishCalls);
            Click("game-back");
            Assert.AreEqual(ScreenId.PackSelect, Router.Current);
            Assert.AreEqual(0, Api.SavePackCalls + Api.StartCalls + Api.FinishCalls + Api.DeleteCalls);
        }

        [Test]
        public void FirstAutoPress_StartsTheServerGame()
        {
            Start();
            PlayLocal();
            Assert.AreEqual(0, Api.StartCalls);
            Click("game-auto");
            Assert.AreEqual(1, Api.StartCalls);
            GameCtl.Tick(0.5f);
            Assert.AreEqual("10", Lbl("game-steps").text);
        }

        [Test]
        public void BoardLooksTheSame_BeforeAndAfterTheGameStarts()
        {
            Start();
            PlayLocal();
            string before = BoardSignature();
            string sensorsBefore = Lbl("sensor-battery").text + Lbl("sensor-temp").text + Lbl("sensor-light").text + Lbl("sensor-prox").text + Lbl("sensor-cell").text;
            Assert.AreEqual(64, Root.Q("board-grid").childCount);

            Api.StartResult = ApiResult<StartedGame>.Ok(new StartedGame { gameId = 9, seed = 123456u });
            Drain(GameCtl.Session.Begin());

            string after = BoardSignature();
            Assert.AreEqual(before, after);
            Assert.AreEqual(sensorsBefore, Lbl("sensor-battery").text + Lbl("sensor-temp").text + Lbl("sensor-light").text + Lbl("sensor-prox").text + Lbl("sensor-cell").text);
        }

        private string BoardSignature()
        {
            GameSimulation sim = GameCtl.Session.Simulation ?? GameCtl.Session.Preview;
            var sb = new System.Text.StringBuilder();
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++) sb.Append((int)sim.GetCell(r, c));
            sb.Append(sim.RobotRow).Append(sim.RobotCol).Append(sim.DirRow).Append(sim.DirCol);
            return sb.ToString();
        }

        [Test]
        public void ConnectionLostBeforeFirstStep_StartsUnranked_WithTheOfflineBadge()
        {
            Start();
            Api.SavePackResult = ApiResult<long>.ConnectionFailed("sin red");
            PlayLocal("p1", 0);
            Click("game-step");
            Assert.IsTrue(IsShown(Root.Q("game-badge")));
            Assert.AreEqual("1", Lbl("game-steps").text, "El primer Paso se ejecuta aunque no haya conexión");
        }

        // --- Resultado ---

        private void ShowResult(SessionResult result)
        {
            Start();
            Context.LastResult = result;
            Context.LastResultPack = "Alpha";
            Context.LastResultManual = false;
            Router.Show(ScreenId.Result);
        }

        [TestCase(UnrankedReason.RateLimited, "Has alcanzado el límite de 60 partidas por hora. Esta no entra en la clasificación.")]
        [TestCase(UnrankedReason.Expired, "La partida caducó.")]
        [TestCase(UnrankedReason.Rejected, "El servidor rechazó la partida.")]
        [TestCase(UnrankedReason.Offline, "Sin conexión: esta partida no entra en la clasificación")]
        public void Result_ShowsTheReason(UnrankedReason reason, string text)
        {
            ShowResult(new SessionResult { Steps = 5, Fires = 1, Points = 5, EndReason = "Battery", Reason = reason });
            Assert.AreEqual(text, Lbl("result-note").text);
            Assert.IsFalse(IsShown(Btn("result-retry")));
        }

        [Test]
        public void Result_ZeroPoints_SaysNoPoints_NotOutsideTop100()
        {
            ShowResult(new SessionResult { Steps = 5, Fires = 0, Points = 0, EndReason = "Battery", Registered = true });
            Assert.AreEqual("Sin puntos", Lbl("result-rank").text);
        }

        [Test]
        public void Result_PendingFinish_OffersRetry_AndRetrySuccessShowsTheRank()
        {
            Start();
            Store.AddPendingFinish(new PendingFinish { serverPackId = 55, gameId = 9 });
            Context.LastResult = new SessionResult { Steps = 5, Fires = 1, Points = 5, EndReason = "Battery", Reason = UnrankedReason.FinishPending, Pending = new PendingFinish { serverPackId = 55, gameId = 9 } };
            Context.LastResultPack = "Alpha";
            Router.Show(ScreenId.Result);
            Assert.AreEqual("Sin conexión: se reintentará al volver a conectar", Lbl("result-note").text);
            Assert.IsTrue(IsShown(Btn("result-retry")));

            Api.FinishResult = ApiResult<GameResult>.ConnectionFailed("sin red");
            Click("result-retry");
            Assert.AreEqual(1, Store.LoadPendingFinishes().Count, "Sin conexión sigue pendiente");
            Assert.IsTrue(IsShown(Btn("result-retry")));

            Api.FinishResult = ApiResult<GameResult>.Ok(new GameResult { Steps = 5, Fires = 1, Points = 5, EndReason = "Battery", Rank = 3 });
            Click("result-retry");
            Assert.AreEqual("Nueva marca: puesto 3", Lbl("result-rank").text);
            Assert.IsFalse(IsShown(Btn("result-retry")));
            Assert.AreEqual(0, Store.LoadPendingFinishes().Count);
            Assert.AreEqual(9, Api.LastFinishGameId);
        }

        [Test]
        public void Result_RetryAnswered410_DropsItSilently()
        {
            Start();
            Store.AddPendingFinish(new PendingFinish { serverPackId = 55, gameId = 9 });
            Context.LastResult = new SessionResult { Steps = 5, Fires = 1, Points = 5, EndReason = "Battery", Reason = UnrankedReason.FinishPending, Pending = new PendingFinish { serverPackId = 55, gameId = 9 } };
            Context.LastResultPack = "Alpha";
            Router.Show(ScreenId.Result);
            Api.FinishResult = ApiResult<GameResult>.Rejected(410, null);
            Click("result-retry");
            Assert.AreEqual(0, Store.LoadPendingFinishes().Count);
            Assert.AreEqual("La partida caducó.", Lbl("result-note").text);
            Assert.IsFalse(IsShown(Btn("result-retry")));
        }

        [Test]
        public void SyncPending_RetriesPendingFinishes_AndDropsThoseTheServerForgot()
        {
            Start();
            Store.AddPendingFinish(new PendingFinish { serverPackId = 55, gameId = 9 });
            Store.AddPendingFinish(new PendingFinish { serverPackId = 55, gameId = 10 });
            LocalPack pack = AddPack("p9", "Beta", 56);
            pack.stats.Add(10, 1);

            Api.FinishResult = ApiResult<GameResult>.ConnectionFailed("sin red");
            Context.Run(Context.Packs.SyncPending());
            Assert.AreEqual(2, Store.LoadPendingFinishes().Count);

            Api.FinishResult = ApiResult<GameResult>.Rejected(404, null);
            Context.Run(Context.Packs.SyncPending());
            Assert.AreEqual(0, Store.LoadPendingFinishes().Count, "404 se descarta en silencio");
            Assert.AreEqual(1, pack.stats.games, "Las estadísticas no se tocan");
        }

        [Test]
        public void DeletingAPack_DropsItsPendingFinishes()
        {
            Start();
            LocalPack pack = AddPack("p1", "Alpha", 55);
            Store.AddPendingFinish(new PendingFinish { serverPackId = 55, gameId = 9 });
            Store.AddPendingFinish(new PendingFinish { serverPackId = 77, gameId = 10 });
            Context.Packs.Delete(pack);
            Assert.AreEqual(1, Store.LoadPendingFinishes().Count);
            Assert.AreEqual(10, Store.LoadPendingFinishes()[0].gameId);
        }

        // --- 401 tardío ---

        [Test]
        public void LateUnauthorized_ForAnOldKey_DoesNotDestroyTheNewIdentity()
        {
            Start();
            bool fired = false;
            Context.SessionExpired += () => fired = true;
            Context.Profile = new Profile { playerName = "Nueva", playerKey = "k-nueva", playerId = 9 };

            bool wasUnauthorized = Context.ExpireIfUnauthorized(ApiResult<long>.Rejected(401, null), "k-vieja");

            Assert.IsTrue(wasUnauthorized);
            Assert.IsTrue(Context.HasIdentity);
            Assert.IsFalse(fired);
        }

        [Test]
        public void Unauthorized_WithTheCurrentKey_ExpiresTheSession()
        {
            Start();
            bool fired = false;
            Context.SessionExpired += () => fired = true;
            Context.ExpireIfUnauthorized(ApiResult<long>.Rejected(401, null), "k-secret");
            Assert.IsTrue(fired);
            Assert.IsFalse(Context.HasIdentity);
        }

        [Test]
        public void SessionExpired_AlwaysFires_EvenIfWritesFail()
        {
            string blocker = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rb-ui-file-" + System.Guid.NewGuid().ToString("N"));
            System.IO.File.WriteAllText(blocker, "x");
            try
            {
                var context = new GameContext(new LocalStore(blocker), Api, Drain, Files);
                context.Profile = new Profile { playerName = "Ana", playerKey = "k", playerId = 1 };
                bool fired = false;
                context.SessionExpired += () => fired = true;
                context.ExpireSession("k");
                Assert.IsTrue(fired);
            }
            finally { System.IO.File.Delete(blocker); }
        }

        [Test]
        public void NewIdentity_ForgetsStalePackServerIds()
        {
            Start(false);
            Click("login-offline");
            LocalPack pack = AddPack("p1", "Alpha", 55);
            Click("menu-register");
            Api.CreatePlayerResult = ApiResult<PlayerCreated>.Ok(new PlayerCreated { playerId = 8, name = "Ana", key = "k-nueva" }, 201);
            Root.Q<TextField>("login-name").value = "Ana";
            Click("login-enter");
            Assert.AreEqual(0, Store.LoadPacks().Single().serverPackId);
        }

        // --- Atrás, Esc y pantalla encendida ---

        [Test]
        public void Back_FromMenuOrLogin_HasNowhereToGo()
        {
            Start();
            Assert.IsFalse(Router.HandleBack());
        }

        [Test]
        public void Back_FromASecondaryScreen_GoesToTheMenu()
        {
            Start();
            foreach (string button in new[] { "menu-rules", "menu-stats", "menu-leaderboard", "menu-play" })
            {
                Router.Show(ScreenId.Menu);
                Click(button);
                Assert.IsTrue(Router.HandleBack(), button);
                Assert.AreEqual(ScreenId.Menu, Router.Current, button);
            }
        }

        [Test]
        public void Back_FromTheEditor_GoesToRules()
        {
            Start();
            LocalPack pack = AddPack("p1", "Alpha");
            Context.EditingPack = pack;
            Router.Show(ScreenId.RuleEditor);
            Assert.IsTrue(Router.HandleBack());
            Assert.AreEqual(ScreenId.Rules, Router.Current);
        }

        [Test]
        public void Back_InAGameInProgress_AsksConfirmation_ThenDialogCloses_ThenLeaves()
        {
            Start();
            PlayLocal();
            Click("game-step");

            Assert.IsTrue(Router.HandleBack());
            Assert.AreEqual(ScreenId.Game, Router.Current);
            Assert.IsTrue(Dialog.IsOpen(Root), "Pide confirmación");

            Assert.IsTrue(Router.HandleBack());
            Assert.IsFalse(Dialog.IsOpen(Root), "Atrás cierra el diálogo");
            Assert.AreEqual(ScreenId.Game, Router.Current);

            Assert.IsTrue(Router.HandleBack());
            Click("dialog-ok");
            Assert.AreEqual(ScreenId.PackSelect, Router.Current);
        }

        [Test]
        public void Back_InAGameNotStarted_LeavesWithoutConfirmation()
        {
            Start();
            PlayLocal();
            Assert.IsTrue(Router.HandleBack());
            Assert.AreEqual(ScreenId.PackSelect, Router.Current);
        }

        [Test]
        public void ScreenStaysAwake_OnlyDuringAuto()
        {
            Start();
            PlayLocal();
            Assert.AreEqual(SleepTimeout.SystemSetting, Screen.sleepTimeout);

            Click("game-auto");
            Assert.AreEqual(SleepTimeout.NeverSleep, Screen.sleepTimeout);
            Click("game-auto");
            Assert.AreEqual(SleepTimeout.SystemSetting, Screen.sleepTimeout);

            Click("game-auto");
            Assert.AreEqual(SleepTimeout.NeverSleep, Screen.sleepTimeout);
            Router.Show(ScreenId.Menu);
            Assert.AreEqual(SleepTimeout.SystemSetting, Screen.sleepTimeout, "Al salir de la partida");
        }

        [TearDown]
        public void ResetSleep()
        {
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        // --- Texto de otros jugadores ---

        [Test]
        public void PlayerSuppliedNames_AreNotParsedAsRichText()
        {
            Assert.IsFalse(UiKit.Label(null, "<b>x</b>").enableRichText);
            Assert.IsFalse(UiKit.Button(null, "<b>x</b>", () => { }).enableRichText);

            Start();
            Api.LeaderboardResult = ApiResult<LeaderboardRow[]>.Ok(new[]
            {
                new LeaderboardRow { rank = 1, playerName = "<color=red>Mala</color>", packName = "<size=99>Pack</size>", points = 10, steps = 5, fires = 2 }
            });
            Router.Show(ScreenId.Leaderboard);
            var labels = Root.Q("lb-list").Query<Label>().ToList();
            Assert.IsNotEmpty(labels);
            foreach (Label l in labels) Assert.IsFalse(l.enableRichText, l.text);
        }

        [Test]
        public void PackName_WithControlCharacters_IsRejectedLocally()
        {
            Assert.IsNotNull(PackManager.ValidateName("uno\ndos"));
            Assert.IsNotNull(PackManager.ValidateName("uno\tdos"));
            Assert.IsNull(PackManager.ValidateName("uno dos"));
        }
    }
}
