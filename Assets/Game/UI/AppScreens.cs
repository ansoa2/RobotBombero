using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>Lo que comparten las pantallas: datos locales, servidor, perfil, packs y cómo lanzar corrutinas.</summary>
    public sealed class GameContext
    {
        public GameContext(LocalStore store, IApiClient api, Action<IEnumerator> run, IFileService files = null, ServerConfig server = null)
        {
            if (store == null) throw new ArgumentNullException("store");
            if (api == null) throw new ArgumentNullException("api");
            if (run == null) throw new ArgumentNullException("run");
            Store = store;
            Api = api;
            Run = run;
            Files = files ?? new FileBridgeService();
            Profile = store.LoadProfile();
            Packs = new PackManager(this);

            // La dirección del servidor es la de la compilación y nada más: no se guarda, no se elige y no se muestra.
            // En WebGL (sin configuración) las rutas son relativas.
            var target = api as IServerTarget;
            if (target != null && server != null && server.Enabled)
            {
                ServerAddressResult address = ServerAddress.Normalize(server.DefaultAddress);
                target.BaseUrl = address.IsValid ? address.Url : "";
            }
        }

        public LocalStore Store { get; private set; }
        public IApiClient Api { get; private set; }
        public Action<IEnumerator> Run { get; private set; }
        public IFileService Files { get; private set; }
        public PackManager Packs { get; private set; }
        public Profile Profile { get; set; }

        // Lo que una pantalla le pasa a la siguiente.
        public LocalPack EditingPack { get; set; }
        public LocalPack PlayPack { get; set; }
        public SessionResult LastResult { get; set; }
        public string LastResultPack { get; set; }
        public bool LastResultManual { get; set; }

        /// <summary>La clave del jugador ya no vale en el servidor: hay que volver al acceso.</summary>
        public event Action SessionExpired;

        /// <summary>Solo el servidor concede la identidad: hace falta nombre y clave.</summary>
        public bool HasIdentity
        {
            get { return Profile != null && !string.IsNullOrEmpty(Profile.playerName) && !string.IsNullOrEmpty(Profile.playerKey); }
        }

        /// <summary>
        /// Juego sin conexión: no hay identidad, pero sí datos locales (el jugador eligió jugar sin conexión o ya hay packs).
        /// Se entra directamente en el menú, sin esperar a la red. Las partidas no se clasifican.
        /// </summary>
        public bool IsOffline
        {
            get { return !HasIdentity && Profile != null && (Profile.offline || Packs.All.Count > 0); }
        }

        /// <summary>Se puede usar la aplicación: con identidad o sin conexión.</summary>
        public bool HasAccess
        {
            get { return HasIdentity || IsOffline; }
        }

        /// <summary>Elige jugar sin conexión. Se recuerda para el próximo arranque. No toca packs ni estadísticas.</summary>
        public void StartOffline()
        {
            if (HasIdentity) return;
            Profile = new Profile { offline = true };
            try { Store.SaveProfile(Profile); }
            catch (System.IO.IOException) { Debug.LogWarning("RobotBombero: no se pudo guardar el perfil sin conexión."); }
            catch (UnauthorizedAccessException) { Debug.LogWarning("RobotBombero: no se pudo guardar el perfil sin conexión."); }
        }

        /// <summary>
        /// Si la respuesta es un 401 de la clave enviada, cierra la sesión y avisa a la interfaz. Devuelve si era un 401.
        /// Un 401 tardío de una clave que ya no es la del jugador (otra identidad nueva) no hace nada.
        /// </summary>
        public bool ExpireIfUnauthorized<T>(ApiResult<T> result, string sentKey)
        {
            if (result == null || result.Status != ApiStatus.Rejected || result.HttpStatus != 401) return false;
            ExpireSession(sentKey);
            return true;
        }

        /// <summary>
        /// Borra la identidad del perfil; los packs y sus estadísticas se quedan, sin los ids del servidor.
        /// Con sentKey, solo si sigue siendo la clave del jugador: una respuesta tardía no destruye una identidad nueva.
        /// Un fallo al escribir no impide avisar a la interfaz.
        /// </summary>
        public void ExpireSession(string sentKey = null)
        {
            if (sentKey != null && (Profile == null || Profile.playerKey != sentKey)) return;

            Profile = new Profile();
            try { Store.SaveProfile(Profile); }
            catch (System.IO.IOException) { Debug.LogWarning("RobotBombero: no se pudo guardar el perfil al cerrar la sesión."); }
            catch (UnauthorizedAccessException) { Debug.LogWarning("RobotBombero: no se pudo guardar el perfil al cerrar la sesión."); }
            Packs.ForgetServerIds();
            EditingPack = null;
            PlayPack = null;
            if (SessionExpired != null) SessionExpired();
        }
    }

    [Serializable]
    public sealed class UiAssets
    {
        public VisualTreeAsset login;
        public VisualTreeAsset mainMenu;
        public VisualTreeAsset rules;
        public VisualTreeAsset ruleEditor;
        public VisualTreeAsset packSelect;
        public VisualTreeAsset game;
        public VisualTreeAsset result;
        public VisualTreeAsset stats;
        public VisualTreeAsset leaderboard;
        public VisualTreeAsset tutorial;
        public Texture2D backgroundPortrait;
        public Texture2D backgroundLandscape;
    }

    /// <summary>Une las vistas con el enrutador y cada una con su controlador.</summary>
    public static class AppScreens
    {
        public const string MsgSessionExpired = "Tu sesión ya no es válida. Vuelve a entrar con un nombre.";
        public const string TextOffline = "Sin conexión";

        public static ScreenRouter Compose(VisualElement root, UiAssets assets, GameContext context)
        {
            if (assets == null) throw new ArgumentNullException("assets");
            if (context == null) throw new ArgumentNullException("context");

            var router = new ScreenRouter(root, () => context.HasAccess, assets.backgroundPortrait, assets.backgroundLandscape);
            Action<ScreenId> go = id => router.Show(id);

            LoginController login = null;
            router.Register(ScreenId.Login, () =>
            {
                VisualElement view = Instantiate(assets.login);
                login = new LoginController(view, context,
                    () =>
                    {
                        router.Show(ScreenId.Menu);
                        context.Run(context.Packs.SyncPending());
                    },
                    () => router.Show(ScreenId.Menu));
                view.userData = login;
                return view;
            }, () => login.OnShow());
            context.SessionExpired += () =>
            {
                router.Show(ScreenId.Login);
                login.ShowNotice(MsgSessionExpired);
            };

            Label player = null;
            Button register = null;
            router.Register(ScreenId.Menu, () =>
            {
                VisualElement view = Instantiate(assets.mainMenu);
                player = view.Q<Label>("menu-player");
                Bind(view, "menu-play", router, ScreenId.PackSelect);
                Bind(view, "menu-rules", router, ScreenId.Rules);
                Bind(view, "menu-stats", router, ScreenId.Stats);
                Bind(view, "menu-leaderboard", router, ScreenId.Leaderboard);
                Bind(view, "menu-tutorial", router, ScreenId.Tutorial);

                // Sin conexión no hay nombre en el servidor: esta opción vuelve al formulario para conseguir uno.
                register = view.Q<Button>("menu-register");
                if (register == null) throw new InvalidOperationException("Falta el botón 'menu-register'.");
                register.clicked += () => router.Show(ScreenId.Login);
                return view;
            }, () =>
            {
                bool offline = !context.HasIdentity;
                player.text = offline ? TextOffline : "Jugando como " + context.Profile.playerName;
                player.EnableInClassList("menu__player--offline", offline);
                UiKit.Show(register, offline);
            });

            Register(router, ScreenId.Rules, assets.rules, v => new RulesController(v, context, go), c => c.OnShow(), c => go(ScreenId.Menu));
            Register(router, ScreenId.RuleEditor, assets.ruleEditor, v => new RuleEditorController(v, context, go), c => c.OnShow(), c => c.Back());
            Register(router, ScreenId.PackSelect, assets.packSelect, v => new PackSelectController(v, context, go), c => c.OnShow(), c => go(ScreenId.Menu));
            Register(router, ScreenId.Game, assets.game, v => new GameController(v, context, go), c => c.OnShow(), c => c.Back());
            Register(router, ScreenId.Result, assets.result, v => new ResultController(v, context, go), c => c.OnShow(), c => go(ScreenId.Menu));
            Register(router, ScreenId.Stats, assets.stats, v => new StatsController(v, context, go), c => c.OnShow(), c => go(ScreenId.Menu));
            Register(router, ScreenId.Leaderboard, assets.leaderboard, v => new LeaderboardController(v, context, go), c => c.OnShow(), c => go(ScreenId.Menu));
            Register(router, ScreenId.Tutorial, assets.tutorial, v => new TutorialController(v, context, go), c => c.OnShow(), c => c.Back());

            // La pantalla solo se mantiene encendida mientras una partida corre en automático.
            router.Changed += id => { if (id != ScreenId.Game) ScreenAwake.Set(false); };

            return router;
        }

        private static void Register<T>(ScreenRouter router, ScreenId id, VisualTreeAsset asset, Func<VisualElement, T> create, Action<T> onShow, Action<T> onBack)
        {
            T controller = default(T);
            router.Register(id, () =>
            {
                VisualElement view = Instantiate(asset);
                controller = create(view);
                view.userData = controller;
                return view;
            }, () => onShow(controller), () => onBack(controller));
        }

        private static VisualElement Instantiate(VisualTreeAsset asset)
        {
            if (asset == null) throw new InvalidOperationException("Falta una vista en UiAssets.");
            VisualElement view = asset.CloneTree();
            view.AddToClassList("screen-root");
            return view;
        }

        private static void Bind(VisualElement view, string button, ScreenRouter router, ScreenId target)
        {
            Button element = view.Q<Button>(button);
            if (element == null) throw new InvalidOperationException("Falta el botón '" + button + "'.");
            element.clicked += () => router.Show(target);
        }
    }
}
