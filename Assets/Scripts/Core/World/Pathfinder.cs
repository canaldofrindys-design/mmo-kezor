using System;
using System.Collections.Generic;

namespace Kezor.Core.World
{
    /// <summary>
    /// A* numa grade de 1 m sobre o mapa (8 vizinhos, sem cortar quina), seguido de "puxar o barbante":
    /// o caminho fica só com os pontos onde precisa virar. A grade de bloqueio é calculada uma vez por mapa.
    /// Usado quando a linha reta até o destino bate em muralha, casa, poça ou parede de masmorra.
    /// </summary>
    public sealed class Pathfinder
    {
        public const float Cell = 1f;
        public const int MaxExpanded = 30000;
        private readonly MapDefinition _map;
        private readonly int _n;
        private readonly bool[] _blocked;

        public Pathfinder(MapDefinition map)
        {
            _map = map;
            _n = (int)Math.Ceiling(map.Size / Cell);
            _blocked = new bool[_n * _n];
            for (int z = 0; z < _n; z++)
                for (int x = 0; x < _n; x++)
                    _blocked[z * _n + x] = map.IsBlocked((x + 0.5f) * Cell, (z + 0.5f) * Cell);
        }

        private bool Free(int x, int z) => x >= 0 && z >= 0 && x < _n && z < _n && !_blocked[z * _n + x];

        /// <summary>Caminho de (ax,az) até (bx,bz), sem o ponto inicial e terminando no destino. null = sem caminho.</summary>
        public List<(float x, float z)>? Find(float ax, float az, float bx, float bz)
        {
            if (_map.LineClear(ax, az, bx, bz)) return new List<(float, float)> { (bx, bz) };
            int sx = Clamp((int)(ax / Cell)), sz = Clamp((int)(az / Cell));
            int tx = Clamp((int)(bx / Cell)), tz = Clamp((int)(bz / Cell));
            if (!Free(tx, tz) && !NearestFree(ref tx, ref tz)) return null;
            if (!Free(sx, sz)) NearestFree(ref sx, ref sz);

            int start = sz * _n + sx, goal = tz * _n + tx;
            var g = new Dictionary<int, float> { [start] = 0 };
            var from = new Dictionary<int, int>();
            var open = new SortedSet<(float f, int id)>();
            open.Add((H(sx, sz, tx, tz), start));
            var closed = new HashSet<int>();
            int expanded = 0;
            while (open.Count > 0 && expanded++ < MaxExpanded)
            {
                var cur = open.Min; open.Remove(cur);
                int id = cur.id;
                if (id == goal) return Smooth(Rebuild(from, id), ax, az, bx, bz);
                if (!closed.Add(id)) continue;
                int cx = id % _n, cz = id / _n;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int nx = cx + dx, nz = cz + dz;
                        if (!Free(nx, nz)) continue;
                        if (dx != 0 && dz != 0 && (!Free(cx + dx, cz) || !Free(cx, cz + dz))) continue; // não corta quina
                        int nid = nz * _n + nx;
                        if (closed.Contains(nid)) continue;
                        float ng = g[id] + (dx != 0 && dz != 0 ? 1.4142f : 1f);
                        if (g.TryGetValue(nid, out var old) && old <= ng) continue;
                        g[nid] = ng; from[nid] = id;
                        open.Add((ng + H(nx, nz, tx, tz), nid));
                    }
            }
            return null;
        }

        private int Clamp(int v) => Math.Max(0, Math.Min(_n - 1, v));
        private static float H(int x, int z, int tx, int tz) { int dx = Math.Abs(x - tx), dz = Math.Abs(z - tz); return Math.Max(dx, dz) + 0.4142f * Math.Min(dx, dz); }

        private bool NearestFree(ref int x, ref int z)
        {
            for (int r = 1; r <= 6; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                        if (Free(x + dx, z + dz)) { x += dx; z += dz; return true; }
            return false;
        }

        private List<(float x, float z)> Rebuild(Dictionary<int, int> from, int id)
        {
            var path = new List<(float, float)>();
            while (from.TryGetValue(id, out var prev)) { path.Add(((id % _n + 0.5f) * Cell, (id / _n + 0.5f) * Cell)); id = prev; }
            path.Reverse();
            return path;
        }

        /// <summary>Remove pontos intermediários enquanto a linha reta continuar livre.</summary>
        private List<(float x, float z)> Smooth(List<(float x, float z)> path, float ax, float az, float bx, float bz)
        {
            if (path.Count == 0 || _map.LineClear(path[path.Count - 1].x, path[path.Count - 1].z, bx, bz)) path.Add((bx, bz));
            var outp = new List<(float, float)>();
            float px = ax, pz = az;
            int i = 0;
            while (i < path.Count)
            {
                int j = path.Count - 1;
                while (j > i && !_map.LineClear(px, pz, path[j].x, path[j].z)) j--;
                outp.Add(path[j]);
                px = path[j].x; pz = path[j].z;
                i = j + 1;
            }
            return outp;
        }
    }
}
