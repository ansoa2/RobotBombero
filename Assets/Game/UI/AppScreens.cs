using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>Lo que comparten las pantallas: datos locales, servidor, perfil y cómo lanzar corrutinas.</summary>
    public sealed class GameContext
    {
        public GameContext(LocalStore store, IApiClient api, Action<IEnumerator> run)
        {
            if (store == null) throw new ArgumentNullException("store");
            if (api == null) throw new ArgumentNullException("api");
            if (run == null) throw new ArgumentNullException("run");
            Store = store;
            Api = api;
            Run = run;
            Profile = store.LoadProfile();
        }

        public LocalStore Store { get; private set; }
        public IApiClient Api { get; private set; }
        public Action<IEnumerator> Run { get; private set; }
        public Profile Profile { get; set; }

        /// <summary>Solo el servidor concede la identidad: hace falta nombre y clave.</summary>
        public bool HasIdentity
        {
            get { return Profile != null && !string.IsNullOrEmpty(Profile.playerName) && !string.IsNullOrEmpty(Profile.playerKey); }
        }
    }

    [Serializable]
    public sealed class UiAssets
    {
        public VisualTreeAsset login;
        public VisualTreeAsset mainMenu;
        public VisualTreeAsset placeholder;
        public Texture2D backgroundPortrait;
        public Texture2D backgroundLandscape;
    }

    /// <summary>Une las vistas con el enrutador. Las pantallas sin construir usan una vista provisional con «Volver».</summary>
    public static class AppScreens
    {
        private static readonly Dictionary<ScreenId, string> PlaceholderTitles = new Dictionary<ScreenId, string>
        {
            { ScreenId.Rules, "Editar reglas" },
            { ScreenId.RuleEditor, "Editor de reglas" },
            { ScreenId.PackSelect, "Elegir pack" },
            { ScreenId.Game, "Partida" },
            { ScreenId.Result, "Resultado" },
            { ScreenId.Stats, "Estadísticas" },
            { ScreenId.Leaderboard, "Clasificación global" }
        };

        public static ScreenRouter Compose(VisualElement root, UiAssets assets, GameContext context)
        {
            if (assets == null) throw new ArgumentNullException("assets");
            if (context == null) throw new ArgumentNullException("context");

            var router = new ScreenRouter(root, () => context.HasIdentity, assets.backgroundPortrait, assets.backgroundLandscape);

            router.Register(ScreenId.Login, () =>
            {
                VisualElement view = Instantiate(assets.login);
                new LoginController(view, context, () => router.Show(ScreenId.Menu));
                return view;
            });

            Label player = null;
            router.Register(ScreenId.Menu, () =>
            {
                VisualElement view = Instantiate(assets.mainMenu);
                player = view.Q<Label>("menu-player");
                Bind(view, "menu-play", router, ScreenId.PackSelect);
                Bind(view, "menu-rules", router, ScreenId.Rules);
                Bind(view, "menu-stats", router, ScreenId.Stats);
                Bind(view, "menu-leaderboard", router, ScreenId.Leaderboard);
                return view;
            }, () => player.text = "Jugando como " + context.Profile.playerName);

            foreach (KeyValuePair<ScreenId, string> placeholder in PlaceholderTitles)
            {
                string title = placeholder.Value;
                router.Register(placeholder.Key, () =>
                {
                    VisualElement view = Instantiate(assets.placeholder);
                    view.Q<Label>("placeholder-title").text = title;
                    Bind(view, "placeholder-back", router, ScreenId.Menu);
                    return view;
                });
            }

            return router;
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
