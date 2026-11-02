using System.Linq;
using NUnit.Framework;
using RobotBombero.Simulation;
using UnityEngine.UIElements;

namespace RobotBombero.Game.Tests
{
    /// <summary>Reglas, editor de reglas y selección de pack.</summary>
    public class RulesControllerTests : UiFixture
    {
        private LocalPack Edit(LocalPack pack)
        {
            Context.EditingPack = pack;
            Router.Show(ScreenId.RuleEditor);
            return pack;
        }

        private LocalPack Stored(string id)
        {
            return Store.LoadPacks().First(p => p.localId == id);
        }

        // --- Editor de reglas ---

        [Test]
        public void EditingRules_MarksPackChanged_AndCallsApi()
        {
            Start();
            LocalPack pack = Edit(AddPack("p1", "Alpha"));
            Assert.IsFalse(Context.Packs.NeedsSync(pack));

            Click("rule-edit-0");
            Click("cond-battery-Low");

            Assert.AreEqual("Low", pack.content.rules[0].battery);
            Assert.AreEqual("Low", Stored("p1").content.rules[0].battery, "El cambio se guarda al momento");
            Assert.IsTrue(Context.Packs.NeedsSync(pack), "El servidor aún no conoce las reglas nuevas");
            Assert.AreEqual(0, Api.SavePackCalls);

            Click("editor-detail-done");
            Click("editor-back");

            Assert.AreEqual(1, Api.SavePackCalls);
            Assert.AreEqual("k-secret", Api.LastKey);
            Assert.IsFalse(Context.Packs.NeedsSync(pack));
            Assert.AreEqual(RulesHash.Compute(pack.content.rules), Stored("p1").serverRulesHash);
            Assert.AreEqual(ScreenId.Rules, Router.Current);
        }

        [Test]
        public void LeavingEditorWithoutChanges_DoesNotCallApi()
        {
            Start();
            Edit(AddPack("p1", "Alpha"));

            Click("editor-back");

            Assert.AreEqual(0, Api.SavePackCalls);
        }

        [Test]
        public void EditingPackNeverUploaded_DoesNotCallApi()
        {
            Start();
            Edit(AddPack("p1", "Alpha", 0));
            Click("rule-edit-0");
            Click("cond-temp-Down");

            Click("editor-detail-done");
            Click("editor-back");

            Assert.AreEqual(0, Api.SavePackCalls, "Se subirá al jugar");
        }

        [Test]
        public void EveryConditionAndAction_CanBeChosenFromTheEditor()
        {
            Start();
            LocalPack pack = Edit(AddPack("p1", "Alpha"));
            Click("rule-edit-0");

            Click("cond-battery-Medium");
            Click("cond-temp-Equal");
            Click("cond-light-Dark");
            Click("cond-prox-Obstacle-Yes");
            Click("cond-cell-Black");
            Click("action-add-Put-Out");

            Rule rule = pack.content.rules[0];
            Assert.AreEqual("Medium", rule.battery);
            Assert.AreEqual("Equal", rule.temp);
            Assert.AreEqual("Dark", rule.light);
            Assert.AreEqual("Obstacle Yes", rule.prox);
            Assert.AreEqual("Black", rule.cell);
            CollectionAssert.AreEqual(new[] { "Fwd", "Put Out" }, rule.actions);
            Assert.IsTrue(PackValidator.Validate(pack.content).IsValid);
        }

        [Test]
        public void ActionLimit_DisablesAddAtFive()
        {
            Start();
            LocalPack pack = AddPack("p1", "Alpha");
            pack.content.rules[0].actions = new[] { "Fwd", "TurnL", "TurnR", "Fwd" };
            Edit(pack);
            Click("rule-edit-0");
            Assert.IsTrue(Btn("action-add-Fwd").enabledSelf);

            Click("action-add-TurnL");

            Assert.AreEqual(SimConfig.MaxActionsPerRule, pack.content.rules[0].actions.Length);
            foreach (string value in new[] { "Fwd", "TurnL", "TurnR", "Put-Out" })
                Assert.IsFalse(Btn("action-add-" + value).enabledSelf, value);

            Click("action-remove-4");
            Assert.AreEqual(4, pack.content.rules[0].actions.Length);
            Assert.IsTrue(Btn("action-add-Fwd").enabledSelf);
        }

