using System;
using System.Collections.Generic;
using RobotBombero.Simulation;
using UnityEngine.UIElements;

namespace RobotBombero.Game
{
    /// <summary>
    /// Editor de las reglas de un pack. Dos vistas: la lista (la regla de arriba gana) y el detalle de una regla,
    /// con sus cinco condiciones y hasta cinco acciones. Cada cambio se guarda al momento; el servidor se avisa al salir.
    /// </summary>
    public sealed class RuleEditorController
    {
        private readonly GameContext _context;
        private readonly Action<ScreenId> _go;
        private readonly VisualElement _view;
        private readonly Label _title;
        private readonly VisualElement _listView;
        private readonly VisualElement _rules;
        private readonly VisualElement _detailView;
        private readonly Label _detailTitle;
        private readonly VisualElement _conditions;
        private readonly VisualElement _actions;
        private readonly Button _addRule;
        private readonly Dictionary<string, List<Button>> _chips = new Dictionary<string, List<Button>>();

        private LocalPack _pack;
        private int _editing = -1;

        /// <summary>Por debajo de este ancho de lista, los botones de cada regla van debajo de su resumen.</summary>
        public const float NarrowWidth = 480f;
        public const string NarrowClass = "rules-narrow";

        public RuleEditorController(VisualElement view, GameContext context, Action<ScreenId> go)
        {
            _context = context;
            _go = go;
            _view = view;
            _title = UiKit.Plain(UiKit.Require<Label>(view, "editor-title"));
            _listView = UiKit.Require<VisualElement>(view, "editor-list-view");
            _rules = UiKit.Require<VisualElement>(view, "editor-rules");
            _detailView = UiKit.Require<VisualElement>(view, "editor-detail");
            _detailTitle = UiKit.Require<Label>(view, "detail-title");
            _conditions = UiKit.Require<VisualElement>(view, "detail-conditions");
            _actions = UiKit.Require<VisualElement>(view, "detail-actions");
            _addRule = UiKit.Require<Button>(view, "editor-add-rule");

            UiKit.Require<Button>(view, "editor-back").clicked += Back;
            UiKit.Require<Button>(view, "editor-detail-done").clicked += ShowList;
            _addRule.clicked += AddRule;
            // USS no recoloca la fila aunque tenga flex-wrap: sin esto, en un móvil el resumen se queda en una columna de una palabra.
            _rules.RegisterCallback<GeometryChangedEvent>(e => ApplyWidth(view, e.newRect.width));
        }

        public static void ApplyWidth(VisualElement view, float width)
        {
            view.EnableInClassList(NarrowClass, width < NarrowWidth);
        }

        public void OnShow()
        {
            _pack = _context.EditingPack;
            _title.text = _pack == null ? "Editor de reglas" : _pack.content.name;
            _addRule.SetEnabled(_pack != null);
            ShowList();
        }

        public void Back()
        {
            if (_editing >= 0) { ShowList(); return; }
            _go(ScreenId.Rules);
        }

        private void Changed()
        {
            _context.Packs.Save(_pack);
        }

        // --- Lista ---

        private void ShowList()
        {
            _editing = -1;
            UiKit.Show(_listView, true);
            UiKit.Show(_detailView, false);
            BuildList();
        }

        private void BuildList()
        {
            _rules.Clear();
            if (_pack == null) return;
            Rule[] rules = _pack.content.rules;
            for (int i = 0; i < rules.Length; i++) _rules.Add(RuleCard(rules, i));
            _addRule.SetEnabled(rules.Length < PackLimits.MaxRules);
        }

        private VisualElement RuleCard(Rule[] rules, int i)
        {
            VisualElement card = UiKit.Box("card", "card--rule");
            VisualElement head = UiKit.Box("rule__head");
            head.Add(UiKit.Label(null, (i + 1).ToString(), "rule__number"));
            head.Add(UiKit.Label("rule-summary-" + i, RuleEdits.Summary(rules[i]), "rule__summary"));
            card.Add(head);

            VisualElement buttons = UiKit.Box("card__buttons");
            Button up = UiKit.Button("rule-up-" + i, "↑", () => Move(i, -1), "btn--small", "btn--icon");
            up.SetEnabled(i > 0);
            Button down = UiKit.Button("rule-down-" + i, "↓", () => Move(i, 1), "btn--small", "btn--icon");
            down.SetEnabled(i < rules.Length - 1);
            Button remove = UiKit.Button("rule-delete-" + i, "Quitar", () => Remove(i), "btn--small", "btn--danger");
            remove.SetEnabled(rules.Length > 1);
            buttons.Add(up);
            buttons.Add(down);
            buttons.Add(UiKit.Button("rule-edit-" + i, "Editar", () => ShowDetail(i), "btn--small", "btn--main"));
            buttons.Add(remove);
            card.Add(buttons);
            return card;
        }

        private void Move(int i, int delta)
        {
            if (!RuleEdits.MoveRule(_pack.content, i, delta)) return;
            Changed();
            BuildList();
        }

        private void Remove(int i)
        {
            if (!RuleEdits.RemoveRule(_pack.content, i)) return;
            Changed();
            BuildList();
        }

        private void AddRule()
        {
            if (!RuleEdits.AddRule(_pack.content)) return;
            Changed();
            BuildList();
        }

        // --- Detalle de una regla ---

