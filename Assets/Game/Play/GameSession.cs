using System;
using System.Collections;
using System.IO;
using RobotBombero.Simulation;
using UnityEngine;

namespace RobotBombero.Game
{
    public enum SessionMode
    {
        Stepping,
        Auto
    }

    /// <summary>Por qué una partida no entra en la clasificación (el texto lo pone la interfaz).</summary>
    public enum UnrankedReason
    {
        /// <summary>Sí entra (o aún no se sabe).</summary>
        None,
        /// <summary>Usó los controles manuales de prueba.</summary>
        Manual,
        /// <summary>Sin identidad o sin conexión al empezar: se juega con una semilla local y no se envía nunca.</summary>
        Offline,
        /// <summary>El servidor limita las partidas por hora (429).</summary>
        RateLimited,
        /// <summary>La partida caducó antes de cerrarse (410).</summary>
        Expired,
        /// <summary>El servidor rechazó la partida por otro motivo.</summary>
        Rejected,
        /// <summary>Se jugó con el servidor, pero el cierre no llegó: queda pendiente y se reintenta.</summary>
        FinishPending
    }

    /// <summary>Lo que se muestra al terminar. Si FromServer, los números son los del servidor.</summary>
    public sealed class SessionResult
    {
        public int Steps;
        public int Fires;
        public long Points;
        public string EndReason;
        /// <summary>Posición, solo si el servidor la dio (esta partida es la fila del jugador en la clasificación).</summary>
        public long? Rank;
        /// <summary>Posición actual del jugador, aunque esta partida no sea su fila; nulo si no tiene fila.</summary>
        public long? PlayerRank;
        public bool FromServer;
        /// <summary>Falso si la partida no se registró en el servidor (sin conexión, sin clasificar o con acciones manuales).</summary>
        public bool Registered;
        /// <summary>Por qué no se registró; None si se registró.</summary>
        public UnrankedReason Reason;
        /// <summary>La partida del servidor cuyo cierre falta (solo con Reason FinishPending), para reintentarlo desde el resultado.</summary>
        public PendingFinish Pending;
    }

    /// <summary>
    /// Una partida: pide la semilla al servidor al primer Paso o Automático (o usa una local y juega sin clasificar),
    /// avanza la simulación a pasos o en automático y, al terminar, deja que el servidor decida el resultado.
    /// Abrir la pantalla no llama al servidor: hasta empezar solo hay una vista previa del tablero inicial.
    /// Es una clase simple: las corrutinas (Begin, Finish) las ejecuta un MonoBehaviour de la interfaz.
    /// </summary>
    public class GameSession
    {
        /// <summary>Pasos por segundo. El original (20) es el valor por defecto.</summary>
        public static readonly int[] Speeds = { 1, 2, 5, 10, 20, 50 };
        public const int DefaultSpeedIndex = 4;
        public const int MaxStepsPerFrame = 200;

        // El tablero inicial no depende de la semilla (el primer fuego es fijo), así que la vista previa vale para cualquiera.
        private const uint PreviewSeed = 1;

        private readonly LocalPack _pack;
        private readonly Profile _profile;
        private readonly string _key;
        private readonly IApiClient _api;
        private readonly LocalStore _store;
        private readonly Func<uint> _localSeed;
        private readonly Func<bool> _packStillExists;
        private readonly int _maxStepsPerFrame;

        private double _pending;
        private long _gameId;
        private bool _manual;
        private bool _finished;
        private bool _endedRaised;
        private bool _beginning;
        private UnrankedReason _startReason;
        private GameSimulation _preview;

        public GameSession(LocalPack pack, Profile profile, IApiClient api, LocalStore store, Func<uint> localSeed = null, int maxStepsPerFrame = MaxStepsPerFrame, Func<bool> packStillExists = null)
        {
            if (pack == null || pack.content == null || pack.content.rules == null) throw new ArgumentNullException("pack");
            if (maxStepsPerFrame <= 0) throw new ArgumentOutOfRangeException("maxStepsPerFrame");
            _pack = pack;
            _profile = profile ?? new Profile();
            _key = _profile.playerKey;
            _api = api;
            _store = store;
            _localSeed = localSeed ?? (() => (uint)Guid.NewGuid().GetHashCode());
            _packStillExists = packStillExists;
            _maxStepsPerFrame = maxStepsPerFrame;
            SpeedIndex = DefaultSpeedIndex;
        }

