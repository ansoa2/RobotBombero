using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    public enum ScreenId
    {
        Login,
        Menu,
        Rules,
        RuleEditor,
        PackSelect,
        Game,
        Result,
        Stats,
        Leaderboard,
        Tutorial
    }

    /// <summary>Márgenes en píxeles del panel.</summary>
    public readonly struct RectOffsetF
    {
        public readonly float Left, Right, Top, Bottom;

        public RectOffsetF(float left, float right, float top, float bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }
    }

    public static class SafeArea
    {
        /// <summary>Convierte la zona segura (píxeles de pantalla, con el origen abajo) en márgenes del panel.</summary>
        public static RectOffsetF Insets(Rect safeArea, Vector2 screen, Vector2 panel)
        {
            if (screen.x <= 0 || screen.y <= 0 || panel.x <= 0 || panel.y <= 0) return new RectOffsetF(0, 0, 0, 0);
            float sx = panel.x / screen.x;
            float sy = panel.y / screen.y;
            return new RectOffsetF(
                Mathf.Max(0, safeArea.xMin) * sx,
                Mathf.Max(0, screen.x - safeArea.xMax) * sx,
                Mathf.Max(0, screen.y - safeArea.yMax) * sy,
                Mathf.Max(0, safeArea.yMin) * sy);
        }
    }

    /// <summary>
    /// Decide qué pantalla se ve y adapta el marco a la orientación. No necesita una escena: recibe el árbol de Root.uxml.
    /// Sin acceso (ni identidad concedida por el servidor ni juego sin conexión), cualquier destino lleva al acceso.
    /// </summary>
    public sealed class ScreenRouter
    {
        public const string LandscapeClass = "is-landscape";
        /// <summary>Vertical pero ancho (tabletas): el fondo recorta distinto y el menú se compacta.</summary>
        public const string TabletClass = "is-tablet";
        public const float TabletAspect = 0.65f;

        private sealed class Entry
        {
            public Func<VisualElement> Build;
            public Action OnShow;
            public Action OnBack;
            public VisualElement View;
        }

        private readonly Dictionary<ScreenId, Entry> _screens = new Dictionary<ScreenId, Entry>();
        private readonly Func<bool> _hasIdentity;
        private readonly Texture2D _portrait;
        private readonly Texture2D _landscape;
        private readonly VisualElement _app;
        private readonly VisualElement _background;
        private readonly VisualElement _safe;
        private readonly VisualElement _host;

        public ScreenRouter(VisualElement root, Func<bool> hasIdentity, Texture2D portrait, Texture2D landscape)
        {
            if (root == null) throw new ArgumentNullException("root");
            if (hasIdentity == null) throw new ArgumentNullException("hasIdentity");
            _hasIdentity = hasIdentity;
            _portrait = portrait;
            _landscape = landscape;
            _app = Require(root, "app");
            _background = Require(root, "background");
            _safe = Require(root, "safe-area");
            _host = Require(root, "screen-host");
            _app.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        public ScreenId? Current { get; private set; }
        public bool IsLandscape { get; private set; }
        public Texture2D CurrentBackground { get; private set; }

        /// <summary>Asocia una vista a una pantalla; sustituye la anterior. La vista se crea la primera vez que se muestra y se conserva.</summary>
        public void Register(ScreenId id, Func<VisualElement> build, Action onShow = null, Action onBack = null)
        {
            if (build == null) throw new ArgumentNullException("build");
            _screens[id] = new Entry { Build = build, OnShow = onShow, OnBack = onBack };
        }

        /// <summary>Se ha mostrado una pantalla (después de su OnShow).</summary>
        public event Action<ScreenId> Changed;

        /// <summary>
        /// El botón Atrás de Android o Esc en Windows: cierra un diálogo abierto o vuelve una pantalla, con lo que haga el
        /// botón Atrás de esa pantalla (en la partida, la confirmación de salir). Devuelve falso si no hay adónde volver (menú y acceso).
        /// </summary>
        public bool HandleBack()
        {
            Entry entry;
            if (Current == null || !_screens.TryGetValue(Current.Value, out entry) || entry.View == null) return false;
            if (Dialog.IsOpen(entry.View)) { Dialog.Close(entry.View); return true; }
            if (entry.OnBack == null) return false;
            entry.OnBack();
            return true;
        }

        public void ShowStart()
        {
            Show(_hasIdentity() ? ScreenId.Menu : ScreenId.Login);
        }

        public void Show(ScreenId id)
        {
            if (id != ScreenId.Login && !_hasIdentity()) id = ScreenId.Login;

            Entry entry;
            if (!_screens.TryGetValue(id, out entry)) throw new InvalidOperationException("Pantalla sin vista: " + id);
            if (entry.View == null) entry.View = entry.Build();

            _host.Clear();
            _host.Add(entry.View);
            Current = id;
            if (entry.OnShow != null) entry.OnShow();
            if (Changed != null) Changed(id);
        }

        /// <summary>Elige clase y fondo según el tamaño del panel. Un panel cuadrado cuenta como vertical.</summary>
        public void ApplyLayout(float width, float height)
        {
            if (width <= 0 || height <= 0) return;

            IsLandscape = width > height;
            _app.EnableInClassList(LandscapeClass, IsLandscape);
            _app.EnableInClassList(TabletClass, !IsLandscape && width / height >= TabletAspect);

            Texture2D background = IsLandscape ? _landscape : _portrait;
            if (background != CurrentBackground)
            {
                CurrentBackground = background;
                _background.style.backgroundImage = new StyleBackground(background);
            }
        }

        private void OnGeometryChanged(GeometryChangedEvent e)
        {
            ApplyLayout(e.newRect.width, e.newRect.height);

            RectOffsetF insets = SafeArea.Insets(Screen.safeArea, new Vector2(Screen.width, Screen.height), e.newRect.size);
            _safe.style.paddingLeft = insets.Left;
            _safe.style.paddingRight = insets.Right;
            _safe.style.paddingTop = insets.Top;
            _safe.style.paddingBottom = insets.Bottom;
        }

        private static VisualElement Require(VisualElement root, string name)
        {
            VisualElement element = root.Q(name);
            if (element == null) throw new InvalidOperationException("Falta el elemento '" + name + "' en la interfaz.");
            return element;
        }
    }
}