        [Test]
        public void LastActionAndLastRule_CannotBeRemoved()
        {
            Start();
            LocalPack pack = AddPack("p1", "Alpha");
            pack.content.rules = new[] { MakeRule("r1", "High", "Fwd") };
            Edit(pack);

            Assert.IsFalse(Btn("rule-delete-0").enabledSelf, "Un pack sin reglas no se puede guardar ni jugar");
            Click("rule-edit-0");
            Assert.IsFalse(Btn("action-remove-0").enabledSelf);
        }

        [Test]
        public void ActionOrder_CanBeChanged()
        {
            Start();
            LocalPack pack = AddPack("p1", "Alpha");
            pack.content.rules[0].actions = new[] { "Fwd", "TurnL", "TurnR" };
            Edit(pack);
            Click("rule-edit-0");

            Click("action-down-0");

            CollectionAssert.AreEqual(new[] { "TurnL", "Fwd", "TurnR" }, pack.content.rules[0].actions);
            Assert.IsFalse(Btn("action-up-0").enabledSelf);
        }

        [Test]
        public void Reorder_ChangesPriority()
        {
            Start();
            LocalPack pack = AddPack("p1", "Alpha");
            pack.content.rules = new[] { MakeRule("a", "High", "Fwd"), MakeRule("b", "Medium", "TurnL"), MakeRule("c", "Low", "TurnR") };
            Edit(pack);
            Assert.IsFalse(Btn("rule-up-0").enabledSelf, "La primera ya es la de más prioridad");
            Assert.IsFalse(Btn("rule-down-2").enabledSelf);

            Click("rule-up-1");

            CollectionAssert.AreEqual(new[] { "b", "a", "c" }, pack.content.rules.Select(r => r.name).ToArray());
            CollectionAssert.AreEqual(new[] { "b", "a", "c" }, Stored("p1").content.rules.Select(r => r.name).ToArray());

            Click("rule-down-1");

            CollectionAssert.AreEqual(new[] { "b", "c", "a" }, pack.content.rules.Select(r => r.name).ToArray());
        }

        [Test]
        public void AddAndRemoveRule_UpdatesPack()
        {
            Start();
            LocalPack pack = Edit(AddPack("p1", "Alpha"));

            Click("editor-add-rule");

            Assert.AreEqual(3, pack.content.rules.Length);
            Assert.IsTrue(PackValidator.Validate(pack.content).IsValid, "La regla nueva ya es válida");

            Click("rule-delete-0");

            Assert.AreEqual(2, pack.content.rules.Length);
            Assert.AreEqual("r2", pack.content.rules[0].name);
        }

        [Test]
        public void RuleSummary_UsesSpanishLabels()
        {
            Start();
            Edit(AddPack("p1", "Alpha"));

            string text = Lbl("rule-summary-0").text;

            StringAssert.Contains("Batería Alta", text);
            StringAssert.Contains("Sin obstáculo", text);
            StringAssert.DoesNotContain("Obstacle", text);
        }

        // --- Lista de packs ---

        [Test]
        public void RenamePack_KeepsRules()
        {
            Start();
            LocalPack pack = AddPack("p1", "Alpha");
            string hash = RulesHash.Compute(pack.content.rules);
            Router.Show(ScreenId.Rules);

            Click("pack-rename-0");
            Root.Q<TextField>("dialog-input").value = "Beta";
            Click("dialog-ok");

            Assert.AreEqual("Beta", pack.content.name);
            Assert.AreEqual(hash, RulesHash.Compute(pack.content.rules));
            Assert.AreEqual(2, pack.content.rules.Length);
            Assert.AreEqual("Beta", Stored("p1").content.name);
            Assert.AreEqual(1, Api.SavePackCalls, "El servidor también conoce el nombre");
            Assert.IsNull(Root.Q("dialog"), "El diálogo se cierra");
        }