        public LocalPack Pack { get { return _pack; } }
        /// <summary>Nulo hasta el primer Paso o Automático (Begin).</summary>
        public GameSimulation Simulation { get; private set; }
        /// <summary>El tablero inicial para dibujarlo antes de empezar. No es la partida: no se avanza.</summary>
        public GameSimulation Preview
        {
            get
            {
                if (_preview == null) _preview = new GameSimulation(_pack.content.rules, PreviewSeed);
                return _preview;
            }
        }
        public bool IsStarted { get { return Simulation != null; } }
        public uint Seed { get; private set; }
        public SessionMode Mode { get; private set; }
        public int SpeedIndex { get; private set; }
        public int Speed { get { return Speeds[SpeedIndex]; } }
        /// <summary>Verdadero mientras el servidor conozca la partida y nadie haya usado los controles manuales.</summary>
        public bool IsRanked { get; private set; }
        /// <summary>Por qué la partida empezó sin clasificar (None si es clasificable o aún no ha empezado).</summary>
        public UnrankedReason StartReason { get { return IsStarted && !IsRanked ? _startReason : UnrankedReason.None; } }
        public bool IsEnded { get { return Simulation != null && Simulation.IsEnded; } }
        public SessionResult Result { get; private set; }
        /// <summary>El servidor rechazó la clave del jugador (401) en alguna llamada: la sesión ya no es válida.</summary>
        public bool Unauthorized { get; private set; }
        /// <summary>La clave con la que se hicieron las llamadas: un 401 solo cuenta si sigue siendo la del jugador.</summary>
        public string SentKey { get { return _key; } }

        /// <summary>La simulación acaba de terminar. El resultado definitivo llega por OnResult tras Finish.</summary>
        public event Action OnEnded;
        public event Action<SessionResult> OnResult;

        /// <summary>
        /// Sube el pack si hace falta, empieza la partida en el servidor y crea la simulación. Nunca falla: sin identidad
        /// o sin servidor se juega con una semilla local sin clasificar. Se puede llamar varias veces: solo empieza una vez.
        /// </summary>
        public IEnumerator Begin()
        {
            while (_beginning) yield return null;
            if (Simulation != null) yield break;

            _beginning = true;
            uint seed = 0;
            bool haveSeed = false;
            try
            {
                if (_api != null && !string.IsNullOrEmpty(_key))
                {
                    for (int attempt = 0; attempt < 2 && !haveSeed; attempt++)
                    {
                        bool synced = false;
                        yield return SyncPack(ok => synced = ok);
                        if (!synced) break;

                        ApiResult<StartedGame> started = null;
                        yield return _api.StartGame(_key, _pack.serverPackId, _pack.content.rules, r => started = r);
                        NoteStatus(started);
                        if (started != null && started.IsOk && started.Value != null)
                        {
                            seed = started.Value.seed;
                            _gameId = started.Value.gameId;
                            haveSeed = true;
                        }
                        else if (attempt == 0 && started != null && started.Status == ApiStatus.Rejected && (started.HttpStatus == 404 || started.HttpStatus == 403))
                        {
                            // El servidor no conoce ese pack (base nueva) o no es de este jugador: se vuelve a crear y se reintenta una vez.
                            ForgetServerPack();
                        }
                        else
                        {
                            _startReason = ReasonOf(started);
                            break;
                        }
                    }
                }
                else
                {
                    _startReason = UnrankedReason.Offline;
                }
            }
            finally
            {
                _beginning = false;
            }

            if (!haveSeed && _startReason == UnrankedReason.None) _startReason = UnrankedReason.Offline;
            IsRanked = haveSeed;
            if (!haveSeed) seed = _localSeed();
            Seed = seed;
            Simulation = new GameSimulation(_pack.content.rules, seed);
        }

