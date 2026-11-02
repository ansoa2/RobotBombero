using System;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>"Cómo se juega": páginas cortas con Anterior y Siguiente. Igual con o sin conexión.</summary>
    public sealed class TutorialController
    {
        private readonly Action<ScreenId> _go;
        private readonly Label _counter;
        private readonly Label _title;
        private readonly ScrollView _scroll;
        private readonly VisualElement _body;
        private readonly Button _prev;
        private readonly Button _next;

        public TutorialController(VisualElement view, GameContext context, Action<ScreenId> go)
        {
            _go = go;
            _counter = UiKit.Plain(UiKit.Require<Label>(view, "tutorial-counter"));
            _title = UiKit.Plain(UiKit.Require<Label>(view, "tutorial-page-title"));
            _scroll = UiKit.Require<ScrollView>(view, "tutorial-scroll");
            _body = UiKit.Require<VisualElement>(view, "tutorial-body");
            _prev = UiKit.Require<Button>(view, "tutorial-prev");
            _next = UiKit.Require<Button>(view, "tutorial-next");

            UiKit.Require<Button>(view, "tutorial-back").clicked += Back;
            _prev.clicked += () => Show(Index - 1);
            _next.clicked += () => Show(Index + 1);
        }

        public int Index { get; private set; }

        public void OnShow()
        {
            Show(0);
        }

        public void Back()
        {
            _go(ScreenId.Menu);
        }

        private void Show(int index)
        {
            int count = TutorialPages.All.Count;
            if (index < 0 || index >= count) return;
            Index = index;
            TutorialPage page = TutorialPages.All[index];

            _counter.text = "Página " + (index + 1) + " de " + count;
            _title.text = page.Title;
            _prev.SetEnabled(index > 0);
            _next.SetEnabled(index < count - 1);

            _body.Clear();
            foreach (TutorialBlock block in page.Blocks)
            {
                VisualElement box = UiKit.Box("tutorial__block");
                if (block.Heading != null) box.Add(UiKit.Label(null, block.Heading, "tutorial__heading"));
                foreach (string line in block.Lines) box.Add(UiKit.Label(null, line, "tutorial__text"));
                _body.Add(box);
            }
            _scroll.scrollOffset = UnityEngine.Vector2.zero;
        }
    }
}