        [Test]
        public void RenamePack_RejectsEmptyAndDuplicateNames_WithoutChanges()
        {
            Start();
            AddPack("p1", "Alpha");
            AddPack("p2", "Beta");
            Router.Show(ScreenId.Rules);

            Click("pack-rename-0");
            Root.Q<TextField>("dialog-input").value = "   ";
            Click("dialog-ok");
            Assert.IsNotNull(Root.Q("dialog"), "Sigue abierto para corregir");
            Assert.IsNotEmpty(Lbl("dialog-message").text);

            Root.Q<TextField>("dialog-input").value = "beta";
            Click("dialog-ok");
            StringAssert.Contains("existe", Lbl("dialog-message").text);
            Assert.AreEqual("Alpha", Stored("p1").content.name);
        }

        [Test]
        public void NewPack_IsPlayable_AndSurvivesReload()
        {
            Start();
            Router.Show(ScreenId.Rules);

            Click("rules-new");
            Root.Q<TextField>("dialog-input").value = "Mi pack";
            Click("dialog-ok");

            LocalPack saved = Store.LoadPacks().Single();
            Assert.AreEqual("Mi pack", saved.content.name);
            Assert.AreEqual(1, saved.content.rules.Length);
            Assert.IsTrue(Context.Packs.CanPlay(saved), "Nace con una regla válida: uno vacío no se podría cargar");
        }

        [Test]
        public void DeletePack_RequiresConfirmation()
        {
            Start();
            AddPack("p1", "Alpha");
            Router.Show(ScreenId.Rules);

            Click("pack-delete-0");

            Assert.AreEqual(1, Store.LoadPacks().Count, "Sin confirmar no se borra nada");
            Assert.IsNotNull(Root.Q("dialog"));
            StringAssert.Contains("Alpha", Lbl("dialog-message").text);

            Click("dialog-cancel");

            Assert.AreEqual(1, Store.LoadPacks().Count);
            Assert.AreEqual(0, Api.DeleteCalls);
            Assert.IsNull(Root.Q("dialog"));

            Click("pack-delete-0");
            Click("dialog-ok");

            Assert.AreEqual(0, Store.LoadPacks().Count);
            Assert.AreEqual(0, Context.Packs.All.Count);
            Assert.AreEqual(1, Api.DeleteCalls, "También se borra en el servidor");
            Assert.AreEqual(55, Api.LastDeletedPackId);
        }

        [Test]
        public void Export_DownloadsJsonWithoutPlayerData()
        {
            Start();
            LocalPack pack = AddPack("p1", "Alpha");
            pack.stats.Add(100, 4);
            Router.Show(ScreenId.Rules);

            Click("pack-export-0");

            Assert.AreEqual("Alpha.json", Files.DownloadedName);
            Assert.AreEqual(PackFiles.Export(pack), Files.DownloadedContent);
            StringAssert.DoesNotContain("stats", Files.DownloadedContent);
            StringAssert.DoesNotContain("k-secret", Files.DownloadedContent);
        }

        [Test]
        public void Export_WhenPlatformCannotDownload_ExplainsIt()
        {
            Start();
            AddPack("p1", "Alpha");
            Files.DownloadResult = false;
            Router.Show(ScreenId.Rules);

            Click("pack-export-0");

            Assert.IsTrue(IsShown(Lbl("rules-message")));
        }

        private RulesController NewRules(out VisualElement view)
        {
            view = Load<VisualTreeAsset>("Screens/Rules.uxml").CloneTree();
            var controller = new RulesController(view, Context, id => { });
            controller.OnShow();
            return controller;
        }

        [Test]
        public void Import_Valid_AddsPackWithUniqueName()
        {
            Start();
            AddPack("p1", "Alpha");
            VisualElement view;
            RulesController rules = NewRules(out view);

            rules.BeginImport();
            Assert.AreEqual(1, Files.PickCalls);
            Files.Deliver(PackFiles.Export(TestData.Local("x", "Alpha")));

            Assert.AreEqual(2, Store.LoadPacks().Count);
            Assert.IsTrue(Store.LoadPacks().Any(p => p.content.name == "Alpha (2)"));
            Assert.AreEqual(0, Api.SavePackCalls, "Un pack importado se sube al jugar");
            Assert.AreEqual(2, view.Q("rules-list").childCount);
        }

