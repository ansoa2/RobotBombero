using System;
using System.Collections;
using RobotBombero.Simulation;
using UnityEngine;

namespace RobotBombero.Game
{
    public enum SessionMode
    {
        Stepping,
        Auto
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
        public bool FromServer;
        /// <summary>Falso si la partida no se registró en el servidor (sin conexión, sin clasificar o con acciones manuales).</summary>
        public bool Registered;
    }

    /// <summary>
    /// Una partida: pide la semilla al servidor (o usa una local y juega sin clasificar), avanza la
    /// simulación a pasos o en automático y, al terminar, deja que el servidor decida el resultado.
    /// Es una clase simple: las corrutinas (Begin, Finish) las ejecuta un MonoBehaviour de la interfaz.
    /// </summary>
    public class GameSession
    {
        /// <summary>Pasos por segundo. El original (20) es el valor por defecto.</summary>
        public static readonly int[] Speeds = { 1, 2, 5, 10, 20, 50 };
        public const int DefaultSpeedIndex = 4;
        public const int MaxStepsPerFrame = 200;

        private readonly LocalPack _pack;
        private readonly Profile _profile;
        private readonly IApiClient _api;
        private readonly LocalStore _store;
        private readonly Func<uint> _localSeed;
        private readonly int _maxStepsPerFrame;

        private double _pending;
        private long _gameId;
        private bool _manual;
        private bool _finished;
        private bool _endedRaised;

        public GameSession(LocalPack pack, Profile profile, IApiClient api, LocalStore store, Func<uint> localSeed = null, int maxStepsPerFrame = MaxStepsPerFrame)
        {
            if (pack == null || pack.content == null || pack.content.rules == null) throw new ArgumentNullException("pack");
            if (maxStepsPerFrame <= 0) throw new ArgumentOutOfRangeException("maxStepsPerFrame");
            _pack = pack;
            _profile = profile ?? new Profile();
            _api = api;
            _store = store;
            _localSeed = localSeed ?? (() => (uint)Guid.NewGuid().GetHashCode());
            _maxStepsPerFrame = maxStepsPerFrame;
            SpeedIndex = DefaultSpeedIndex;
        }

        public LocalPack Pack { get { return _pack; } }
        public GameSimulation Simulation { get; private set; }
        public uint Seed { get; private set; }
        public SessionMode Mode { get; private set; }
        public int SpeedIndex { get; private set; }
        public int Speed { get { return Speeds[SpeedIndex]; } }
        /// <summary>Verdadero mientras el servidor conozca la partida y nadie haya usado los controles manuales.</summary>
        public bool IsRanked { get; private set; }
        public bool IsEnded { get { return Simulation != null && Simulation.IsEnded; } }
        public SessionResult Result { get; private set; }
        /// <summary>El servidor rechazó la clave del jugador (401) en alguna llamada: la sesión ya no es válida.</summary>
        public bool Unauthorized { get; private set; }

        /// <summary>La simulación acaba de terminar. El resultado definitivo llega por OnResult tras Finish.</summary>
        public event Action OnEnded;
        public event Action<SessionResult> OnResult;

        /// <summary>Sube el pack si hace falta, empieza la partida en el servidor y crea la simulación. Nunca falla: sin servidor se juega sin clasificar.</summary>
        public IEnumerator Begin()
        {
            uint seed = 0;
            bool haveSeed = false;

            if (_api != null && !string.IsNullOrEmpty(_profile.playerKey))
            {
                bool synced = false;
                yield return SyncPack(ok => synced = ok);

                if (synced)
                {
                    ApiResult<StartedGame> started = null;
                    yield return _api.StartGame(_profile.playerKey, _pack.serverPackId, _pack.content.rules, r => started = r);
                    NoteStatus(started);
                    if (started != null && started.IsOk && started.Value != null)
                    {
                        seed = started.Value.seed;
                        _gameId = started.Value.gameId;
                        haveSeed = true;
                    }
                }
            }

            IsRanked = haveSeed;
            if (!haveSeed) seed = _localSeed();
            Seed = seed;
            Simulation = new GameSimulation(_pack.content.rules, seed);
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
            yield return _api.SavePack(_profile.playerKey, _pack.serverPackId, name, hash, r => saved = r);
            NoteStatus(saved);

            // El pack ya no existe en el servidor (borrado desde otro sitio): se vuelve a crear.
            if (saved != null && saved.Status == ApiStatus.Rejected && saved.HttpStatus == 404 && _pack.serverPackId > 0)
            {
                _pack.serverPackId = 0;
                yield return _api.SavePack(_profile.playerKey, 0, name, hash, r => saved = r);
                NoteStatus(saved);
            }

            if (saved == null || !saved.IsOk)
            {
                done(false);
                yield break;
            }

            _pack.serverPackId = saved.Value;
            _pack.serverRulesHash = hash;
            _pack.serverName = name;
            Persist();
            done(true);
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
        /// mostrados son los del servidor. No toca las estadísticas (ya se anotaron al terminar). Tras completarse no repite nada.
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
                _finished = true;
                Result = result;
                if (OnResult != null) OnResult(result);
                yield break;
            }

            if (IsRanked)
            {
                ApiResult<GameResult> finished = null;
                yield return _api.FinishGame(_profile.playerKey, _gameId, r => finished = r);
                NoteStatus(finished);
                if (finished != null && finished.IsOk && finished.Value != null)
                {
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
                        FromServer = true,
                        Registered = true
                    };
                }
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

        private void Persist()
        {
            if (_store != null) _store.SavePack(_pack);
        }
    }
}
