using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        private List<long> _pending;
        private bool _syncing;
        private bool _again;
        private bool _offline;

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
            if (PackValidator.HasControlCharacters(name.Trim())) return "El nombre no puede llevar saltos de línea ni otros caracteres de control.";
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
                localId = LocalPack.NewId(),
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

        /// <summary>
        /// Quita el pack del dispositivo. Si el servidor lo conoce, su id queda anotado (en disco) hasta que
        /// SyncPending consiga borrarlo allí: una desconexión no deja la fila para siempre.
        /// </summary>
        public void Delete(LocalPack pack)
        {
            List.Remove(pack);
            _context.Store.DeletePack(pack.localId);
            // Sus partidas ya no existirán en el servidor: no hay cierre que reintentar.
            if (pack.serverPackId > 0)
            {
                List<PendingFinish> finishes = _context.Store.LoadPendingFinishes();
                if (finishes.RemoveAll(f => f.serverPackId == pack.serverPackId) > 0) _context.Store.SavePendingFinishes(finishes);
            }
            if (pack.serverPackId > 0 && !Pending.Contains(pack.serverPackId))
            {
                Pending.Add(pack.serverPackId);
                _context.Store.SavePendingDeletes(Pending);
            }
        }

        /// <summary>Borrados que aún no han llegado al servidor.</summary>
        public IReadOnlyList<long> PendingDeletes { get { return Pending; } }

        private List<long> Pending
        {
            get
            {
                if (_pending == null) _pending = _context.Store.LoadPendingDeletes();
                return _pending;
            }
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

        /// <summary>
        /// Pone el servidor al día: primero los borrados pendientes y luego cada pack con reglas o nombre cambiados.
        /// Lo que falla sigue pendiente, en silencio, hasta la próxima vez (al arrancar, al entrar, en Reglas...).
        /// Solo hay una pasada a la vez; una petición durante otra pasada la repite al terminar.
        /// </summary>
        public IEnumerator SyncPending()
        {
            if (!_context.HasIdentity) yield break;
            if (_syncing) { _again = true; yield break; }

            _syncing = true;
            try
            {
                do
                {
                    _again = false;
                    _offline = false;
                    yield return RunDeletes();
                    if (!_context.HasIdentity) yield break;
                    if (!_offline) yield return RunFinishes();
                    if (!_context.HasIdentity) yield break;

                    foreach (LocalPack pack in new List<LocalPack>(List))
                    {
                        if (_offline || !_context.HasIdentity) break;
                        if (NeedsSync(pack)) yield return Sync(pack);
                    }
                } while (_again && _context.HasIdentity);
            }
            finally
            {
                _syncing = false;
            }
        }

        private IEnumerator RunDeletes()
        {
            foreach (long id in new List<long>(Pending))
            {
                ApiResult<bool> deleted = null;
                string sentKey = _context.Profile.playerKey;
                yield return _context.Api.DeletePack(sentKey, id, r => deleted = r);
                if (_context.ExpireIfUnauthorized(deleted, sentKey)) yield break;

                bool gone = deleted != null && (deleted.IsOk || (deleted.Status == ApiStatus.Rejected && deleted.HttpStatus == 404));
                if (gone)
                {
                    Pending.Remove(id);
                    _context.Store.SavePendingDeletes(Pending);
                }
                else if (deleted == null || deleted.Status == ApiStatus.ConnectionFailed)
                {
                    _offline = true;
                    yield break;
                }
            }
        }

        /// <summary>Avisa al servidor de un cambio de reglas o de nombre de un pack. Si el pack se borra mientras tanto, no vuelve a guardarse.</summary>
        public IEnumerator Sync(LocalPack pack)
        {
            if (!NeedsSync(pack) || !_context.HasIdentity) yield break;

            string hash = RulesHash.Compute(pack.content.rules);
            string name = pack.content.name;
            ApiResult<long> saved = null;
            string sentKey = _context.Profile.playerKey;
            yield return _context.Api.SavePack(sentKey, pack.serverPackId, name, hash, r => saved = r);

            if (_context.ExpireIfUnauthorized(saved, sentKey)) yield break;
            if (saved == null || saved.Status == ApiStatus.ConnectionFailed) _offline = true;
            // Borrado durante la petición: guardar ahora devolvería el archivo.
            if (!List.Contains(pack)) yield break;

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

        /// <summary>Tras una sesión caducada: los packs siguen aquí, pero el servidor ya no los conoce ni hay nada que borrar allí.</summary>
        public void ForgetServerIds()
        {
            ForgetPackServerIds();
            Pending.Clear();
            try
            {
                _context.Store.SavePendingDeletes(Pending);
                _context.Store.SavePendingFinishes(new List<PendingFinish>());
            }
            catch (IOException) { Warn(); }
            catch (UnauthorizedAccessException) { Warn(); }
        }

        /// <summary>Solo los ids de servidor de los packs (para una identidad nueva): los borrados y cierres pendientes no se tocan.</summary>
        public void ForgetPackServerIds()
        {
            foreach (LocalPack pack in List)
            {
                pack.serverPackId = 0;
                pack.serverRulesHash = null;
                pack.serverName = null;
                // Un fallo al escribir un pack no debe impedir olvidar el resto ni avisar a la interfaz.
                try { Save(pack); } catch (IOException) { Warn(); } catch (UnauthorizedAccessException) { Warn(); }
            }
        }

        private static void Warn()
        {
            UnityEngine.Debug.LogWarning("RobotBombero: no se pudo guardar un cambio local al cerrar la sesión.");
        }

        // --- Cierres pendientes de partidas clasificadas ---

        /// <summary>Verdadero si el cierre puede reintentarse más tarde: sin conexión o error del servidor (5xx).</summary>
        public static bool IsRetryable<T>(ApiResult<T> result)
        {
            if (result == null || result.Status == ApiStatus.ConnectionFailed) return true;
            return result.Status == ApiStatus.Rejected && result.HttpStatus >= 500;
        }

        /// <summary>
        /// Intenta cerrar una partida pendiente. Con respuesta definitiva (éxito, 404, 410, 403...) la quita de la lista, sin
        /// tocar las estadísticas (ya contaron al terminar). Sin conexión o con un 5xx sigue pendiente. done recibe la respuesta.
        /// </summary>
        public IEnumerator RetryFinish(PendingFinish item, Action<ApiResult<GameResult>> done = null)
        {
            if (!_context.HasIdentity) { if (done != null) done(null); yield break; }
            string sentKey = _context.Profile.playerKey;
            ApiResult<GameResult> result = null;
            yield return _context.Api.FinishGame(sentKey, item.gameId, r => result = r);
            if (_context.ExpireIfUnauthorized(result, sentKey)) { if (done != null) done(result); yield break; }
            if (!IsRetryable(result))
            {
                try { _context.Store.RemovePendingFinish(item.gameId); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            if (done != null) done(result);
        }

        private IEnumerator RunFinishes()
        {
            foreach (PendingFinish item in _context.Store.LoadPendingFinishes())
            {
                ApiResult<GameResult> result = null;
                yield return RetryFinish(item, r => result = r);
                if (!_context.HasIdentity) yield break;
                if (IsRetryable(result)) { _offline = true; yield break; }
            }
        }
    }
}
