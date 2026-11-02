using System;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>Resultado de la última partida: causa, pasos, fuegos, ratio, puntos y posición.</summary>
    public sealed class ResultController
    {
        public const string RankNotRegistered = "No registrada";
        public const string RankOutsideTop = "Fuera del top 100";
        public const string RankNoPoints = "Sin puntos";
        public const string RankNoScore = "Sin puntuación";

        private readonly GameContext _context;
        private readonly Action<ScreenId> _go;
        private readonly VisualElement _view;
        private readonly Button _retry;
        private bool _retrying;

        public ResultController(VisualElement view, GameContext context, Action<ScreenId> go)
        {
            _view = view;
            _context = context;
            _go = go;
            UiKit.Plain(UiKit.Require<Label>(view, "result-pack"));
            UiKit.Require<Button>(view, "result-again").clicked += PlayAgain;
            UiKit.Require<Button>(view, "result-menu").clicked += () => _go(ScreenId.Menu);
            _retry = UiKit.Require<Button>(view, "result-retry");
            _retry.clicked += Retry;
        }

        public void OnShow()
        {
            _retrying = false;
            SessionResult r = _context.LastResult;
            Label note = Label("result-note");
            UiKit.Show(_retry, false);
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
            if (_context.LastResultManual) text = Labels.Unranked(UnrankedReason.Manual);
            else if (!r.Registered) text = Labels.Unranked(r.Reason == UnrankedReason.None ? UnrankedReason.Offline : r.Reason);
            note.text = text ?? "";
            UiKit.Show(note, text != null);
            UiKit.Show(_retry, !_context.LastResultManual && !r.Registered && r.Reason == UnrankedReason.FinishPending && r.Pending != null);
        }

        private string Rank(SessionResult r)
        {
            if (_context.LastResultManual) return RankNoScore;
            if (!r.Registered) return RankNotRegistered;
            if (r.Rank.HasValue) return "Nueva marca: puesto " + Labels.Number(r.Rank.Value);
            if (r.PlayerRank.HasValue) return "No mejora tu marca · Tu puesto: " + Labels.Number(r.PlayerRank.Value);
            return r.Points <= 0 ? RankNoPoints : RankOutsideTop;
        }

        /// <summary>Vuelve a pedir al servidor el cierre pendiente de esta partida y, si llega, cambia el resultado en pantalla.</summary>
        private void Retry()
        {
            SessionResult r = _context.LastResult;
            if (_retrying || r == null || r.Pending == null) return;
            _retrying = true;
            _retry.SetEnabled(false);
            _context.Run(RetryRoutine(r));
        }

        private System.Collections.IEnumerator RetryRoutine(SessionResult shown)
        {
            ApiResult<GameResult> result = null;
            yield return _context.Packs.RetryFinish(shown.Pending, x => result = x);
            _retrying = false;
            _retry.SetEnabled(true);
            // El jugador pudo irse a otra partida mientras tanto.
            if (!ReferenceEquals(_context.LastResult, shown)) yield break;
            if (!_context.HasIdentity) yield break;

            if (result != null && result.IsOk && result.Value != null)
            {
                GameResult server = result.Value;
                _context.LastResult = new SessionResult
                {
                    Steps = server.Steps,
                    Fires = server.Fires,
                    Points = server.Points,
                    EndReason = server.EndReason,
                    Rank = server.Rank,
                    PlayerRank = server.PlayerRank,
                    FromServer = true,
                    Registered = true
                };
            }
            else if (!PackManager.IsRetryable(result))
            {
                shown.Reason = result.HttpStatus == 410 ? UnrankedReason.Expired : UnrankedReason.Rejected;
                shown.Pending = null;
            }
            OnShow();
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
