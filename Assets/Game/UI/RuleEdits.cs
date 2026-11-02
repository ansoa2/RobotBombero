using System;
using System.Collections.Generic;
using RobotBombero.Simulation;

namespace RobotBombero.Game
{
    /// <summary>Cambios sobre las reglas de un pack. Nunca dejan una regla sin acciones ni un pack sin reglas.</summary>
    public static class RuleEdits
    {
        public static Rule NewRule(int number)
        {
            return new Rule
            {
                name = "Regla " + number,
                battery = "Anyone",
                temp = "Anyone",
                light = "Anyone",
                prox = "Anyone",
                cell = "Anyone",
                actions = new[] { "Fwd" }
            };
        }

        public static bool AddRule(RulePackFile pack)
        {
            if (pack.rules.Length >= PackLimits.MaxRules) return false;
            Array.Resize(ref pack.rules, pack.rules.Length + 1);
            pack.rules[pack.rules.Length - 1] = NewRule(pack.rules.Length);
            return true;
        }

        public static bool RemoveRule(RulePackFile pack, int index)
        {
            if (pack.rules.Length <= 1 || index < 0 || index >= pack.rules.Length) return false;
            pack.rules = Without(pack.rules, index);
            return true;
        }

        /// <summary>Mueve una regla arriba (-1) o abajo (+1). La posición en la lista es la prioridad.</summary>
        public static bool MoveRule(RulePackFile pack, int index, int delta)
        {
            return Move(pack.rules, index, delta);
        }

        public static bool MoveAction(Rule rule, int index, int delta)
        {
            return Move(rule.actions, index, delta);
        }

        public static bool AddAction(Rule rule, string action)
        {
            if (rule.actions.Length >= SimConfig.MaxActionsPerRule) return false;
            Array.Resize(ref rule.actions, rule.actions.Length + 1);
            rule.actions[rule.actions.Length - 1] = action;
            return true;
        }

        public static bool RemoveAction(Rule rule, int index)
        {
            if (rule.actions.Length <= 1 || index < 0 || index >= rule.actions.Length) return false;
            rule.actions = Without(rule.actions, index);
            return true;
        }

        public static string GetCondition(Rule rule, string key)
        {
            switch (key)
            {
                case "battery": return rule.battery;
                case "temp": return rule.temp;
                case "light": return rule.light;
                case "prox": return rule.prox;
                default: return rule.cell;
            }
        }

        public static void SetCondition(Rule rule, string key, string value)
        {
            switch (key)
            {
                case "battery": rule.battery = value; break;
                case "temp": rule.temp = value; break;
                case "light": rule.light = value; break;
                case "prox": rule.prox = value; break;
                default: rule.cell = value; break;
            }
        }

        /// <summary>Resumen corto de lo que se cumple: solo las condiciones que importan.</summary>
        public static string Summary(Rule rule)
        {
            var parts = new List<string>();
            foreach (string key in Labels.ConditionKeys)
            {
                string value = GetCondition(rule, key);
                if (value != "Anyone") parts.Add(Labels.Field(key) + " " + Labels.Value(value));
            }
            string when = parts.Count == 0 ? "Siempre" : string.Join(" · ", parts.ToArray());

            var actions = new string[rule.actions.Length];
            for (int i = 0; i < actions.Length; i++) actions[i] = Labels.Value(rule.actions[i]);
            return when + "  →  " + string.Join(", ", actions);
        }

        private static bool Move<T>(T[] items, int index, int delta)
        {
            int target = index + delta;
            if (index < 0 || index >= items.Length || target < 0 || target >= items.Length) return false;
            T temp = items[index];
            items[index] = items[target];
            items[target] = temp;
            return true;
        }

        private static T[] Without<T>(T[] items, int index)
        {
            var result = new T[items.Length - 1];
            Array.Copy(items, 0, result, 0, index);
            Array.Copy(items, index + 1, result, index, items.Length - index - 1);
            return result;
        }
    }
}
