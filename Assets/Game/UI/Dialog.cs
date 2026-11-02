using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>Diálogos modales de confirmación y de pedir un nombre. Se dibujan sobre la pantalla que los abre.</summary>
    public static class Dialog
    {
        public const string Name = "dialog";

        public static bool IsOpen(VisualElement host)
        {
            return host.Q(Name) != null;
        }

        public static void Close(VisualElement host)
        {
            VisualElement open = host.Q(Name);
            if (open != null) open.RemoveFromHierarchy();
        }

        public static void Confirm(VisualElement host, string title, string message, string okText, Action onOk)
        {
            VisualElement card = Open(host, title);
            card.Add(UiKit.Label("dialog-message", message, "text-muted", "dialog__message"));
            Buttons(host, card, okText, () => { Close(host); onOk(); });
        }

        /// <summary>Una ventana de información con texto largo que se desplaza y un único botón para cerrarla.</summary>
        public static void Info(VisualElement host, string title, VisualElement content, string closeText)
        {
            VisualElement card = Open(host, title);
            card.AddToClassList("dialog__card--info");
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "dialog-scroll" };
            scroll.AddToClassList("dialog__scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.Add(content);
            card.Add(scroll);
            VisualElement row = UiKit.Box("dialog__buttons");
            row.Add(UiKit.Button("dialog-close", closeText, () => Close(host), "btn--main", "btn--center"));
            card.Add(row);
        }

        /// <summary>
        /// Pide un texto. submit recibe el texto y devuelve un error (el diálogo sigue abierto para corregirlo)
        /// o nulo (se cierra).
        /// </summary>
        public static void Prompt(VisualElement host, string title, string initial, int maxLength, string okText, Func<string, string> submit)
        {
            VisualElement card = Open(host, title);
            var input = new TextField { name = "dialog-input", value = initial ?? "", maxLength = maxLength };
            input.AddToClassList("field");
            card.Add(input);
            Label message = UiKit.Label("dialog-message", "", "login__message");
            UiKit.Show(message, false);
            card.Add(message);

            Action accept = () =>
            {
                string error = submit(input.value ?? "");
                if (error == null) { Close(host); return; }
                message.text = error;
                UiKit.Show(message, true);
            };
            input.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) accept();
            }, TrickleDown.TrickleDown);
            Buttons(host, card, okText, accept);
        }

        private static VisualElement Open(VisualElement host, string title)
        {
            Close(host);
            VisualElement overlay = UiKit.Box("dialog");
            overlay.name = Name;
            VisualElement card = UiKit.Box("panel", "dialog__card");
            card.Add(UiKit.Label("dialog-title", title, "dialog__title"));
            overlay.Add(card);
            host.Add(overlay);
            return card;
        }

        private static void Buttons(VisualElement host, VisualElement card, string okText, Action accept)
        {
            VisualElement row = UiKit.Box("dialog__buttons");
            row.Add(UiKit.Button("dialog-cancel", "Cancelar", () => Close(host), "btn--center"));
            row.Add(UiKit.Button("dialog-ok", okText, accept, "btn--main", "btn--center"));
            card.Add(row);
        }
    }
}
