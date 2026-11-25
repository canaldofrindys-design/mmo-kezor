using System.Collections.Generic;
using UnityEngine;
using Kezor.Core.World;

namespace Kezor.Client.UI
{
    /// <summary>
    /// Desenha o mapa inteiro numa textura (uma vez por mapa): terreno sombreado pela altura, vila e estradas, muralha,
    /// salões da masmorra e poças. O minimapa recorta um círculo dela centrado no herói.
    /// </summary>
    public static class MinimapTexture
    {
        private static readonly Dictionary<int, Texture2D> Cache = new Dictionary<int, Texture2D>();

        public static Texture2D Build(MapDefinition map, int size = 320)
        {
            if (Cache.TryGetValue(map.Id, out var cached) && cached != null) return cached;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[size * size];
            bool crypt = map.Id == MapDefinition.CryptId, swamp = map.Id == MapDefinition.SwampId;
            Color baseLow = crypt ? new Color(0.03f, 0.03f, 0.05f) : swamp ? new Color(0.13f, 0.16f, 0.11f) : new Color(0.15f, 0.24f, 0.13f);
            Color baseHigh = crypt ? new Color(0.06f, 0.06f, 0.09f) : swamp ? new Color(0.24f, 0.26f, 0.17f) : new Color(0.3f, 0.36f, 0.2f);
            float step = map.Size / size;

            // faixa de alturas para o sombreado
            float hMin = float.MaxValue, hMax = float.MinValue;
            for (int i = 0; i < 400; i++)
            {
                float h = WorldBuilder.Height((i % 20 + 0.5f) * map.Size / 20f, (i / 20 + 0.5f) * map.Size / 20f);
                hMin = Mathf.Min(hMin, h); hMax = Mathf.Max(hMax, h);
            }
            float hRange = Mathf.Max(0.5f, hMax - hMin);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float wx = (x + 0.5f) * step, wz = map.Size - (y + 0.5f) * step;      // linha 0 = norte (z alto)
                    float h = WorldBuilder.Height(wx, wz);
                    float hx = WorldBuilder.Height(wx + step, wz) - h;                   // luz vindo do noroeste
                    var c = Color.Lerp(baseLow, baseHigh, Mathf.Clamp01((h - hMin) / hRange));
                    c *= 1f + Mathf.Clamp(-hx * 0.6f, -0.25f, 0.25f);

                    if (map.SafeRadius > 0)
                    {
                        float d = Mathf.Sqrt((wx - map.SafeX) * (wx - map.SafeX) + (wz - map.SafeZ) * (wz - map.SafeZ));
                        bool road = Mathf.Abs(wx - map.SafeX) < 1.9f || Mathf.Abs(wz - map.SafeZ) < 1.9f
                                    || (map.HasWall && d < map.WallRadius && (Mathf.Abs(d - MapDefinition.RingRoadRadius) < 1.4f || Mathf.Abs(d - MapDefinition.OuterRingRadius) < 1.4f));
                        if (road) c = Color.Lerp(c, new Color(0.42f, 0.33f, 0.21f), 0.85f);
                        if (d < map.SafeRadius) c = Color.Lerp(new Color(0.42f, 0.4f, 0.36f), c, road ? 0.4f : 0.15f);
                        if (map.HasWall && Mathf.Abs(d - map.WallRadius) < map.WallHalfThickness + 0.6f) c = new Color(0.55f, 0.52f, 0.47f);
                    }
                    foreach (var f in map.Floors)
                        if (wx >= f.X0 && wx <= f.X1 && wz >= f.Z0 && wz <= f.Z1) { c = new Color(0.25f, 0.23f, 0.31f); break; }
                    if (swamp)
                        foreach (var o in map.Obstacles)
                            if ((wx - o.X) * (wx - o.X) + (wz - o.Z) * (wz - o.Z) < o.R * o.R) { c = new Color(0.05f, 0.08f, 0.07f); break; }
                    foreach (var house in map.Houses)
                        if (Mathf.Abs(wx - house.X) < 3.2f * house.Scale && Mathf.Abs(wz - house.Z) < 3.2f * house.Scale) { c = new Color(0.36f, 0.22f, 0.16f); break; }
                    c.a = 1f;
                    px[(size - 1 - y) * size + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            Cache[map.Id] = tex;
            return tex;
        }
    }
}
