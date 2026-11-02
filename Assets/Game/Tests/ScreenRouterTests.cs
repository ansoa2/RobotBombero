using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game.Tests
{
    /// <summary>Prueba la interfaz sin escena: los árboles vienen de los UXML reales y no hay panel en marcha.</summary>
    public class ScreenRouterTests
    {
        private const string UiPath = "Assets/Game/UI/";

        private string _storeRoot;
        private LocalStore _store;
        private FakeApi _api;
        private GameContext _context;
        private VisualElement _root;
        private ScreenRouter _router;
        private Texture2D _portrait;
        private Texture2D _landscape;

        [SetUp]
        public void SetUp()
        {
            _storeRoot = Path.Combine(Path.GetTempPath(), "rb-ui-" + Guid.NewGuid().ToString("N"));
            _store = new LocalStore(_storeRoot);
            _api = new FakeApi();
            _portrait = new Texture2D(2, 4);
            _landscape = new Texture2D(4, 2);
            _root = Asset<VisualTreeAsset>("Root.uxml").CloneTree();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_portrait);
            UnityEngine.Object.DestroyImmediate(_landscape);
            if (Directory.Exists(_storeRoot)) Directory.Delete(_storeRoot, true);
            else if (File.Exists(_storeRoot)) File.Delete(_storeRoot);
        }

        private static T Asset<T>(string relativePath) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(UiPath + relativePath);
            Assert.IsNotNull(asset, "Falta " + relativePath);
            return asset;
        }

        private void Start(Profile existing = null)
        {
            if (existing != null) _store.SaveProfile(existing);
            _context = new GameContext(_store, _api, Drain);
            UiAssets assets = UiFixture.Assets(_portrait, _landscape);
            _router = AppScreens.Compose(_root, assets, _context);
            _router.ShowStart();
        }

        /// <summary>Como StartCoroutine: un IEnumerator cedido se ejecuta entero antes de seguir.</summary>
        private static void Drain(IEnumerator routine)
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

        private static void Click(Button button)
        {
            // Clickable.Invoke es lo que ejecuta un clic real; se llama por reflexión para no necesitar un panel.
            MethodInfo invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(invoke, "Clickable.Invoke no existe en esta versión de Unity");
            using (var e = MouseDownEvent.GetPooled())
                invoke.Invoke(button.clickable, new object[] { e });
        }

        private TextField NameField { get { return _root.Q<TextField>("login-name"); } }
        private Button Enter { get { return _root.Q<Button>("login-enter"); } }
        private Button Retry { get { return _root.Q<Button>("login-retry"); } }
        private Label Message { get { return _root.Q<Label>("login-message"); } }

        private void Login(string name)
        {
            NameField.value = name;
            Click(Enter);
        }

        private static bool IsDisplayed(VisualElement element)
        {
            return element.resolvedStyle.display != DisplayStyle.None && element.style.display != DisplayStyle.None;
        }

        // --- Pantalla inicial e identidad ---

        [Test]
        public void NoProfile_ShowsLogin()
        {
            Start();

            Assert.AreEqual(ScreenId.Login, _router.Current);
            Assert.IsNotNull(_root.Q("login"), "La vista de acceso debe estar en el árbol");
            Assert.IsNull(_root.Q("menu"));
        }

        [Test]
        public void ExistingProfile_ShowsMenu()
        {
            Start(new Profile { playerName = "Ana", playerKey = "k-secret", playerId = 3 });

            Assert.AreEqual(ScreenId.Menu, _router.Current);
            Assert.IsNotNull(_root.Q("menu"));
            Assert.IsNull(_root.Q("login"));
            StringAssert.Contains("Ana", _root.Q<Label>("menu-player").text);
        }

        [TestCase(ScreenId.Menu)]
        [TestCase(ScreenId.Leaderboard)]
        [TestCase(ScreenId.Game)]
        public void WithoutServerIdentity_NothingPassesLogin(ScreenId target)
        {
            Start();

            _router.Show(target);

            Assert.AreEqual(ScreenId.Login, _router.Current);
        }

        [Test]
        public void ProfileWithNameButNoKey_ShowsLogin()
        {
            Start(new Profile { playerName = "Ana" });

            Assert.AreEqual(ScreenId.Login, _router.Current);
        }

        // --- Acceso ---

        [Test]
        public void NameTaken_ShowsMessage_StaysOnLogin()
        {
            Start();
            _api.CreatePlayerResult = ApiResult<PlayerCreated>.Rejected(409, "Ese nombre ya existe.");

            Login("Ana");

            Assert.AreEqual(ScreenId.Login, _router.Current);
            StringAssert.Contains("ya está en uso", Message.text);
            Assert.IsFalse(IsDisplayed(Retry), "Reintentar no ayuda si el nombre está ocupado");
            Assert.IsTrue(Enter.enabledSelf, "Debe poder probar otro nombre");
            Assert.IsNull(_store.LoadProfile().playerKey);
        }

        [Test]
        public void ServerOffline_LoginExplainsAndAllowsRetry()
        {
            Start();
            _api.CreatePlayerResult = ApiResult<PlayerCreated>.ConnectionFailed("sin red");

            Login("Ana");

            Assert.AreEqual(ScreenId.Login, _router.Current);
            StringAssert.Contains("servidor", Message.text);
            Assert.IsTrue(IsDisplayed(Retry));

            _api.CreatePlayerResult = ApiResult<PlayerCreated>.Ok(new PlayerCreated { playerId = 8, name = "Ana", key = "k-secret" }, 201);
            Click(Retry);

            Assert.AreEqual(2, _api.CreatePlayerCalls);
            Assert.AreEqual(ScreenId.Menu, _router.Current);
        }

        [Test]
        public void Success_SavesProfile_AndGoesToMenu()
        {
            Start();
            _api.CreatePlayerResult = ApiResult<PlayerCreated>.Ok(new PlayerCreated { playerId = 8, name = "Ana_1", key = "k-secret" }, 201);

            Login("Ana_1");

            Assert.AreEqual(ScreenId.Menu, _router.Current);
            Profile saved = _store.LoadProfile();
            Assert.AreEqual("Ana_1", saved.playerName);
            Assert.AreEqual("k-secret", saved.playerKey);
            Assert.AreEqual(8, saved.playerId);
            Assert.AreEqual("k-secret", _context.Profile.playerKey);
        }

        [Test]
        public void Success_NeverWritesKeyToLog()
        {
            var logged = new List<string>();
            Application.LogCallback capture = (text, stack, type) => logged.Add(text + stack);
            Application.logMessageReceived += capture;
            try
            {
                Start();
                _api.CreatePlayerResult = ApiResult<PlayerCreated>.Ok(new PlayerCreated { playerId = 8, name = "Ana", key = "k-secret" }, 201);
                Login("Ana");
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }

            Assert.IsFalse(logged.Any(l => l.Contains("k-secret")));
        }

        [Test]
        public void AcceptedWithoutKey_DoesNotGrantIdentity()
        {
            Start();
            _api.CreatePlayerResult = ApiResult<PlayerCreated>.Ok(new PlayerCreated { playerId = 8, name = "Ana", key = "" }, 201);

            Login("Ana");

            Assert.AreEqual(ScreenId.Login, _router.Current);
            Assert.IsNull(_store.LoadProfile().playerKey);
            Assert.IsTrue(IsDisplayed(Retry));
        }

        [Test]
        public void ProfileCannotBeSaved_RetryKeepsTheGrantedName()
        {
            // Una ruta que es un archivo hace fallar la escritura del perfil.
            File.WriteAllText(_storeRoot, "ocupado");
            Start();
            _api.CreatePlayerResult = ApiResult<PlayerCreated>.Ok(new PlayerCreated { playerId = 8, name = "Ana", key = "k-secret" }, 201);

            Login("Ana");

            Assert.AreEqual(ScreenId.Login, _router.Current);
            Assert.IsTrue(IsDisplayed(Retry));
            StringAssert.Contains("guardar", Message.text);

            // El servidor ya concedió el nombre: repetir la petición daría 409.
            File.Delete(_storeRoot);
            Click(Retry);

            Assert.AreEqual(1, _api.CreatePlayerCalls);
            Assert.AreEqual(ScreenId.Menu, _router.Current);
            Assert.AreEqual("k-secret", _store.LoadProfile().playerKey);
        }

        [TestCase("", "entre 3 y 20")]
        [TestCase("ab", "entre 3 y 20")]
        [TestCase("abcdefghijklmnopqrstu", "entre 3 y 20")]
        [TestCase("José", "sin acentos")]
        [TestCase("Ñandú", "sin acentos")]
        [TestCase("con espacio", "sin acentos")]
        [TestCase("a.b", "sin acentos")]
        public void InvalidName_ExplainsWithoutCallingServer(string name, string expected)
        {
            Start();

            Login(name);

            StringAssert.Contains(expected, Message.text);
            Assert.AreEqual(0, _api.CreatePlayerCalls);
            Assert.AreEqual(ScreenId.Login, _router.Current);
        }

        [TestCase("Ana")]
        [TestCase("a-b_C9")]
        [TestCase("abcdefghijklmnopqrst")]
        public void ValidName_MatchesServerRule(string name)
        {
            Assert.IsNull(LoginController.Validate(name));
        }

        [Test]
        public void NameIsTrimmedBeforeSending()
        {
            Start();
            _api.CreatePlayerResult = ApiResult<PlayerCreated>.Rejected(409, null);

            Login("  Ana ");

            Assert.AreEqual("Ana", _api.LastPlayerName);
        }

        [Test]
        public void SubmitWhileWaiting_SendsOneRequest()
        {
            IEnumerator pending = null;
            _context = new GameContext(_store, _api, routine => pending = routine);
            UiAssets assets = UiFixture.Assets(null, null);
            _router = AppScreens.Compose(_root, assets, _context);
            _router.ShowStart();
            _api.CreatePlayerResult = ApiResult<PlayerCreated>.Rejected(409, null);

            Login("Ana");
            Assert.IsFalse(Enter.enabledSelf, "El botón se desactiva mientras espera");
            IEnumerator first = pending;
            Click(Enter);

            Assert.AreSame(first, pending, "No debe lanzar otra petición");
            Drain(pending);
            Assert.AreEqual(1, _api.CreatePlayerCalls);
            Assert.IsTrue(Enter.enabledSelf);
        }

        [Test]
        public void Login_ExplainsLocalProgress()
        {
            Start();

            string note = _root.Q<Label>("login-note").text;
            StringAssert.Contains("dispositivo", note);
            StringAssert.Contains("datos del navegador", note);
        }

        // --- Orientación y zonas seguras ---

        [Test]
        public void Portrait_And_Landscape_SelectMatchingBackground()
        {
            Start();
            VisualElement app = _root.Q("app");

            _router.ApplyLayout(420, 780);
            Assert.IsFalse(app.ClassListContains(ScreenRouter.LandscapeClass));
            Assert.AreSame(_portrait, _router.CurrentBackground);
            Assert.AreSame(_portrait, _root.Q("background").style.backgroundImage.value.texture);

            _router.ApplyLayout(780, 420);
            Assert.IsTrue(app.ClassListContains(ScreenRouter.LandscapeClass));
            Assert.AreSame(_landscape, _router.CurrentBackground);
            Assert.AreSame(_landscape, _root.Q("background").style.backgroundImage.value.texture);

            _router.ApplyLayout(420, 780);
            Assert.IsFalse(app.ClassListContains(ScreenRouter.LandscapeClass));
            Assert.AreSame(_portrait, _router.CurrentBackground);
        }

        [TestCase(768f, 1024f, true)]
        [TestCase(540f, 960f, false)]
        [TestCase(360f, 640f, false)]
        [TestCase(1024f, 768f, false)]
        public void WidePortrait_GetsTabletClass(float width, float height, bool tablet)
        {
            Start();

            _router.ApplyLayout(width, height);

            Assert.AreEqual(tablet, _root.Q("app").ClassListContains(ScreenRouter.TabletClass));
        }

        [Test]
        public void ValidationMessage_DisappearsWhenTheNameChanges()
        {
            Start();
            Login("ab");
            Assert.IsTrue(IsDisplayed(Message));

            NameField.value = "abc";
            ((LoginController)_root.Q("login").GetFirstAncestorOfType<TemplateContainer>().userData).NameChanged();

            Assert.IsFalse(IsDisplayed(Message));
            Assert.IsTrue(IsDisplayed(Enter));
        }

        [Test]
        public void SafeArea_ConvertsScreenPixelsToPanelInsets()
        {
            // Pantalla de 1000 x 2000 con panel de 500 x 1000: la escala es 0,5. Screen.safeArea mide desde abajo.
            var safe = new Rect(40, 60, 900, 1800);

            RectOffsetF insets = SafeArea.Insets(safe, new Vector2(1000, 2000), new Vector2(500, 1000));

            Assert.AreEqual(20f, insets.Left, 0.01f);
            Assert.AreEqual(30f, insets.Right, 0.01f);
            Assert.AreEqual(70f, insets.Top, 0.01f);
            Assert.AreEqual(30f, insets.Bottom, 0.01f);
        }

        [Test]
        public void SafeArea_WithoutSizes_IsEmpty()
        {
            RectOffsetF insets = SafeArea.Insets(new Rect(0, 0, 10, 10), Vector2.zero, Vector2.zero);

            Assert.AreEqual(0f, insets.Left + insets.Right + insets.Top + insets.Bottom);
        }

        // --- Menú ---

        [TestCase("menu-play", ScreenId.PackSelect, "select-back")]
        [TestCase("menu-rules", ScreenId.Rules, "rules-back")]
        [TestCase("menu-stats", ScreenId.Stats, "stats-back")]
        [TestCase("menu-leaderboard", ScreenId.Leaderboard, "lb-back")]
        public void MenuButton_OpensScreen_AndBackReturns(string button, ScreenId expected, string back)
        {
            Start(new Profile { playerName = "Ana", playerKey = "k-secret" });

            Click(_root.Q<Button>(button));

            Assert.AreEqual(expected, _router.Current);
            Assert.IsNull(_root.Q("menu"), "El menú ya no debe estar en el árbol");

            Click(_root.Q<Button>(back));

            Assert.AreEqual(ScreenId.Menu, _router.Current);
            Assert.IsNotNull(_root.Q("menu"));
        }

        [Test]
        public void EveryScreenCanBeShown()
        {
            Start(new Profile { playerName = "Ana", playerKey = "k-secret" });

            foreach (ScreenId id in Enum.GetValues(typeof(ScreenId)))
            {
                _router.Show(id);
                Assert.AreEqual(id, _router.Current);
                Assert.AreEqual(1, _root.Q("screen-host").childCount, "Una sola pantalla a la vez: " + id);
            }
        }

        [Test]
        public void Register_ReplacesScreen()
        {
            Start(new Profile { playerName = "Ana", playerKey = "k-secret" });
            var own = new VisualElement { name = "propia" };
            int shown = 0;

            _router.Register(ScreenId.Stats, () => own, () => shown++);
            _router.Show(ScreenId.Stats);

            Assert.AreSame(own, _root.Q("propia"));
            Assert.AreEqual(1, shown);
        }

        // --- Recursos ---

        [Test]
        public void Fonts_HaveSpanishGlyphs()
        {
            foreach (string path in new[] { "Fonts/Inter-Regular.ttf", "Fonts/Inter-SemiBold.ttf" })
            {
                Font font = Asset<Font>(path);
                foreach (char c in "áéíóúüñÁÉÍÓÚÑ¿¡…")
                    Assert.IsTrue(font.HasCharacter(c), path + " no tiene '" + c + "'");
            }
        }

        [Test]
        public void PanelSettings_ScaleWithScreen_WithTheme()
        {
            PanelSettings settings = Asset<PanelSettings>("PanelSettings.asset");

            Assert.AreEqual(PanelScaleMode.ScaleWithScreenSize, settings.scaleMode);
            Assert.AreEqual(new Vector2Int(420, 780), settings.referenceResolution);
            Assert.AreEqual(0.67f, settings.match, 0.001f);
            Assert.IsNotNull(settings.themeStyleSheet);
        }

        [Test]
        public void BuildList_StartsWithMain_WithoutOldScene()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            Assert.AreEqual("Assets/Scenes/Main.unity", scenes[0].path);
            Assert.IsTrue(scenes[0].enabled);
            Assert.IsFalse(scenes.Any(s => s.path.EndsWith("GameScene.unity")));
        }
    }
}
