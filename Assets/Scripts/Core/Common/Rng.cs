using System;

namespace Kezor.Core
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
        public static bool RollPerMil(this IRandom r, int perMil) => r.Next(0, 1000) < perMil;
        /// <summary>Float em [min,max) com resolução de 1/1000.</summary>
        public static float Range(this IRandom r, float min, float max) => min + (max - min) * r.Next(0, 1000) / 1000f;
    }

    public static class MathK
    {
        public static float Dist(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx, dz = az - bz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
