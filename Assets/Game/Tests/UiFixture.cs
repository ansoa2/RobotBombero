using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using RobotBombero.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game.Tests
{
    internal sealed class FakeFiles : IFileService
    {
        public string DownloadedName, DownloadedContent;
        public bool DownloadResult = true;
        public int PickCalls;
        public bool Available = true;
        public bool DownloadCancelled;
        public bool IsAvailable { get { return Available; } }
        private Action<string> _onContent, _onError;

        public void Download(string fileName, string content, Action onDone, Action<string> onError)
        {
            DownloadedName = fileName;
            DownloadedContent = content;
            if (DownloadCancelled) onError(FileBridge.ErrorCancelled);
            else if (DownloadResult) onDone();
            else onError(FileBridge.ErrorWrite);
        }

        public void PickJson(Action<string> onContent, Action<string> onError)
        {
            PickCalls++;
            _onContent = onContent;
            _onError = onError;
        }

        public void Deliver(string json) { _onContent(json); }
        public void Fail(string code) { _onError(code); }
    }

    /// <summary>Base de las pruebas de pantallas: árbol real de Root.uxml y vistas reales, sin panel en marcha.</summary>
    public abstract class UiFixture
    {
        private const string UiPath = "Assets/Game/UI/";

        protected string StoreRoot;
        internal LocalStore Store;
        internal FakeApi Api;
        protected GameContext Context;
        protected VisualElement Root;
        protected ScreenRouter Router;
        private Texture2D _portrait;
        private Texture2D _landscape;

        [SetUp]
        public void BaseSetUp()
        {
            StoreRoot = Path.Combine(Path.GetTempPath(), "rb-ui-" + Guid.NewGuid().ToString("N"));
            Store = new LocalStore(StoreRoot);
            Api = new FakeApi();
            Files = new FakeFiles();
            _portrait = new Texture2D(2, 4);
            _landscape = new Texture2D(4, 2);
            Root = Load<VisualTreeAsset>("Root.uxml").CloneTree();
        }

        [TearDown]
        public void BaseTearDown()
        {
            UnityEngine.Object.DestroyImmediate(_portrait);
            UnityEngine.Object.DestroyImmediate(_landscape);
            if (Directory.Exists(StoreRoot)) Directory.Delete(StoreRoot, true);
        }

        protected static T Load<T>(string relativePath) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(UiPath + relativePath);
            Assert.IsNotNull(asset, "Falta " + relativePath);
            return asset;
        }

        internal static UiAssets Assets(Texture2D portrait, Texture2D landscape)
        {
            return new UiAssets
            {
                login = Load<VisualTreeAsset>("Screens/Login.uxml"),
                mainMenu = Load<VisualTreeAsset>("Screens/MainMenu.uxml"),
                rules = Load<VisualTreeAsset>("Screens/Rules.uxml"),
                ruleEditor = Load<VisualTreeAsset>("Screens/RuleEditor.uxml"),
                packSelect = Load<VisualTreeAsset>("Screens/PackSelect.uxml"),
                game = Load<VisualTreeAsset>("Screens/Game.uxml"),
                result = Load<VisualTreeAsset>("Screens/Result.uxml"),
                stats = Load<VisualTreeAsset>("Screens/Stats.uxml"),
                leaderboard = Load<VisualTreeAsset>("Screens/Leaderboard.uxml"),
                tutorial = Load<VisualTreeAsset>("Screens/Tutorial.uxml"),
                backgroundPortrait = portrait,
                backgroundLandscape = landscape
            };
        }

        /// <summary>Arranca la aplicación con una identidad ya concedida (por defecto) o sin ella.</summary>
        internal FakeFiles Files = new FakeFiles();

        protected void Start(bool loggedIn = true)
        {
            if (loggedIn) Store.SaveProfile(new Profile { playerName = "Ana", playerKey = "k-secret", playerId = 3 });
            Context = new GameContext(Store, Api, Drain, Files);
            Router = AppScreens.Compose(Root, Assets(_portrait, _landscape), Context);
            Router.ShowStart();
        }

        /// <summary>Un pack sincronizado con el servidor (id 55), guardado y registrado.</summary>
        internal LocalPack AddPack(string id, string name, long serverId = 55)
        {
            LocalPack pack = TestData.Local(id, name, serverId);
            if (serverId > 0)
            {
                pack.serverRulesHash = RulesHash.Compute(pack.content.rules);
                pack.serverName = name;
            }
            Context.Packs.Add(pack);
            return pack;
        }

        /// <summary>Como StartCoroutine: un IEnumerator cedido se ejecuta entero antes de seguir.</summary>
        protected static void Drain(IEnumerator routine)
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

        protected Button Btn(string name)
        {
            var button = Root.Q<Button>(name);
            Assert.IsNotNull(button, "No existe el botón " + name);
            return button;
        }

        protected Label Lbl(string name)
        {
            var label = Root.Q<Label>(name);
            Assert.IsNotNull(label, "No existe la etiqueta " + name);
            return label;
        }

        /// <summary>Un clic real no llega a un botón desactivado ni oculto.</summary>
        protected static void Click(Button button)
        {
            if (!button.enabledInHierarchy) return;
            if (button.resolvedStyle.display == DisplayStyle.None || button.style.display == DisplayStyle.None) return;
            MethodInfo invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(invoke, "Clickable.Invoke no existe en esta versión de Unity");
            using (var e = MouseDownEvent.GetPooled())
                invoke.Invoke(button.clickable, new object[] { e });
        }

        internal GameController GameCtl { get { return (GameController)Root.Q("game").GetFirstAncestorOfType<TemplateContainer>().userData; } }
        internal LoginController LoginCtl { get { return (LoginController)Root.Q("login").GetFirstAncestorOfType<TemplateContainer>().userData; } }

        protected void Click(string name)
        {
            Click(Btn(name));
        }

        protected static bool IsShown(VisualElement element)
        {
            return element != null && element.resolvedStyle.display != DisplayStyle.None && element.style.display != DisplayStyle.None;
        }

        protected static Rule MakeRule(string name, string battery, string action)
        {
            return new Rule { name = name, battery = battery, temp = "Anyone", light = "Anyone", prox = "Anyone", cell = "Anyone", actions = new[] { action } };
        }
    }
}
