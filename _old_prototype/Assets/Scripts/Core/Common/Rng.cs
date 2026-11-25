using System;

namespace MuProto.Core
{
    /// <summary>Abstração de RNG para permitir testes determinísticos.</summary>
    public interface IRandom
    {
        int Next(int minInclusive, int maxExclusive);
    }

    public sealed class SystemRandom : IRandom
    {
        private readonly Random _r;
        public SystemRandom(int? seed = null) { _r = seed.HasValue ? new Random(seed.Value) : new Random(); }
        public int Next(int minInclusive, int maxExclusive) => _r.Next(minInclusive, maxExclusive);
    }

    public static class RandomExtensions
    {
        /// <summary>Retorna true com probabilidade percent% (0..100).</summary>
        public static bool Roll(this IRandom r, int percent) => r.Next(0, 100) < percent;
    }
}
