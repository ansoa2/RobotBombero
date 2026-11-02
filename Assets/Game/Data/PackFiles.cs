using System;
using System.Collections.Generic;
using System.Text;
using RobotBombero.Simulation;
using UnityEngine;

namespace RobotBombero.Game
{
    public sealed class ImportResult
    {
        public bool Success { get; private set; }
        public string Error { get; private set; }
        /// <summary>Pack nuevo, sin id de servidor y sin estadísticas. Nulo si Success es falso.</summary>
        public LocalPack Pack { get; private set; }

        public static ImportResult Ok(LocalPack pack)
        {
            return new ImportResult { Success = true, Pack = pack };
        }

        public static ImportResult Fail(string error)
        {
            return new ImportResult { Success = false, Error = error };
        }
    }

    // Formato del juego antiguo: el orden de la lista es el orden de prioridad. Los nombres de campo son los del archivo.
    [Serializable]
    internal class OldRule
    {
        public int id_rule;
        public string name;
        public string battery_state;
        public string sensor_temp;
        public string sensor_light;
        public string sensor_prox;
        public string sensor_cell;
        public string[] action;
    }

    [Serializable]
    internal class OldPackFile
    {
        public OldRule[] rulemodel_list;
    }

    /// <summary>Intercambio de packs como archivos JSON: solo el contenido del pack, ningún dato del jugador.</summary>
    public static class PackFiles
    {
        public const string OldPackName = "Reglas importadas";

        private static readonly string[] Reserved =
        {
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public static string Export(LocalPack pack)
        {
            if (pack == null || pack.content == null) throw new ArgumentNullException("pack");
            var clean = new RulePackFile
            {
                formatVersion = pack.content.formatVersion,
                name = pack.content.name,
                rules = pack.content.rules
            };
            return JsonUtility.ToJson(clean);
        }

        /// <summary>
        /// Nombre de archivo "nombre.json" válido en cualquier sistema de archivos: solo letras, dígitos, espacio, guion y guion bajo
        /// (el resto pasa a guion bajo). Sin letras ni dígitos se usa "pack"; los nombres reservados de Windows llevan un guion bajo delante.
        /// </summary>
        public static string ExportFileName(string packName)
        {
            var chars = (packName ?? "").ToCharArray();
            bool anyLetter = false;
            for (int i = 0; i < chars.Length; i++)
            {
                if (char.IsLetterOrDigit(chars[i])) { anyLetter = true; continue; }
                if (chars[i] != '-' && chars[i] != '_' && chars[i] != ' ') chars[i] = '_';
            }
            string stem = anyLetter ? new string(chars).Trim() : "pack";
            if (stem.Length > 100) stem = stem.Substring(0, 100).TrimEnd();
            if (Array.IndexOf(Reserved, stem.ToUpperInvariant()) >= 0) stem = "_" + stem;
            return stem + ".json";
        }

        public static ImportResult Import(string json, IEnumerable<string> existingNames)
        {
            if (string.IsNullOrWhiteSpace(json)) return ImportResult.Fail("El archivo está vacío.");
            if (Encoding.UTF8.GetByteCount(json) > PackLimits.MaxFileBytes)
                return ImportResult.Fail("El archivo supera " + PackLimits.MaxFileBytes + " bytes.");

            json = json.TrimStart((char)0xFEFF);

            RulePackFile parsed = TryParse<RulePackFile>(json);
            if (parsed == null) return ImportResult.Fail("El archivo no es un pack válido.");

            // El formato se decide por el contenido, nunca por la extensión del archivo.
            bool isNew = parsed.formatVersion != 0 || !string.IsNullOrEmpty(parsed.name) || (parsed.rules != null && parsed.rules.Length > 0);
            if (!isNew)
            {
                OldPackFile old = TryParse<OldPackFile>(json);
                if (old != null && old.rulemodel_list != null && old.rulemodel_list.Length > 0) parsed = FromOld(old);
                else if (json.Contains("\"rulemodel_list\"")) return ImportResult.Fail("El pack no contiene ninguna regla.");
            }

            // JsonUtility no falla siempre con basura: la validación decide.
            ValidationResult validation = PackValidator.Validate(parsed);
            if (!validation.IsValid) return ImportResult.Fail(validation.Error);

            parsed.name = UniqueName(parsed.name, existingNames);
            return ImportResult.Ok(new LocalPack
            {
                localId = LocalPack.NewId(),
                serverPackId = 0,
                content = parsed,
                stats = new PackStats()
            });
        }

        private static T TryParse<T>(string json) where T : class
        {
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static RulePackFile FromOld(OldPackFile old)
        {
            var rules = new Rule[old.rulemodel_list.Length];
            for (int i = 0; i < rules.Length; i++)
            {
                OldRule r = old.rulemodel_list[i];
                rules[i] = new Rule
                {
                    name = r.name,
                    battery = r.battery_state,
                    temp = r.sensor_temp,
                    light = r.sensor_light,
                    prox = r.sensor_prox,
                    cell = r.sensor_cell,
                    actions = r.action
                };
            }
            return new RulePackFile { formatVersion = PackLimits.CurrentFormatVersion, name = OldPackName, rules = rules };
        }

        private static string UniqueName(string name, IEnumerable<string> existing)
        {
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (existing != null)
            {
                foreach (string e in existing)
                {
                    if (e != null) taken.Add(e);
                }
            }
            if (!taken.Contains(name)) return name;

            for (int n = 2; ; n++)
            {
                string suffix = " (" + n + ")";
                int keep = Math.Min(name.Length, PackLimits.MaxNameLength - suffix.Length);
                string candidate = name.Substring(0, keep) + suffix;
                if (!taken.Contains(candidate)) return candidate;
            }
        }
    }
}
