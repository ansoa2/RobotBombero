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
        Leaderboard
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
    /// Sin una identidad concedida por el servidor, cualquier destino lleva al acceso.
    /// </summary>
    public sealed class ScreenRouter
    {
        public const string LandscapeClass = "is-landscape";

        private sealed class Entry
        {
            public Func<VisualElement> Build;
            public Action OnShow;
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
        public void Register(ScreenId id, Func<VisualElement> build, Action onShow = null)
        {
            if (build == null) throw new ArgumentNullException("build");
            _screens[id] = new Entry { Build = build, OnShow = onShow };
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
        }

        /// <summary>Elige clase y fondo según el tamaño del panel. Un panel cuadrado cuenta como vertical.</summary>
        public void ApplyLayout(float width, float height)
        {
            if (width <= 0 || height <= 0) return;

            IsLandscape = width > height;
            _app.EnableInClassList(LandscapeClass, IsLandscape);

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
