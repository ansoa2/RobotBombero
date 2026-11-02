using System;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>Pequeños constructores de elementos para las pantallas que se rellenan por código.</summary>
    public static class UiKit
    {
        public static Label Label(string name, string text, params string[] classes)
        {
            var label = new Label(text);
            if (name != null) label.name = name;
            AddClasses(label, classes);
            return label;
        }

        public static Button Button(string name, string text, Action click, params string[] classes)
        {
            var button = new Button(click) { text = text };
            if (name != null) button.name = name;
            button.AddToClassList("btn");
            AddClasses(button, classes);
            return button;
        }

        public static VisualElement Box(params string[] classes)
        {
            var box = new VisualElement();
            AddClasses(box, classes);
            return box;
        }

        public static void Show(VisualElement element, bool shown)
        {
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static T Require<T>(VisualElement view, string name) where T : VisualElement
        {
            T element = view.Q<T>(name);
            if (element == null) throw new InvalidOperationException("Falta el elemento '" + name + "' en la pantalla.");
            return element;
        }

        private static void AddClasses(VisualElement element, string[] classes)
        {
            foreach (string c in classes)
            {
                if (!string.IsNullOrEmpty(c)) element.AddToClassList(c);
            }
        }
    }
}
