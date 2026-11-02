using System;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>Estadísticas locales por pack: partidas, mejor partida y medias.</summary>
    public sealed class StatsController
    {
        private readonly GameContext _context;
        private readonly VisualElement _list;
        private readonly VisualElement _empty;

        public StatsController(VisualElement view, GameContext context, Action<ScreenId> go)
        {
            _context = context;
            _list = UiKit.Require<VisualElement>(view, "stats-list");
            _empty = UiKit.Require<VisualElement>(view, "stats-empty");
            UiKit.Require<Button>(view, "stats-back").clicked += () => go(ScreenId.Menu);
        }

        public void OnShow()
        {
            _list.Clear();
            var packs = _context.Packs.All;
            UiKit.Show(_empty, packs.Count == 0);
            foreach (LocalPack pack in packs) _list.Add(Card(pack));
        }

        private static VisualElement Card(LocalPack pack)
        {
            PackStats s = pack.stats ?? new PackStats();
            VisualElement card = UiKit.Box("card");
            card.Add(UiKit.Label(null, pack.content.name, "card__title"));
            if (s.games == 0)
            {
                card.Add(UiKit.Label(null, "Sin partidas todavía.", "text-muted"));
                return card;
            }

            card.Add(Line("Partidas", Labels.Number(s.games)));
            string best = s.bestPoints > 0
                ? Labels.Number(s.bestPoints) + " puntos (" + Labels.Number(s.bestSteps) + " pasos, " + Labels.Number(s.bestFires) + " fuegos)"
                : "Aún sin puntos";
            card.Add(Line("Mejor partida", best));
            card.Add(Line("Media de pasos", Labels.Average(s.AverageSteps)));
            card.Add(Line("Media de fuegos", Labels.Average(s.AverageFires)));
            return card;
        }

        private static VisualElement Line(string name, string value)
        {
            VisualElement row = UiKit.Box("stat-line");
            row.Add(UiKit.Label(null, name, "text-muted", "stat-line__name"));
            row.Add(UiKit.Label(null, value, "stat-line__value"));
            return row;
        }
    }
}
