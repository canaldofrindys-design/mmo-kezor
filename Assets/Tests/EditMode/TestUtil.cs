using System.Collections.Generic;
using Kezor.Core;

namespace Kezor.Tests
{
    /// <summary>RNG roteirizado: devolve valores fixos em sequência (repete o último).</summary>
    public sealed class FixedRandom : IRandom
    {
        private readonly Queue<int> _q; private int _last;
        public FixedRandom(params int[] values) { _q = new Queue<int>(values); }
        public int Next(int min, int max)
        {
            if (_q.Count > 0) _last = _q.Dequeue();
            if (_last < min) return min;
            if (_last >= max) return max - 1;
            return _last;
        }
    }
}
