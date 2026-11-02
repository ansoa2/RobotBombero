using System;
using System.Collections;
using RobotBombero.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>
    /// Pantalla de partida: tablero, estado del robot, Paso, Automático, velocidad y modo de prueba manual.
    /// Una partida abandonada (se sale antes de que termine) no se envía ni cuenta en las estadísticas.
    /// </summary>
    public sealed class GameController
    {
        private const string BadgeText = "Sin puntuación";
        private static readonly string[] SensorKeys = { "battery", "temp", "light", "prox", "cell" };

        private readonly VisualElement _view;
        private readonly GameContext _context;
        private readonly Action<ScreenId> _go;
        private readonly BoardView _board;
        private readonly Label _pack, _badge, _status, _battery, _steps, _fires, _testNote;
        private readonly VisualElement _batteryBar, _testPanel, _boardHolder;
        private readonly Button _back, _step, _auto, _testToggle;
        private readonly Button[] _speeds = new Button[GameSession.Speeds.Length];
        private readonly Button[] _manual = new Button[4];
        private readonly Label[] _sensors = new Label[SensorKeys.Length];

        private GameSession _session;
        private int _generation;
        private bool _manualUsed;
        private bool _ending;
        private double _lastTime;

        public GameController(VisualElement view, GameContext context, Action<ScreenId> go)
        {
            _view = view;
            _context = context;
            _go = go;
            _pack = UiKit.Require<Label>(view, "game-pack");
            _badge = UiKit.Require<Label>(view, "game-badge");
            _status = UiKit.Require<Label>(view, "game-status");
            _battery = UiKit.Require<Label>(view, "game-battery");
            _steps = UiKit.Require<Label>(view, "game-steps");
            _fires = UiKit.Require<Label>(view, "game-fires");
            _testNote = UiKit.Require<Label>(view, "game-test-note");
            _batteryBar = UiKit.Require<VisualElement>(view, "game-battery-bar");
            _testPanel = UiKit.Require<VisualElement>(view, "game-test-panel");
            _boardHolder = UiKit.Require<VisualElement>(view, "board-holder");
            _back = UiKit.Require<Button>(view, "game-back");
            _step = UiKit.Require<Button>(view, "game-step");
            _auto = UiKit.Require<Button>(view, "game-auto");
            _testToggle = UiKit.Require<Button>(view, "game-test-toggle");
            for (int i = 0; i < SensorKeys.Length; i++) _sensors[i] = UiKit.Require<Label>(view, "sensor-" + SensorKeys[i]);

            _board = new BoardView(UiKit.Require<VisualElement>(view, "board"), UiKit.Require<VisualElement>(view, "board-grid"));
            _boardHolder.RegisterCallback<GeometryChangedEvent>(e => _board.Fit(e.newRect.width, e.newRect.height));

            _back.clicked += Back;
            _step.clicked += Step;
            _auto.clicked += ToggleAuto;
            _testToggle.clicked += ToggleTestPanel;

            VisualElement speedRow = UiKit.Require<VisualElement>(view, "game-speeds");
            for (int i = 0; i < _speeds.Length; i++)
            {
                int index = i;
                _speeds[i] = UiKit.Button("game-speed-" + i, GameSession.Speeds[i].ToString(), () => SetSpeed(index), "chip");
                speedRow.Add(_speeds[i]);
            }

            VisualElement manualRow = UiKit.Require<VisualElement>(view, "game-manual");
            for (int i = 0; i < _manual.Length; i++)
            {
                string action = Labels.ActionValues[i];
                _manual[i] = UiKit.Button("game-manual-" + i, Labels.Value(action), () => Manual(action), "btn--small", "game__manual");
                manualRow.Add(_manual[i]);
            }

            // Un solo reloj para el modo automático: la sesión limita los pasos por fotograma.
            view.schedule.Execute(OnFrame).Every(16);
        }

        public GameSession Session { get { return _session; } }

        public void OnShow()
        {
            _generation++;
            Dialog.Close(_view);
            _session = null;
            _manualUsed = false;
            _ending = false;
            _lastTime = Time.realtimeSinceStartupAsDouble;
            _board.Reset();
            UiKit.Show(_testPanel, false);
            UiKit.Show(_badge, false);
            _testNote.text = "Sin puntuación: con estos botones la partida no puntúa ni cuenta en las estadísticas.";
            SetControls(false);
            ShowCounters(0, 0, 50);

            LocalPack pack = _context.PlayPack;
            if (pack == null) { _pack.text = ""; _status.text = "Elige un pack para jugar."; return; }
            _pack.text = pack.content.name;
            if (!_context.Packs.CanPlay(pack)) { _status.text = "Este pack no tiene reglas: añade al menos una en Editar reglas."; return; }

            _status.text = "Preparando la partida…";
            var session = new GameSession(pack, _context.Profile, _context.Api, _context.Store);
            session.OnEnded += () => OnEnded(session);
            session.OnResult += result => OnResult(session, result);
            _context.Run(Begin(session, _generation));
        }

        private IEnumerator Begin(GameSession session, int generation)
        {
            yield return session.Begin();
            if (generation != _generation) yield break;
            if (session.Unauthorized) { _context.ExpireSession(); yield break; }

            _session = session;
            SetControls(true);
            UpdateBadge();
            RefreshSpeeds();
            UpdateAutoButton();
            _status.text = session.IsRanked
                ? "Paso avanza un ciclo (la primera regla que se cumple, con todas sus acciones); Automático sigue solo."
                : "Sin conexión: esta partida no puntúa, pero cuenta en tus estadísticas.";
            Render();
        }

        // --- Controles ---

        private void Step()
        {
            if (_session == null) return;
            _session.StepOnce();
            Render();
        }

        private void ToggleTestPanel()
        {
            bool open = _testPanel.style.display == DisplayStyle.None;
            UiKit.Show(_testPanel, open);
            // El panel nace bajo el pliegue: se lleva a la vista para que se vean los botones.
            if (open) _view.schedule.Execute(() => UiKit.Require<ScrollView>(_view, "game-controls").ScrollTo(_testPanel));
        }

        private void ToggleAuto()
        {
            if (_session == null) return;
            _session.SetAuto(_session.Mode != SessionMode.Auto);
            UpdateAutoButton();
        }

        private void SetSpeed(int index)
        {
            if (_session == null) return;
            _session.SetSpeed(index);
            RefreshSpeeds();
        }

        private void Manual(string action)
        {
            if (_session == null || _session.IsEnded) return;
            _manualUsed = true;
            _session.SetAuto(false);
            _session.ApplyManual(action);
            UpdateBadge();
            UpdateAutoButton();
            Render();
        }

        /// <summary>Avanza el modo automático. Lo llama el reloj de la pantalla; las pruebas lo llaman a mano.</summary>
        public void Tick(float deltaSeconds)
        {
            if (_session == null || _session.Mode != SessionMode.Auto) return;
            _session.Tick(deltaSeconds);
            UpdateAutoButton();
            Render();
        }

        private void OnFrame()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float delta = (float)Math.Min(now - _lastTime, 0.1);
            _lastTime = now;
            Tick(delta);
        }

        private void Back()
        {
            bool inProgress = _session != null && !_session.IsEnded
                && (_session.Simulation.Steps > 0 || _manualUsed || _session.Mode == SessionMode.Auto);
            if (!inProgress) { Leave(); return; }

            if (_session.Mode == SessionMode.Auto) { _session.SetAuto(false); UpdateAutoButton(); }
            Dialog.Confirm(_view, "¿Salir de la partida?", "La partida se detendrá y no contará para la clasificación ni para tus estadísticas.", "Salir", Leave);
        }

        private void Leave()
        {
            _generation++;
            if (_session != null) _session.SetAuto(false);
            _session = null;
            _go(ScreenId.PackSelect);
        }

        // --- Fin de la partida ---

        private void OnEnded(GameSession session)
        {
            if (session != _session) return;
            _ending = true;
            SetControls(false);
            _status.text = "Fin de la partida: " + Labels.Cause(session.Simulation.EndReason.ToString()) + ". Guardando el resultado…";
            _context.Run(session.Finish());
        }

        private void OnResult(GameSession session, SessionResult result)
        {
            if (session != _session) return;
            if (session.Unauthorized) { _context.ExpireSession(); return; }
            _context.LastResult = result;
            _context.LastResultPack = session.Pack.content.name;
            _context.LastResultManual = _manualUsed;
            _go(ScreenId.Result);
        }

        // --- Dibujo ---

        private void Render()
        {
            if (_session == null || _session.Simulation == null) return;
            GameSimulation sim = _session.Simulation;
            _board.Render(sim);
            ShowCounters(sim.Steps, sim.Fires, sim.Battery);
            SetText(_sensors[0], Labels.Value(sim.BatteryState));
            SetText(_sensors[1], Labels.Value(sim.SensorTemp));
            SetText(_sensors[2], Labels.Value(sim.SensorLight));
            SetText(_sensors[3], Labels.Value(sim.SensorProx));
            SetText(_sensors[4], Labels.Value(sim.SensorCell));
        }

        private void ShowCounters(int steps, int fires, int battery)
        {
            SetText(_steps, Labels.Number(steps));
            SetText(_fires, Labels.Number(fires));
            SetText(_battery, battery.ToString());
            _batteryBar.style.width = Length.Percent(Mathf.Clamp(battery * 2f, 0f, 100f));
            _batteryBar.EnableInClassList("bar--low", battery < 10);
            _batteryBar.EnableInClassList("bar--medium", battery >= 10 && battery <= 30);
        }

        private static void SetText(Label label, string text)
        {
            if (label.text != text) label.text = text;
        }

        private void SetControls(bool enabled)
        {
            _step.SetEnabled(enabled);
            _auto.SetEnabled(enabled);
            foreach (Button b in _manual) b.SetEnabled(enabled);
            foreach (Button b in _speeds) b.SetEnabled(enabled);
            _testToggle.SetEnabled(enabled || _ending);
            _back.SetEnabled(!_ending);
        }

        private void UpdateBadge()
        {
            bool unranked = _session != null && !_session.IsRanked;
            _badge.text = BadgeText;
            UiKit.Show(_badge, unranked);
        }

        private void UpdateAutoButton()
        {
            bool auto = _session != null && _session.Mode == SessionMode.Auto;
            _auto.text = auto ? "Pausar" : "Automático";
            _auto.EnableInClassList("is-selected", auto);
        }

        private void RefreshSpeeds()
        {
            for (int i = 0; i < _speeds.Length; i++) _speeds[i].EnableInClassList("is-selected", _session != null && _session.SpeedIndex == i);
        }
    }
}
