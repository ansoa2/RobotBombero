using System;

namespace RobotBombero.Simulation
{
    public static class PackLimits
    {
        public const int MaxFileBytes = 65536;
        public const int MaxRules = 100;
        public const int MaxNameLength = 40;
        public const int CurrentFormatVersion = 1;
    }

    public sealed class ValidationResult
    {
        public bool IsValid { get; private set; }
        public string Error { get; private set; }

        private ValidationResult(bool isValid, string error)
        {
            IsValid = isValid;
            Error = error;
        }

        public static ValidationResult Ok()
        {
            return new ValidationResult(true, null);
        }

        public static ValidationResult Fail(string error)
        {
            return new ValidationResult(false, error);
        }
    }

    /// <summary>
    /// Valida un pack ya deserializado. La simulación confía en su entrada, así que esto protege
    /// tanto la importación de archivos como el servidor. El tamaño del archivo (PackLimits.MaxFileBytes)
    /// lo comprueba el código de importación, no este validador.
    /// </summary>
    public static class PackValidator
    {
        private static readonly string[] Battery = { "Anyone", "High", "Medium", "Low" };
        private static readonly string[] Temp = { "Anyone", "Up", "Down", "Equal" };
        private static readonly string[] Light = { "Anyone", "Normal", "Bright", "Dark" };
        private static readonly string[] Prox = { "Anyone", "Obstacle Yes", "Obstacle No" };
        private static readonly string[] Cell = { "Anyone", "Base", "Normal", "Black" };
        private static readonly string[] Actions = { "Fwd", "Put Out", "TurnL", "TurnR" };

        public static ValidationResult Validate(RulePackFile pack)
        {
            if (pack == null) return ValidationResult.Fail("Pack nulo.");
            if (pack.formatVersion != PackLimits.CurrentFormatVersion)
                return ValidationResult.Fail("Versión de formato no soportada: " + pack.formatVersion + ".");
            if (string.IsNullOrEmpty(pack.name)) return ValidationResult.Fail("El pack no tiene nombre.");
            if (pack.name.Length > PackLimits.MaxNameLength)
                return ValidationResult.Fail("El nombre del pack supera " + PackLimits.MaxNameLength + " caracteres.");
            if (pack.rules == null) return ValidationResult.Fail("El pack no tiene reglas.");
            if (pack.rules.Length == 0) return ValidationResult.Fail("El pack no contiene ninguna regla.");
            if (pack.rules.Length > PackLimits.MaxRules)
                return ValidationResult.Fail("El pack supera " + PackLimits.MaxRules + " reglas.");

            for (int i = 0; i < pack.rules.Length; i++)
            {
                string error = ValidateRule(pack.rules[i]);
                if (error != null) return ValidationResult.Fail("Regla " + (i + 1) + ": " + error);
            }
            return ValidationResult.Ok();
        }

        private static string ValidateRule(Rule rule)
        {
            if (rule == null) return "regla nula.";
            if (!Contains(Battery, rule.battery)) return "valor de batería no válido.";
            if (!Contains(Temp, rule.temp)) return "valor de temperatura no válido.";
            if (!Contains(Light, rule.light)) return "valor de luz no válido.";
            if (!Contains(Prox, rule.prox)) return "valor de proximidad no válido.";
            if (!Contains(Cell, rule.cell)) return "valor de celda no válido.";
            if (rule.actions == null || rule.actions.Length == 0) return "la regla no tiene acciones.";
            if (rule.actions.Length > SimConfig.MaxActionsPerRule)
                return "más de " + SimConfig.MaxActionsPerRule + " acciones.";
            for (int i = 0; i < rule.actions.Length; i++)
            {
                if (!Contains(Actions, rule.actions[i])) return "acción no válida.";
            }
            return null;
        }

        private static bool Contains(string[] allowed, string value)
        {
            return value != null && Array.IndexOf(allowed, value) >= 0;
        }
    }
}
