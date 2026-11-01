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

    /// <summary>Intercambio de packs como archivos JSON: solo el contenido del pack, ningún dato del jugador.</summary>
    public static class PackFiles
    {
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

        public static ImportResult Import(string json, IEnumerable<string> existingNames)
        {
            if (string.IsNullOrWhiteSpace(json)) return ImportResult.Fail("El archivo está vacío.");
            if (Encoding.UTF8.GetByteCount(json) > PackLimits.MaxFileBytes)
                return ImportResult.Fail("El archivo supera " + PackLimits.MaxFileBytes + " bytes.");

            json = json.TrimStart('﻿');

            RulePackFile parsed;
            try
            {
                parsed = JsonUtility.FromJson<RulePackFile>(json);
            }
            catch (ArgumentException)
            {
                return ImportResult.Fail("El archivo no es un pack válido.");
            }

            // JsonUtility no falla siempre con basura: la validación decide.
            ValidationResult validation = PackValidator.Validate(parsed);
            if (!validation.IsValid) return ImportResult.Fail(validation.Error);

            parsed.name = UniqueName(parsed.name, existingNames);
            return ImportResult.Ok(new LocalPack
            {
                localId = Guid.NewGuid().ToString("N"),
                serverPackId = 0,
                content = parsed,
                stats = new PackStats()
            });
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
