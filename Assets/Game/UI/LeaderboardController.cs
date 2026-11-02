using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>Clasificación global: lista desplazable, una fila por jugador, con la propia destacada.</summary>
    public sealed class LeaderboardController
    {
        public const float NarrowWidth = 520f;

        private const string MsgLoading = "Cargando la clasificación…";
        private const string MsgOffline = "No se puede cargar la clasificación: no hay conexión con el servidor.";
        private const string MsgServer = "El servidor no ha podido enviar la clasificación. Inténtalo de nuevo en un momento.";
        private const string MsgEmpty = "Todavía no hay partidas en la clasificación. ¡Sé el primero!";

        private readonly VisualElement _view;
        private readonly GameContext _context;
        private readonly VisualElement _list;
        private readonly Label _message;
        private readonly Button _retry;
        private int _generation;

        public LeaderboardController(VisualElement view, GameContext context, Action<ScreenId> go)
        {
            _view = view;
            _context = context;
            _list = UiKit.Require<VisualElement>(view, "lb-list");
            _message = UiKit.Require<Label>(view, "lb-message");
            _retry = UiKit.Require<Button>(view, "lb-retry");
            UiKit.Require<Button>(view, "lb-back").clicked += () => go(ScreenId.Menu);
            _retry.clicked += OnShow;
            // En estrecho, pasos y fuegos (y el pack) van bajo el nombre en vez de en columnas que se cortarían.
            _list.RegisterCallback<GeometryChangedEvent>(e => view.EnableInClassList("is-narrow", e.newRect.width < NarrowWidth));
        }

        public void OnShow()
        {
            _generation++;
            _list.Clear();
            ShowMessage(MsgLoading, false);
            _context.Run(Load(_generation));
        }

        private IEnumerator Load(int generation)
        {
            ApiResult<LeaderboardRow[]> result = null;
            yield return _context.Api.GetLeaderboard(r => result = r);
            if (generation != _generation) yield break;

            if (result == null || result.Status == ApiStatus.ConnectionFailed) { ShowMessage(MsgOffline, true); yield break; }
            if (!result.IsOk || result.Value == null) { ShowMessage(MsgServer, true); yield break; }

            List<LeaderboardRow> rows = OnePerPlayer(result.Value);
            if (rows.Count == 0) { ShowMessage(MsgEmpty, false); yield break; }

            UiKit.Show(_message, false);
            UiKit.Show(_retry, false);
            string me = _context.Profile == null ? null : _context.Profile.playerName;
            foreach (LeaderboardRow row in rows) _list.Add(Row(row, string.Equals(row.playerName, me, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>El servidor ya manda una sola fila por jugador; aquí se repite la regla por si acaso. Las filas llegan ordenadas.</summary>
        private static List<LeaderboardRow> OnePerPlayer(LeaderboardRow[] source)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rows = new List<LeaderboardRow>();
            foreach (LeaderboardRow row in source)
            {
                if (row != null && seen.Add(row.playerName ?? "")) rows.Add(row);
            }
            return rows;
        }

        private static VisualElement Row(LeaderboardRow r, bool own)
        {
            VisualElement row = UiKit.Box("lb-row");
            if (own) row.AddToClassList("lb-row--own");
            row.Add(UiKit.Label(null, r.rank.ToString(), "lb-rank"));

            VisualElement main = UiKit.Box("lb-main");
            main.Add(UiKit.Label(null, own ? r.playerName + " (tú)" : r.playerName, "lb-name"));
            main.Add(UiKit.Label(null, r.packName + " · " + Plural(r.steps, "paso", "pasos") + " · " + Plural(r.fires, "fuego", "fuegos"), "lb-sub"));
            row.Add(main);

            row.Add(UiKit.Label(null, r.packName, "lb-pack"));
            row.Add(UiKit.Label(null, Labels.Number(r.points), "lb-points"));
            row.Add(UiKit.Label(null, Labels.Number(r.steps), "lb-steps"));
            row.Add(UiKit.Label(null, Labels.Number(r.fires), "lb-fires"));
            return row;
        }

        private static string Plural(int n, string one, string many)
        {
            return n == 1 ? "1 " + one : Labels.Number(n) + " " + many;
        }

        private void ShowMessage(string text, bool canRetry)
        {
            _message.text = text;
            UiKit.Show(_message, true);
            UiKit.Show(_retry, canRetry);
        }
    }
}
