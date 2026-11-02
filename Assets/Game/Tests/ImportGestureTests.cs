using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game.Tests
{
    /// <summary>
    /// El selector de archivos debe armarse con un PointerDown real que atraviese un panel: Clickable lo detiene
    /// al llegar al botón, así que una devolución en la fase normal no se ejecutaría.
    /// </summary>
    public class ImportGestureTests : UiFixture
    {


        private GameObject _host;
        private PanelSettings _settings;

        [TearDown]
        public void CloseWindow()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        private Button ImportButtonInPanel()
        {
            Start();
            VisualElement view = Load<VisualTreeAsset>("Screens/Rules.uxml").CloneTree();
            var rules = new RulesController(view, Context, id => { });
            rules.OnShow();
            // Un panel de ejecución real (UIDocument); no necesita dispositivo gráfico mientras no se pinte.
            _settings = ScriptableObject.CreateInstance<PanelSettings>();
            _host = new GameObject("rb-test-panel");
            var document = _host.AddComponent<UIDocument>();
            document.panelSettings = _settings;
            document.rootVisualElement.Add(view);
            Assert.IsNotNull(view.panel, "Hace falta un panel real");
            return view.Q<Button>("rules-import");
        }

        private static void Press(Button button, int mouseButton)
        {
            var system = new Event { type = EventType.MouseDown, button = mouseButton, mousePosition = Vector2.zero };
            using (PointerDownEvent e = PointerDownEvent.GetPooled(system))
            {
                e.target = button;
                button.SendEvent(e);
            }
        }

        [Test]
        public void PointerDown_ThroughPanel_ArmsThePicker()
        {
            Button import = ImportButtonInPanel();

            Press(import, 0);

            Assert.AreEqual(1, Files.PickCalls);
        }

        [Test]
        public void RightButton_DoesNotArmThePicker()
        {
            Button import = ImportButtonInPanel();

            Press(import, 1);

            Assert.AreEqual(0, Files.PickCalls);
        }
    }
}