        /// <summary>Empieza ya, sin servidor: para los controles manuales, que de todos modos no clasifican.</summary>
        public void BeginLocal()
        {
            if (Simulation != null || _beginning) return;
            _startReason = UnrankedReason.Offline;
            IsRanked = false;
            Seed = _localSeed();
            Simulation = new GameSimulation(_pack.content.rules, Seed);
        }

        private static UnrankedReason ReasonOf<T>(ApiResult<T> result)
        {
            if (result == null || result.Status == ApiStatus.ConnectionFailed) return UnrankedReason.Offline;
            if (result.Status == ApiStatus.Rejected)
            {
                if (result.HttpStatus == 429) return UnrankedReason.RateLimited;
                if (result.HttpStatus == 410) return UnrankedReason.Expired;
            }
            return UnrankedReason.Rejected;
        }

        private IEnumerator SyncPack(Action<bool> done)
        {
            string hash = RulesHash.Compute(_pack.content.rules);
            string name = _pack.content.name;
            if (_pack.serverPackId > 0 && _pack.serverRulesHash == hash && _pack.serverName == name)
            {
                done(true);
                yield break;
            }

            ApiResult<long> saved = null;
            yield return _api.SavePack(_key, _pack.serverPackId, name, hash, r => saved = r);
            NoteStatus(saved);

            // El pack ya no existe en el servidor (404, borrado desde otro sitio) o no es de este jugador (403): se vuelve a crear.
            if (saved != null && saved.Status == ApiStatus.Rejected && (saved.HttpStatus == 404 || saved.HttpStatus == 403) && _pack.serverPackId > 0)
            {
                _pack.serverPackId = 0;
                yield return _api.SavePack(_key, 0, name, hash, r => saved = r);
                NoteStatus(saved);
            }

            if (saved == null || !saved.IsOk)
            {
                _startReason = ReasonOf(saved);
                done(false);
                yield break;
            }

            _pack.serverPackId = saved.Value;
            _pack.serverRulesHash = hash;
            _pack.serverName = name;
            Persist();
            done(true);
        }

        private void ForgetServerPack()
        {
            _pack.serverPackId = 0;
            _pack.serverRulesHash = null;
            _pack.serverName = null;
            Persist();
        }

        public void StepOnce()
        {
            if (Simulation == null || Simulation.IsEnded) return;
            Simulation.Step();
            AfterSteps();
        }

        public void SetAuto(bool auto)
        {
            if (auto && (Simulation == null || Simulation.IsEnded)) return;
            Mode = auto ? SessionMode.Auto : SessionMode.Stepping;
            _pending = 0;
        }

        public void SetSpeed(int index)
        {
            if (index < 0 || index >= Speeds.Length) throw new ArgumentOutOfRangeException("index");
            SpeedIndex = index;
        }

        /// <summary>Acumula tiempo y ejecuta los pasos pendientes, con tope por fotograma; el resto queda para el siguiente.</summary>
        public void Tick(float deltaSeconds)
        {
            if (Mode != SessionMode.Auto || Simulation == null || Simulation.IsEnded) return;
            if (deltaSeconds > 0) _pending += deltaSeconds * Speed;

            int steps = (int)Math.Min(_pending, _maxStepsPerFrame);
            _pending -= steps;
            for (int i = 0; i < steps && !Simulation.IsEnded; i++) Simulation.Step();
            AfterSteps();
        }

        /// <summary>Acción de los controles manuales de prueba. La partida deja de poder clasificarse y de contar en las estadísticas.</summary>
        public void ApplyManual(string action)
        {
            if (Simulation == null || Simulation.IsEnded) return;
            _manual = true;
            IsRanked = false;
            Simulation.ApplyManual(action);
            AfterSteps();
        }