        [Test]
        public void Import_Invalid_ShowsPackFilesError_AndChangesNothing()
        {
            Start();
            AddPack("p1", "Alpha");
            VisualElement view;
            RulesController rules = NewRules(out view);

            rules.BeginImport();
            Files.Deliver("{ esto no es un pack");

            Assert.AreEqual(1, Store.LoadPacks().Count);
            Assert.AreEqual(1, Context.Packs.All.Count);
            Label message = view.Q<Label>("rules-message");
            Assert.IsTrue(IsShown(message));
            StringAssert.Contains("no es un pack válido", message.text);
        }

        [Test]
        public void Import_CancelledStaysQuiet_TooLargeExplains()
        {
            Start();
            VisualElement view;
            RulesController rules = NewRules(out view);
            Label message = view.Q<Label>("rules-message");

            rules.BeginImport();
            Files.Fail(FileBridge.ErrorCancelled);
            Assert.IsFalse(IsShown(message));

            rules.BeginImport();
            Files.Fail(FileBridge.ErrorTooLarge);
            StringAssert.Contains("demasiado grande", message.text);
        }

        [Test]
        public void RulesList_ShowsEmptyMessage_WhenNoPacks()
        {
            Start();

            Router.Show(ScreenId.Rules);

            Assert.IsTrue(IsShown(Root.Q("rules-empty")));
        }

        // --- Elegir pack ---

        [Test]
        public void EmptyPack_CannotStartGame()
        {
            Start();
            var empty = new LocalPack
            {
                localId = "vacio",
                content = new RulePackFile { formatVersion = PackLimits.CurrentFormatVersion, name = "Vacío", rules = new Rule[0] },
                stats = new PackStats()
            };
            Context.Packs.Add(empty);

            Router.Show(ScreenId.PackSelect);
            Assert.IsFalse(Btn("select-play-0").enabledSelf);
            Click("select-play-0");
            Assert.AreEqual(ScreenId.PackSelect, Router.Current);
            StringAssert.Contains("Sin reglas", Lbl("select-note-0").text);

            Context.PlayPack = empty;
            Router.Show(ScreenId.Game);
            Assert.AreEqual(0, Api.StartCalls);
            Assert.IsFalse(Btn("game-step").enabledSelf);
            StringAssert.Contains("no tiene reglas", Lbl("game-status").text);
        }

        [Test]
        public void PackSelect_StartsGameWithChosenPack()
        {
            Start();
            AddPack("p1", "Alpha");
            LocalPack second = AddPack("p2", "Beta");
            Router.Show(ScreenId.PackSelect);

            Click("select-play-1");

            Assert.AreEqual(ScreenId.Game, Router.Current);
            Assert.AreSame(second, Context.PlayPack);
        }

        [Test]
        public void Rename_WhileOffline_KeepsPackMarkedForLaterSync()
        {
            Start();
            LocalPack pack = AddPack("p1", "Alpha");
            Api.SavePackResult = ApiResult<long>.ConnectionFailed("sin red");
            Router.Show(ScreenId.Rules);

            Click("pack-rename-0");
            Root.Q<TextField>("dialog-input").value = "Beta";
            Click("dialog-ok");

            Assert.AreEqual("Beta", Stored("p1").content.name, "El cambio local no depende de la red");
            Assert.IsTrue(Context.Packs.NeedsSync(pack), "Se repetirá al jugar");
            Assert.IsTrue(Context.HasIdentity, "Sin conexión no es una sesión caducada");
        }

        [Test]
        public void Sync_WhenPackIsGoneFromServer_ForgetsItsServerId()
        {
            Start();
            LocalPack pack = AddPack("p1", "Alpha");
            pack.content.rules[0].battery = "Low";
            Api.SavePackResult = ApiResult<long>.Rejected(404, "No existe.");

            Drain(Context.Packs.Sync(pack));

            Assert.AreEqual(0, Stored("p1").serverPackId, "Se volverá a crear al jugar");
            Assert.IsTrue(Context.HasIdentity);
        }

        [Test]
        public void PackSelect_WithoutPacks_PointsToRules()
        {
            Start();

            Router.Show(ScreenId.PackSelect);

            Assert.IsTrue(IsShown(Root.Q("select-empty")));
            Click("select-rules");
            Assert.AreEqual(ScreenId.Rules, Router.Current);
        }
    }
}
