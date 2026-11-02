using System;
using System.Collections;
using System.Collections.Generic;
using RobotBombero.Simulation;

namespace RobotBombero.Game
{
    /// <summary>
    /// Los packs de este dispositivo: lista en memoria (la fuente de verdad mientras se juega), guardado local
    /// y puesta al día en el servidor. El servidor solo conoce nombre y huella de reglas.
    /// </summary>
    public sealed class PackManager
    {
        private readonly GameContext _context;
        private List<LocalPack> _packs;

        public PackManager(GameContext context)
        {
            _context = context;
        }

        public IReadOnlyList<LocalPack> All
        {
            get { return List; }
        }

        private List<LocalPack> List
        {
            get
            {
                if (_packs == null) _packs = _context.Store.LoadPacks();
                return _packs;
            }
        }

        public static string ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Escribe un nombre para el pack.";
            if (name.Trim().Length > PackLimits.MaxNameLength) return "El nombre admite como máximo " + PackLimits.MaxNameLength + " caracteres.";
            return null;
        }

        private bool NameTaken(string name, LocalPack except)
        {
            foreach (LocalPack pack in List)
            {
                if (pack != except && string.Equals(pack.content.name, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private IEnumerable<string> Names()
        {
            foreach (LocalPack pack in List) yield return pack.content.name;
        }

        /// <summary>Añade y guarda un pack ya construido.</summary>
        public void Add(LocalPack pack)
        {
            List.Add(pack);
            Save(pack);
        }

        public void Save(LocalPack pack)
        {
            _context.Store.SavePack(pack);
        }

        /// <summary>Crea un pack con una regla válida: uno sin reglas no se podría volver a cargar. Devuelve el error, o nulo.</summary>
        public string Create(string name, out LocalPack pack)
        {
            pack = null;
            string error = ValidateName(name);
            if (error != null) return error;
            name = name.Trim();
            if (NameTaken(name, null)) return "Ya existe un pack con ese nombre.";

            pack = new LocalPack
            {
                localId = Guid.NewGuid().ToString("N"),
                content = new RulePackFile
                {
                    formatVersion = PackLimits.CurrentFormatVersion,
                    name = name,
                    rules = new[] { RuleEdits.NewRule(1) }
                },
                stats = new PackStats()
            };
            Add(pack);
            return null;
        }

        public string Rename(LocalPack pack, string name)
        {
            string error = ValidateName(name);
            if (error != null) return error;
            name = name.Trim();
            if (NameTaken(name, pack)) return "Ya existe un pack con ese nombre.";
            pack.content.name = name;
            Save(pack);
            return null;
        }

        /// <summary>Quita el pack del dispositivo. El borrado en el servidor va aparte (DeleteRemote).</summary>
        public void Delete(LocalPack pack)
        {
            List.Remove(pack);
            _context.Store.DeletePack(pack.localId);
        }

        public ImportResult Import(string json)
        {
            ImportResult result = PackFiles.Import(json, Names());
            if (result.Success) Add(result.Pack);
            return result;
        }

        public bool CanPlay(LocalPack pack)
        {
            return pack != null && pack.content != null && PackValidator.Validate(pack.content).IsValid;
        }

        /// <summary>Verdadero si el pack ya está en el servidor pero este no conoce sus reglas o nombre actuales.</summary>
        public bool NeedsSync(LocalPack pack)
        {
            if (pack.serverPackId <= 0) return false;
            return pack.serverRulesHash != RulesHash.Compute(pack.content.rules) || pack.serverName != pack.content.name;
        }

        /// <summary>Avisa al servidor de un cambio de reglas o de nombre. Sin conexión no pasa nada: se repite al jugar.</summary>
        public IEnumerator Sync(LocalPack pack)
        {
            if (!NeedsSync(pack) || !_context.HasIdentity) yield break;

            string hash = RulesHash.Compute(pack.content.rules);
            string name = pack.content.name;
            ApiResult<long> saved = null;
            yield return _context.Api.SavePack(_context.Profile.playerKey, pack.serverPackId, name, hash, r => saved = r);

            if (_context.ExpireIfUnauthorized(saved)) yield break;
            if (saved != null && saved.Status == ApiStatus.Rejected && saved.HttpStatus == 404)
            {
                // Ya no existe en el servidor: se volverá a crear al jugar.
                pack.serverPackId = 0;
                pack.serverRulesHash = null;
                pack.serverName = null;
                Save(pack);
                yield break;
            }
            if (saved == null || !saved.IsOk) yield break;

            pack.serverRulesHash = hash;
            pack.serverName = name;
            Save(pack);
        }

        public IEnumerator DeleteRemote(long serverPackId)
        {
            if (serverPackId <= 0 || !_context.HasIdentity) yield break;
            ApiResult<bool> deleted = null;
            yield return _context.Api.DeletePack(_context.Profile.playerKey, serverPackId, r => deleted = r);
            _context.ExpireIfUnauthorized(deleted);
        }

        /// <summary>Tras una sesión caducada: los packs siguen aquí, pero el servidor ya no los conoce.</summary>
        public void ForgetServerIds()
        {
            foreach (LocalPack pack in List)
            {
                pack.serverPackId = 0;
                pack.serverRulesHash = null;
                pack.serverName = null;
                Save(pack);
            }
        }
    }
}