        /// <summary>
        /// Cierra la partida terminada: pide el resultado al servidor si estaba clasificada; puntos y posición
        /// mostrados son los del servidor. Antes de llamar anota el cierre como pendiente: si no llega, se reintenta.
        /// No toca las estadísticas (ya se anotaron al terminar). Tras completarse no repite nada.
        /// </summary>
        public IEnumerator Finish()
        {
            if (_finished || !IsEnded) yield break;

            var result = new SessionResult
            {
                Steps = Simulation.Steps,
                Fires = Simulation.Fires,
                Points = Score.Points(Simulation.Fires, Simulation.Steps),
                EndReason = Simulation.EndReason.ToString()
            };

            if (_manual)
            {
                result.Reason = UnrankedReason.Manual;
                _finished = true;
                Result = result;
                if (OnResult != null) OnResult(result);
                yield break;
            }

            if (IsRanked)
            {
                var pending = new PendingFinish { serverPackId = _pack.serverPackId, gameId = _gameId };
                AddPending(pending);

                ApiResult<GameResult> finished = null;
                yield return _api.FinishGame(_key, _gameId, r => finished = r);
                NoteStatus(finished);
                if (finished != null && finished.IsOk && finished.Value != null)
                {
                    RemovePending(pending);
                    GameResult server = finished.Value;
                    if (server.Steps != result.Steps || server.Fires != result.Fires)
                        Debug.LogWarning("RobotBombero: el resultado del servidor difiere del local (servidor " + server.Steps + " pasos, " + server.Fires + " fuegos; local " + result.Steps + ", " + result.Fires + ").");
                    result = new SessionResult
                    {
                        Steps = server.Steps,
                        Fires = server.Fires,
                        Points = server.Points,
                        EndReason = server.EndReason,
                        Rank = server.Rank,
                        PlayerRank = server.PlayerRank,
                        FromServer = true,
                        Registered = true
                    };
                }
                else if (PackManager.IsRetryable(finished))
                {
                    // Sin conexión o error del servidor: el cierre queda anotado y se reintenta al volver a conectar.
                    result.Reason = UnrankedReason.FinishPending;
                    result.Pending = pending;
                }
                else
                {
                    // Respuesta definitiva (410, 404, 403...): no hay nada que reintentar.
                    RemovePending(pending);
                    result.Reason = ReasonOf(finished);
                }
            }
            else
            {
                result.Reason = _startReason == UnrankedReason.None ? UnrankedReason.Offline : _startReason;
            }

            _finished = true;
            Result = result;
            if (OnResult != null) OnResult(result);
        }

        private void AfterSteps()
        {
            if (!Simulation.IsEnded) return;
            Mode = SessionMode.Stepping;
            _pending = 0;
            if (_endedRaised) return;
            _endedRaised = true;
            // Las estadísticas se anotan ya, con el resultado local y antes de cualquier llamada de red: si la
            // interfaz se destruye esperando al servidor, la partida ya cuenta. Las manuales no cuentan.
            // Un fallo al guardar no impide enviar la partida: Persist lo registra y sigue.
            if (!_manual)
            {
                if (_pack.stats == null) _pack.stats = new PackStats();
                _pack.stats.Add(Simulation.Steps, Simulation.Fires);
                Persist();
            }
            if (OnEnded != null) OnEnded();
        }

        private void NoteStatus<T>(ApiResult<T> result)
        {
            if (result != null && result.Status == ApiStatus.Rejected && result.HttpStatus == 401) Unauthorized = true;
        }

        /// <summary>Guarda el pack, salvo que ya se haya borrado (guardarlo lo devolvería a la lista) o que falle la escritura.</summary>
        private void Persist()
        {
            if (_store == null) return;
            if (_packStillExists != null && !_packStillExists()) return;
            try { _store.SavePack(_pack); }
            catch (IOException) { Debug.LogWarning("RobotBombero: no se pudo guardar el pack en este dispositivo."); }
            catch (UnauthorizedAccessException) { Debug.LogWarning("RobotBombero: no se pudo guardar el pack en este dispositivo."); }
        }

        private void AddPending(PendingFinish pending)
        {
            if (_store == null) return;
            try { _store.AddPendingFinish(pending); }
            catch (IOException) { Debug.LogWarning("RobotBombero: no se pudo anotar el cierre de la partida."); }
            catch (UnauthorizedAccessException) { Debug.LogWarning("RobotBombero: no se pudo anotar el cierre de la partida."); }
        }

        private void RemovePending(PendingFinish pending)
        {
            if (_store == null) return;
            try { _store.RemovePendingFinish(pending.gameId); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
