using System;

namespace RobotBombero.Simulation
{
    /// <summary>
    /// Xorshift32 propio: System.Random no garantiza el mismo resultado entre Mono, IL2CPP y .NET,
    /// y el servidor debe reproducir exactamente cada partida.
    /// </summary>
    public class DeterministicRandom
    {
        // Xorshift se queda en 0 para siempre si parte de 0.
        private const uint ZeroSeedReplacement = 2463534242;

        private uint _state;

        public DeterministicRandom(uint seed)
        {
            _state = seed == 0 ? ZeroSeedReplacement : seed;
        }

        public int Next(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            }

            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state % (uint)exclusiveMax);
        }
    }
}
