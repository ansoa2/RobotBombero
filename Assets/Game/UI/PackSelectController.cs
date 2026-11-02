using System;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>Elegir con qué pack jugar. Un pack sin reglas no se puede jugar.</summary>
    public sealed class PackSelectController
    {
        private readonly GameContext _context;
        private readonly Action<ScreenId> _go;
        private readonly VisualElement _list;
        private readonly VisualElement _empty;

        public PackSelectController(VisualElement view, GameContext context, Action<ScreenId> go)
        {
            _context = context;
            _go = go;
            _list = UiKit.Require<VisualElement>(view, "select-list");
            _empty = UiKit.Require<VisualElement>(view, "select-empty");
            UiKit.Require<Button>(view, "select-back").clicked += () => _go(ScreenId.Menu);
            UiKit.Require<Button>(view, "select-rules").clicked += () => _go(ScreenId.Rules);
        }

        public void OnShow()
        {
            _list.Clear();
            var packs = _context.Packs.All;
            UiKit.Show(_empty, packs.Count == 0);
            for (int i = 0; i < packs.Count; i++) _list.Add(Card(packs[i], i));
        }

        private VisualElement Card(LocalPack pack, int i)
        {
            bool playable = _context.Packs.CanPlay(pack);
            VisualElement card = UiKit.Box("card", "card--row");
            VisualElement text = UiKit.Box("card__text");
            text.Add(UiKit.Label(null, pack.content.name, "card__title"));
            text.Add(UiKit.Label("select-note-" + i, Note(pack, playable), "text-muted"));
            card.Add(text);
            Button play = UiKit.Button("select-play-" + i, "Jugar", () =>
            {
                if (!playable) return;
                _context.PlayPack = pack;
                _go(ScreenId.Game);
            }, "btn--main", "btn--small");
            play.SetEnabled(playable);
            card.Add(play);
            return card;
        }

        private static string Note(LocalPack pack, bool playable)
        {
            int rules = pack.content.rules == null ? 0 : pack.content.rules.Length;
            if (!playable) return rules == 0 ? "Sin reglas: añade al menos una para jugar." : "El pack tiene errores y no se puede jugar.";
            string count = rules == 1 ? "1 regla" : rules + " reglas";
            long best = pack.stats == null ? 0 : pack.stats.bestPoints;
            return count + (best > 0 ? " · mejor partida " + Labels.Number(best) + " puntos" : " · aún sin puntos");
        }
    }
}
