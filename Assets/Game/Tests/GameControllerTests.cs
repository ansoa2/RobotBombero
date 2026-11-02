using System.Linq;
using NUnit.Framework;
using RobotBombero.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game.Tests
{
    /// <summary>Partida, resultado, estadísticas, clasificación y sesión caducada.</summary>
    public class GameControllerTests : UiFixture
    {
        private LocalPack Play(string id = "p1", long serverId = 55)
        {
            LocalPack pack = AddPack(id, "Alpha", serverId);
            Context.PlayPack = pack;
            Router.Show(ScreenId.Game);
            return pack;
        }

        /// <summary>Pulsa Paso hasta salir de la pantalla de partida.</summary>
        private void StepUntilLeft()
        {
            int guard = 0;
            while (Router.Current == ScreenId.Game && guard++ < 6000) Click("game-step");
            Assert.AreNotEqual(ScreenId.Game, Router.Current, "La partida debía terminar");
        }

        // --- Partida ---

        [Test]
        public void Game_StartsRanked_AndShowsBoardAndSensors()
        {
            Start();
            Play();

            Assert.AreEqual(0, Api.StartCalls, "Abrir la pantalla no llama al servidor");
            Assert.AreEqual(64, Root.Q("board-grid").childCount);
            Assert.IsFalse(IsShown(Root.Q("game-badge")), "Una partida clasificable no lleva etiqueta");
            Assert.AreEqual("50", Lbl("game-battery").text);
            StringAssert.Contains("Alta", Lbl("sensor-battery").text);
            Assert.IsTrue(Btn("game-step").enabledSelf);
            Click("game-step");
            Assert.AreEqual(1, Api.StartCalls, "La partida del servidor empieza con el primer Paso");
            Assert.IsFalse(IsShown(Root.Q("game-badge")));
        }

        [Test]
        public void Step_AdvancesOneStep_AndAutoRuns()
        {
            Start();
            Play();

            Click("game-step");
            Assert.AreEqual("1", Lbl("game-steps").text);

            Click("game-auto");
            GameCtl.Tick(0.5f);
            Assert.AreEqual("11", Lbl("game-steps").text, "20 pasos por segundo durante medio segundo, más el paso anterior");
            Click("game-auto");
            GameCtl.Tick(5f);
            Assert.AreEqual("11", Lbl("game-steps").text, "En pausa no avanza");
        }

        [Test]
        public void Speed_UsesSessionValues_WithOriginalAsDefault()
        {
            Start();
            Play();

            for (int i = 0; i < GameSession.Speeds.Length; i++)
                StringAssert.Contains(GameSession.Speeds[i].ToString(), Btn("game-speed-" + i).text);
            Assert.IsTrue(Btn("game-speed-" + GameSession.DefaultSpeedIndex).ClassListContains("is-selected"));

            Click("game-speed-5");
            Click("game-auto");
            GameCtl.Tick(0.2f);

            Assert.AreEqual("10", Lbl("game-steps").text);
            Assert.IsTrue(Btn("game-speed-5").ClassListContains("is-selected"));
            Assert.IsFalse(Btn("game-speed-4").ClassListContains("is-selected"));
        }

        [Test]
        public void GameEnd_UpdatesLocalStats()
        {
            Start();
            LocalPack pack = Play();
            Api.FinishResult = ApiResult<GameResult>.Ok(new GameResult { Steps = 7, Fires = 0, Points = 0, EndReason = "Battery", Rank = 12 });

            StepUntilLeft();

            Assert.AreEqual(ScreenId.Result, Router.Current);
            Assert.AreEqual(1, pack.stats.games);
            Assert.AreEqual(1, Store.LoadPacks().Single().stats.games, "Se guarda en el dispositivo");
            Assert.AreEqual(1, Api.FinishCalls);
        }

        [Test]
        public void NewGame_ShowsTheControlsFromTheTop()
        {
            Start();
            Play();
            var controls = Root.Q<ScrollView>("game-controls");
            controls.verticalScroller.highValue = 500f;
            controls.scrollOffset = new Vector2(0f, 300f);
            Assert.AreEqual(300f, controls.scrollOffset.y, 0.01f);

            Router.Show(ScreenId.Game);

            Assert.AreEqual(0f, controls.scrollOffset.y, 0.01f);
        }

        [Test]
        public void TestMode_NeverSubmitsScore()
        {
            Start();
            LocalPack pack = Play();
            Click("game-test-toggle");
            StringAssert.Contains("Sin puntuación", Lbl("game-test-note").text);
            Assert.IsFalse(IsShown(Root.Q("game-badge")), "Abrir el panel no basta: aún puede clasificar");

            Click("game-manual-1");

            Assert.IsTrue(IsShown(Root.Q("game-badge")));
            StringAssert.Contains("Sin puntuación", Lbl("game-badge").text);
            StepUntilLeft();

            Assert.AreEqual(ScreenId.Result, Router.Current);
            Assert.AreEqual(0, Api.FinishCalls, "Una partida con controles manuales no se envía");
            Assert.AreEqual(0, pack.stats.games, "Tampoco cuenta en las estadísticas");
            StringAssert.Contains("Sin puntuación", Lbl("result-note").text);
        }

        [Test]
        public void LeavingGameInProgress_AsksConfirmation_ThenStopsWithoutSubmitting()
        {
            Start();
            LocalPack pack = Play();
            Click("game-step");
            Click("game-step");

            Click("game-back");

            Assert.AreEqual(ScreenId.Game, Router.Current, "Primero pregunta");
            Assert.IsNotNull(Root.Q("dialog"));

            Click("dialog-cancel");
            Assert.AreEqual(ScreenId.Game, Router.Current);
            Assert.IsNull(Root.Q("dialog"));
            Click("game-step");
            Assert.AreEqual("3", Lbl("game-steps").text, "Seguía en marcha");

            Click("game-back");
            Click("dialog-ok");

            Assert.AreEqual(ScreenId.PackSelect, Router.Current);
            Assert.AreEqual(0, Api.FinishCalls, "Abandonada: no se envía");
            Assert.AreEqual(0, pack.stats.games, "Abandonada: no cuenta");
            Assert.AreEqual(0, Store.LoadPacks().Single().stats.games);
        }

        [Test]
        public void LeavingBeforeFirstStep_NeedsNoConfirmation()
        {
            Start();
            Play();

            Click("game-back");

            Assert.AreEqual(ScreenId.PackSelect, Router.Current);
        }

        [Test]
        public void OfflineGame_ShowsBadge_AndResultIsNotRegistered()
        {
            Start();
            Api.SavePackResult = ApiResult<long>.ConnectionFailed("sin red");
            LocalPack pack = Play("p1", 0);

            Click("game-step");
            Assert.IsTrue(IsShown(Root.Q("game-badge")));
            StepUntilLeft();

            Assert.AreEqual(ScreenId.Result, Router.Current);
            Assert.AreEqual("No registrada", Lbl("result-rank").text);
            Assert.AreEqual(1, pack.stats.games, "Offline sí cuenta en local");
        }

        // --- Resultado ---

        private void ShowResult(SessionResult result, bool manual = false)
        {
            Start();
            Context.LastResult = result;
            Context.LastResultPack = "Alpha";
            Context.LastResultManual = manual;
            Router.Show(ScreenId.Result);
        }

        [TestCase("Battery", "Sin batería")]
        [TestCase("WalkedIntoFire", "Avanzó hacia el fuego")]
        [TestCase("FireTimeout", "100 ciclos sin apagar un fuego")]
        [TestCase("MissionComplete", "Misión superada: 5.000 pasos")]
        public void Result_ExplainsCauseInPlainSpanish(string reason, string expected)
        {
            ShowResult(new SessionResult { Steps = 10, Fires = 1, Points = 10, EndReason = reason, Registered = true });

            Assert.AreEqual(expected, Lbl("result-cause").text);
        }

        [Test]
        public void Result_ShowsNumbersAndRank()
        {
            ShowResult(new SessionResult { Steps = 1234, Fires = 10, Points = 12340, EndReason = "Battery", Registered = true, Rank = 7 });

            Assert.AreEqual("1.234", Lbl("result-steps").text);
            Assert.AreEqual("10", Lbl("result-fires").text);
            Assert.AreEqual("0,0081", Lbl("result-ratio").text);
            Assert.AreEqual("12.340", Lbl("result-points").text);
            Assert.AreEqual("Nueva marca: puesto 7", Lbl("result-rank").text);
        }

        [Test]
        public void Result_RegisteredWithoutRank_IsOutsideTop100()
        {
            ShowResult(new SessionResult { Steps = 5, Fires = 1, Points = 5, EndReason = "Battery", Registered = true });

            Assert.AreEqual("Fuera del top 100", Lbl("result-rank").text);
        }

        [Test]
        public void Result_WeakerGame_ShowsPlayersCurrentRank_NotOutsideTop100()
        {
            ShowResult(new SessionResult { Steps = 50, Fires = 1, Points = 50, EndReason = "Battery", Registered = true, PlayerRank = 4 });

            Assert.AreEqual("No mejora tu marca · Tu puesto: 4", Lbl("result-rank").text);
        }

        [Test]
        public void ParseFinish_ReadsRankAndPlayerRank()
        {
            GameResult both = ApiParsing.ParseFinish("{\"steps\":5,\"fires\":1,\"points\":5,\"endReason\":\"Battery\",\"rank\":null,\"playerRank\":4}");
            GameResult none = ApiParsing.ParseFinish("{\"steps\":5,\"fires\":1,\"points\":5,\"endReason\":\"Battery\",\"rank\":2,\"playerRank\":2}");
            GameResult old = ApiParsing.ParseFinish("{\"steps\":5,\"fires\":1,\"points\":5,\"endReason\":\"Battery\",\"rank\":null,\"playerRank\":null}");

            Assert.IsNull(both.Rank);
            Assert.AreEqual(4, both.PlayerRank);
            Assert.AreEqual(2, none.Rank);
            Assert.AreEqual(2, none.PlayerRank);
            Assert.IsNull(old.PlayerRank);
        }

        [Test]
        public void Result_NotRegistered_SaysSo()
        {
            ShowResult(new SessionResult { Steps = 5, Fires = 0, Points = 0, EndReason = "Battery", Registered = false });

            Assert.AreEqual("No registrada", Lbl("result-rank").text);
        }

        [Test]
        public void Result_ZeroSteps_HasZeroRatio()
        {
            ShowResult(new SessionResult { Steps = 0, Fires = 0, Points = 0, EndReason = "WalkedIntoFire", Registered = false });

            Assert.AreEqual("0", Lbl("result-ratio").text);
        }

        [Test]
        public void Result_Buttons_PlayAgainAndMenu()
        {
            ShowResult(new SessionResult { Steps = 5, EndReason = "Battery" });
            AddPack("p1", "Alpha");
            Context.PlayPack = Context.Packs.All[0];

            Click("result-again");
            Assert.AreEqual(ScreenId.Game, Router.Current);

            Router.Show(ScreenId.Result);
            Click("result-menu");
            Assert.AreEqual(ScreenId.Menu, Router.Current);
        }

        // --- Estadísticas ---

        [Test]
        public void Stats_ShowsEachPack()
        {
            Start();
            LocalPack a = AddPack("p1", "Alpha");
            a.stats.Add(100, 4);
            a.stats.Add(50, 1);
            AddPack("p2", "Beta");

            Router.Show(ScreenId.Stats);

            Assert.AreEqual(2, Root.Q("stats-list").childCount);
            string text = string.Join(" | ", Root.Query<Label>().ToList().Select(l => l.text).ToArray());
            StringAssert.Contains("Alpha", text);
            StringAssert.Contains("400", text, "Mejor partida: 4 fuegos x 100 pasos");
            StringAssert.Contains("75", text, "Media de pasos");
            StringAssert.Contains("Sin partidas", text, "Beta aún no se ha jugado");
        }

        [Test]
        public void Stats_WithoutPacks_ShowsMessage()
        {
            Start();

            Router.Show(ScreenId.Stats);

            Assert.IsTrue(IsShown(Root.Q("stats-empty")));
        }

        // --- Clasificación ---

        private static LeaderboardRow Row(int rank, string player, string pack, long points, int steps, int fires)
        {
            return new LeaderboardRow { rank = rank, playerName = player, packName = pack, points = points, steps = steps, fires = fires };
        }

        [Test]
        public void Leaderboard_Offline_ShowsMessage()
        {
            Start();
            Api.LeaderboardResult = ApiResult<LeaderboardRow[]>.ConnectionFailed("sin red");

            Router.Show(ScreenId.Leaderboard);

            Assert.IsTrue(IsShown(Lbl("lb-message")));
            StringAssert.Contains("conexión", Lbl("lb-message").text);
            Assert.IsTrue(IsShown(Btn("lb-retry")));
            Assert.AreEqual(0, Root.Q("lb-list").childCount);

            Api.LeaderboardResult = ApiResult<LeaderboardRow[]>.Ok(new[] { Row(1, "Luis", "X", 10, 5, 2) });
            Click("lb-retry");

            Assert.AreEqual(1, Root.Q("lb-list").childCount);
            Assert.IsFalse(IsShown(Lbl("lb-message")));
        }

        [Test]
        public void Leaderboard_Empty_ShowsMessage()
        {
            Start();
            Api.LeaderboardResult = ApiResult<LeaderboardRow[]>.Ok(new LeaderboardRow[0]);

            Router.Show(ScreenId.Leaderboard);

            Assert.IsTrue(IsShown(Lbl("lb-message")));
            StringAssert.Contains("Todavía no hay", Lbl("lb-message").text);
            Assert.IsFalse(IsShown(Btn("lb-retry")));
        }

        [Test]
        public void Leaderboard_HighlightsOwnRow_AndShowsEachPlayerOnce()
        {
            Start();
            Api.LeaderboardResult = ApiResult<LeaderboardRow[]>.Ok(new[]
            {
                Row(1, "Luis", "Rápido", 900, 100, 9),
                Row(2, "Ana", "Alpha", 500, 100, 5),
                Row(3, "Luis", "Otro", 300, 100, 3),
                Row(4, "Eva", "Beta", 200, 100, 2)
            });

            Router.Show(ScreenId.Leaderboard);

            var rows = Root.Q("lb-list").Query(className: "lb-row").ToList();
            Assert.AreEqual(3, rows.Count, "Cada jugador aparece una vez");
            Assert.AreEqual(1, rows.Count(r => r.ClassListContains("lb-row--own")));
            Assert.IsTrue(rows[1].ClassListContains("lb-row--own"));
            StringAssert.Contains("Ana", rows[1].Q<Label>(className: "lb-name").text);
        }

        [Test]
        public void Leaderboard_RowShowsStepsAndFiresUnderName()
        {
            Start();
            Api.LeaderboardResult = ApiResult<LeaderboardRow[]>.Ok(new[] { Row(1, "Luis", "Rápido", 12345, 1500, 9) });

            Router.Show(ScreenId.Leaderboard);

            string sub = Root.Q("lb-list").Q<Label>(className: "lb-sub").text;
            StringAssert.Contains("1.500 pasos", sub);
            StringAssert.Contains("9 fuegos", sub);
            StringAssert.Contains("Rápido", sub);
            Assert.AreEqual("12.345", Root.Q("lb-list").Q<Label>(className: "lb-points").text);
        }

        // --- Sesión caducada (401) ---

        private void AssertSessionEnded(LocalPack pack)
        {
            Assert.AreEqual(ScreenId.Login, Router.Current);
            StringAssert.Contains("sesión", Lbl("login-message").text);
            StringAssert.Contains("ya no es válida", Lbl("login-message").text);

            Profile profile = Store.LoadProfile();
            Assert.IsTrue(string.IsNullOrEmpty(profile.playerKey), "Sin clave");
            Assert.IsTrue(string.IsNullOrEmpty(profile.playerName), "Sin nombre");
            Assert.IsFalse(Context.HasIdentity);

            LocalPack saved = Store.LoadPacks().Single();
            Assert.AreEqual(pack.localId, saved.localId, "El pack se conserva");
            Assert.AreEqual(0, saved.serverPackId, "Sin id de servidor");
            Assert.IsTrue(string.IsNullOrEmpty(saved.serverRulesHash));
            Assert.IsTrue(string.IsNullOrEmpty(saved.serverName));
            Assert.AreEqual(2, saved.content.rules.Length);
        }

        [Test]
        public void Unauthorized_WhenStartingGame_ReturnsToLogin_KeepingPacksAndStats()
        {
            Start();
            LocalPack pack = AddPack("p1", "Alpha", 0);
            pack.stats.Add(100, 3);
            Context.Packs.Save(pack);
            Api.SavePackResult = ApiResult<long>.Rejected(401, "Clave no válida.");
            Context.PlayPack = pack;

            Router.Show(ScreenId.Game);
            Click("game-step");

            AssertSessionEnded(pack);
            Assert.AreEqual(1, Store.LoadPacks().Single().stats.games, "Las estadísticas se conservan");
            Assert.AreEqual(0, Api.StartCalls);
        }

        [Test]
        public void Unauthorized_WhenFinishingGame_ReturnsToLogin()
        {
            Start();
            LocalPack pack = Play();
            Api.FinishResult = ApiResult<GameResult>.Rejected(401, "Clave no válida.");

            StepUntilLeft();

            AssertSessionEnded(pack);
            Assert.AreEqual(1, Store.LoadPacks().Single().stats.games, "La partida ya se había contado");
        }

        [Test]
        public void Unauthorized_WhenSyncingEditedPack_ReturnsToLogin()
        {
            Start();
            LocalPack pack = AddPack("p1", "Alpha");
            Context.EditingPack = pack;
            Router.Show(ScreenId.RuleEditor);
            Click("rule-edit-0");
            Click("cond-battery-Low");
            Api.SavePackResult = ApiResult<long>.Rejected(401, "Clave no válida.");

            Click("editor-detail-done");
            Click("editor-back");

            AssertSessionEnded(pack);
        }

        [Test]
        public void Unauthorized_WhenDeletingPackOnServer_ReturnsToLogin()
        {
            Start();
            LocalPack pack = AddPack("p1", "Alpha");
            Api.DeleteResult = ApiResult<bool>.Rejected(401, "Clave no válida.");
            Router.Show(ScreenId.Rules);

            Click("pack-delete-0");
            Click("dialog-ok");

            Assert.AreEqual(ScreenId.Login, Router.Current);
            Assert.IsFalse(Context.HasIdentity);
            Assert.AreEqual(0, Store.LoadPacks().Count, "El borrado local sí se hizo");
        }

        [Test]
        public void LoginAfterExpiry_StartsClean()
        {
            Start();
            LocalPack pack = AddPack("p1", "Alpha");
            Api.SavePackResult = ApiResult<long>.Rejected(401, null);
            Context.PlayPack = pack;
            Context.PlayPack.serverRulesHash = "viejo";
            Router.Show(ScreenId.Game);
            Click("game-step");
            Assert.AreEqual(ScreenId.Login, Router.Current);

            Root.Q<TextField>("login-name").value = "Nueva";
            LoginCtl.NameChanged();
            Assert.IsFalse(IsShown(Lbl("login-message")), "Al cambiar el nombre desaparece el aviso");
        }
    }
}