        private void ShowDetail(int i)
        {
            _editing = i;
            UiKit.Show(_listView, false);
            UiKit.Show(_detailView, true);
            _detailTitle.text = "Regla " + (i + 1);

            Rule rule = _pack.content.rules[i];
            _conditions.Clear();
            _chips.Clear();
            foreach (string key in Labels.ConditionKeys)
            {
                string field = key;
                VisualElement block = UiKit.Box("field-block");
                VisualElement head = UiKit.Box("field-block__head");
                head.Add(UiKit.Label(null, Labels.Field(key), "field-block__title"));
                head.Add(InfoButton(key));
                block.Add(head);
                VisualElement row = UiKit.Box("chips");
                var buttons = new List<Button>();
                foreach (string value in SensorOptions.Offered(key))
                {
                    string chosen = value;
                    Button chip = UiKit.Button("cond-" + key + "-" + Labels.Slug(value), Labels.Value(value), () => SetCondition(field, chosen), "chip");
                    chip.userData = value;
                    buttons.Add(chip);
                    row.Add(chip);
                }
                // Un valor oculto (p. ej. Equal de un pack antiguo) sale como chip extra seleccionado hasta que se cambie.
                string current = RuleEdits.GetCondition(rule, key);
                if (current != null && !SensorOptions.IsOffered(key, current))
                {
                    Button extra = UiKit.Button("cond-" + key + "-" + Labels.Slug(current), Labels.Value(current) + " (nunca se cumple)", () => { }, "chip");
                    extra.userData = current;
                    buttons.Add(extra);
                    row.Add(extra);
                }
                _chips[key] = buttons;
                block.Add(row);
                _conditions.Add(block);
                MarkChips(key, current);
            }
            BuildActions();
        }

        /// <summary>Un círculo con una "i" dibujado con USS (sin glifos fuera de la fuente) y de al menos 44 px de lado.</summary>
        private Button InfoButton(string key)
        {
            Button button = UiKit.Button("sensor-info-" + key, "", () => ShowSensorInfo(key), "info-btn");
            button.RemoveFromClassList("btn");
            VisualElement dot = UiKit.Box("info-btn__dot");
            dot.pickingMode = PickingMode.Ignore;
            Label letter = UiKit.Label(null, "i", "info-btn__letter");
            letter.pickingMode = PickingMode.Ignore;
            dot.Add(letter);
            button.Add(dot);
            return button;
        }

        private void ShowSensorInfo(string key)
        {
            SensorInfo info = SensorInfo.For(key);
            VisualElement content = UiKit.Box("sensor-info");
            content.Add(UiKit.Label(null, info.Measures, "sensor-info__measures"));
            foreach (SensorOption option in info.Options)
            {
                VisualElement row = UiKit.Box("sensor-info__option");
                row.Add(UiKit.Label(null, option.Label, "sensor-info__value"));
                row.Add(UiKit.Label(null, option.Meaning, "sensor-info__meaning"));
                content.Add(row);
            }
            Dialog.Info(_view, info.Title, content, "Cerrar");
        }

        private void SetCondition(string key, string value)
        {
            RuleEdits.SetCondition(_pack.content.rules[_editing], key, value);
            Changed();
            // El chip de un valor oculto desaparece al cambiarlo: no se puede volver a elegir.
            _chips[key].RemoveAll(c =>
            {
                if (SensorOptions.IsOffered(key, (string)c.userData)) return false;
                c.RemoveFromHierarchy();
                return true;
            });
            MarkChips(key, value);
        }

        private void MarkChips(string key, string value)
        {
            foreach (Button chip in _chips[key]) chip.EnableInClassList("is-selected", (string)chip.userData == value);
        }

        private void BuildActions()
        {
            Rule rule = _pack.content.rules[_editing];
            _actions.Clear();
            string[] list = rule.actions;
            _actions.Add(UiKit.Label(null, "Acciones, en orden (" + list.Length + " de " + SimConfig.MaxActionsPerRule + ")", "field-block__title"));
            for (int j = 0; j < list.Length; j++)
            {
                int index = j;
                VisualElement row = UiKit.Box("action-row");
                row.Add(UiKit.Label(null, (j + 1) + ".", "action-row__number"));
                row.Add(UiKit.Label(null, Labels.Value(list[j]), "action-row__name"));
                Button up = UiKit.Button("action-up-" + j, "↑", () => MoveAction(index, -1), "btn--small", "btn--icon");
                up.SetEnabled(j > 0);
                Button down = UiKit.Button("action-down-" + j, "↓", () => MoveAction(index, 1), "btn--small", "btn--icon");
                down.SetEnabled(j < list.Length - 1);
                Button remove = UiKit.Button("action-remove-" + j, "×", () => RemoveAction(index), "btn--small", "btn--icon", "btn--danger");
                remove.SetEnabled(list.Length > 1);
                row.Add(up);
                row.Add(down);
                row.Add(remove);
                _actions.Add(row);
            }

            bool full = list.Length >= SimConfig.MaxActionsPerRule;
            _actions.Add(UiKit.Label("detail-add-title", full ? "Máximo de acciones alcanzado" : "Añadir acción", "field-block__title"));
            VisualElement chips = UiKit.Box("chips");
            foreach (string action in Labels.ActionValues)
            {
                string chosen = action;
                Button chip = UiKit.Button("action-add-" + Labels.Slug(action), Labels.Value(action), () => AddAction(chosen), "chip");
                chip.SetEnabled(!full);
                chips.Add(chip);
            }
            _actions.Add(chips);
        }

        private void AddAction(string action)
        {
            if (!RuleEdits.AddAction(_pack.content.rules[_editing], action)) return;
            Changed();
            BuildActions();
        }

        private void RemoveAction(int j)
        {
            if (!RuleEdits.RemoveAction(_pack.content.rules[_editing], j)) return;
            Changed();
            BuildActions();
        }

        private void MoveAction(int j, int delta)
        {
            if (!RuleEdits.MoveAction(_pack.content.rules[_editing], j, delta)) return;
            Changed();
            BuildActions();
        }
    }
}
