using System;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>Resultado de la última partida: causa, pasos, fuegos, ratio, puntos y posición.</summary>
    public sealed class ResultController
    {
        public const string RankNotRegistered = "No registrada";
        public const string RankOutsideTop = "Fuera del top 100";
        public const string RankNoScore = "Sin puntuación";

        private readonly GameContext _context;
        private readonly Action<ScreenId> _go;
        private readonly VisualElement _view;

        public ResultController(VisualElement view, GameContext context, Action<ScreenId> go)
        {
            _view = view;
            _context = context;
            _go = go;
            UiKit.Require<Button>(view, "result-again").clicked += PlayAgain;
            UiKit.Require<Button>(view, "result-menu").clicked += () => _go(ScreenId.Menu);
        }

        public void OnShow()
        {
            SessionResult r = _context.LastResult;
            Label note = Label("result-note");
            if (r == null)
            {
                Label("result-cause").text = "Aún no has terminado ninguna partida.";
                foreach (string n in new[] { "result-pack", "result-steps", "result-fires", "result-ratio", "result-points", "result-rank" }) Label(n).text = "-";
                UiKit.Show(note, false);
                return;
            }

            Label("result-cause").text = Labels.Cause(r.EndReason);
            Label("result-pack").text = "Pack: " + _context.LastResultPack;
            Label("result-steps").text = Labels.Number(r.Steps);
            Label("result-fires").text = Labels.Number(r.Fires);
            Label("result-ratio").text = Labels.Ratio(r.Fires, r.Steps);
            Label("result-points").text = Labels.Number(r.Points);
            Label("result-rank").text = Rank(r);

            string text = null;
            if (_context.LastResultManual)
                text = "Sin puntuación: esta partida usó los controles manuales de prueba, así que no cuenta en la clasificación ni en las estadísticas.";
            else if (!r.Registered)
                text = "No se pudo registrar en la clasificación porque no hay conexión con el servidor. La partida sí cuenta en tus estadísticas de este dispositivo.";
            note.text = text ?? "";
            UiKit.Show(note, text != null);
        }

        private string Rank(SessionResult r)
        {
            if (_context.LastResultManual) return RankNoScore;
            if (!r.Registered) return RankNotRegistered;
            if (r.Rank.HasValue) return "Nueva marca: puesto " + Labels.Number(r.Rank.Value);
            if (r.PlayerRank.HasValue) return "No mejora tu marca · Tu puesto: " + Labels.Number(r.PlayerRank.Value);
            return RankOutsideTop;
        }

        private void PlayAgain()
        {
            bool canPlay = _context.Packs.CanPlay(_context.PlayPack);
            _go(canPlay ? ScreenId.Game : ScreenId.PackSelect);
        }

        private Label Label(string name)
        {
            return UiKit.Require<Label>(_view, name);
        }
    }
}
