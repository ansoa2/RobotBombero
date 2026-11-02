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

            UsesServerAddress = server != null && server.Enabled;
            ServerUrl = "";
            if (UsesServerAddress)
            {
                // Lo elegido en este dispositivo manda; en una instalación limpia, la dirección fijada al compilar.
                string url = store.LoadServerAddress();
                if (string.IsNullOrEmpty(url))
                {
                    ServerAddressResult fallback = ServerAddress.Normalize(server.DefaultAddress);
                    url = fallback.IsValid ? fallback.Url : "";
                }
                ApplyServerUrl(url);
            }
        }

        /// <summary>Verdadero en las apps nativas: el jugador elige el servidor. En WebGL las rutas son relativas.</summary>
        public bool UsesServerAddress { get; private set; }

        /// <summary>URL base actual del servidor (http://host:puerto), o vacío si aún no hay ninguna.</summary>
        public string ServerUrl { get; private set; }

        /// <summary>Valida y guarda la dirección y la aplica al cliente de red. Devuelve el error (en español) o nulo.</summary>
        public string SetServerAddress(string input)
        {
            ServerAddressResult result = ServerAddress.Normalize(input);
            if (!result.IsValid) return result.Error;
            try { Store.SaveServerAddress(result.Url); }
            catch (System.IO.IOException) { return "No se ha podido guardar la dirección en este dispositivo."; }
            catch (UnauthorizedAccessException) { return "No se ha podido guardar la dirección en este dispositivo."; }
            ApplyServerUrl(result.Url);
            return null;
        }

        private void ApplyServerUrl(string url)
        {
            ServerUrl = url;
            var target = Api as IServerTarget;
            if (target != null) target.BaseUrl = url;
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

        /// <summary>Si la respuesta es un 401, cierra la sesión y avisa a la interfaz. Devuelve si lo era.</summary>
        public bool ExpireIfUnauthorized<T>(ApiResult<T> result)
        {
            if (result == null || result.Status != ApiStatus.Rejected || result.HttpStatus != 401) return false;
            ExpireSession();
            return true;
        }

        /// <summary>Borra la identidad del perfil; los packs y sus estadísticas se quedan, sin los ids del servidor.</summary>
        public void ExpireSession()
        {
            Profile = new Profile();
            try { Store.SaveProfile(Profile); }
            catch (System.IO.IOException) { Debug.LogWarning("RobotBombero: no se pudo guardar el perfil al cerrar la sesión."); }
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
        public Texture2D backgroundPortrait;
        public Texture2D backgroundLandscape;
    }

    /// <summary>Une las vistas con el enrutador y cada una con su controlador.</summary>
    public static class AppScreens
    {
        public const string MsgSessionExpired = "Tu sesión ya no es válida. Vuelve a entrar con un nombre.";

        public static ScreenRouter Compose(VisualElement root, UiAssets assets, GameContext context)
        {
            if (assets == null) throw new ArgumentNullException("assets");
            if (context == null) throw new ArgumentNullException("context");

            var router = new ScreenRouter(root, () => context.HasIdentity, assets.backgroundPortrait, assets.backgroundLandscape);
            Action<ScreenId> go = id => router.Show(id);

            LoginController login = null;
            router.Register(ScreenId.Login, () =>
            {
                VisualElement view = Instantiate(assets.login);
                login = new LoginController(view, context, () =>
                {
                    router.Show(ScreenId.Menu);
                    context.Run(context.Packs.SyncPending());
                });
                view.userData = login;
                return view;
            });
            context.SessionExpired += () =>
            {
                router.Show(ScreenId.Login);
                login.ShowNotice(MsgSessionExpired);
            };

            Label player = null;
            Button serverButton = null;
            router.Register(ScreenId.Menu, () =>
            {
                VisualElement view = Instantiate(assets.mainMenu);
                player = view.Q<Label>("menu-player");
                Bind(view, "menu-play", router, ScreenId.PackSelect);
                Bind(view, "menu-rules", router, ScreenId.Rules);
                Bind(view, "menu-stats", router, ScreenId.Stats);
                Bind(view, "menu-leaderboard", router, ScreenId.Leaderboard);

                // Solo las apps nativas eligen servidor; la dirección del ordenador puede cambiar.
                serverButton = view.Q<Button>("menu-server");
                if (serverButton == null) throw new InvalidOperationException("Falta el botón 'menu-server'.");
                UiKit.Show(serverButton, context.UsesServerAddress);
                serverButton.clicked += () => Dialog.Prompt(view, "Dirección del servidor", ServerAddress.Display(context.ServerUrl),
                    100, "Guardar", text => context.SetServerAddress(text));
                return view;
            }, () =>
            {
                player.text = "Jugando como " + context.Profile.playerName;
                if (context.UsesServerAddress) serverButton.text = "Servidor: " + ServerAddress.Display(context.ServerUrl);
            });

            Register(router, ScreenId.Rules, assets.rules, v => new RulesController(v, context, go), c => c.OnShow());
            Register(router, ScreenId.RuleEditor, assets.ruleEditor, v => new RuleEditorController(v, context, go), c => c.OnShow());
            Register(router, ScreenId.PackSelect, assets.packSelect, v => new PackSelectController(v, context, go), c => c.OnShow());
            Register(router, ScreenId.Game, assets.game, v => new GameController(v, context, go), c => c.OnShow());
            Register(router, ScreenId.Result, assets.result, v => new ResultController(v, context, go), c => c.OnShow());
            Register(router, ScreenId.Stats, assets.stats, v => new StatsController(v, context, go), c => c.OnShow());
            Register(router, ScreenId.Leaderboard, assets.leaderboard, v => new LeaderboardController(v, context, go), c => c.OnShow());

            return router;
        }

        private static void Register<T>(ScreenRouter router, ScreenId id, VisualTreeAsset asset, Func<VisualElement, T> create, Action<T> onShow)
        {
            T controller = default(T);
            router.Register(id, () =>
            {
                VisualElement view = Instantiate(asset);
                controller = create(view);
                view.userData = controller;
                return view;
            }, () => onShow(controller));
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
