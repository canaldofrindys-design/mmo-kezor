// Kezor (URP) — globais e funções compartilhadas por Kezor/LowPoly e Kezor/Terrain:
// textura de detalhe por material (Texture2DArray triplanar), névoa rasteira e iluminação URP (luz principal com
// sombra, luzes adicionais Forward+, ambiente por SH, SSAO) sobre a cor do vértice.
#ifndef KEZOR_COMMON_URP_INCLUDED
#define KEZOR_COMMON_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// ---- globais (DetailTextures.cs / RefineNoise.cs / KezorLook.ApplyEnvironment)
TEXTURE2D_ARRAY(_KezorDetail); SAMPLER(sampler_KezorDetail);
TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
float _KezorDetailOn;
float _KezorDetailTiling;
float4 _KezorLowFog;      // névoa rasteira: x = topo, y = espessura, z = intensidade, w = puxa para o tom do céu
float4 _KezorLowFogColor;
float _KezorEmissive;     // multiplicador global do emissivo (1 = normal; o Bloom da URP pega o que passa de 1)
float4 _KezorSeeTarget;   // xyz = ponto do herói que a câmera precisa ver, w = 1 liga o recorte
// luz ambiente e névoa por mapa (KezorLook.ApplyEnvironment): os mesmos valores do visual antigo
float4 _KezorSkyColor;
float4 _KezorGroundColor;
float4 _KezorFogColor;
float4 _KezorFogParams;   // x = início, y = fim, z = intensidade

half3 KezorDetailTri(float3 p, float3 n, float layer)
{
    float3 w = pow(abs(n), 4.0); w /= max(w.x + w.y + w.z, 1e-4);
    float t = _KezorDetailTiling > 0 ? _KezorDetailTiling : 1.6;
    half3 a = SAMPLE_TEXTURE2D_ARRAY(_KezorDetail, sampler_KezorDetail, p.zy * t, layer).rgb;
    half3 b = SAMPLE_TEXTURE2D_ARRAY(_KezorDetail, sampler_KezorDetail, p.xz * t, layer).rgb;
    half3 c = SAMPLE_TEXTURE2D_ARRAY(_KezorDetail, sampler_KezorDetail, p.xy * t, layer).rgb;
    return a * w.x + b * w.y + c * w.z;
}

// repetições por metro de cada material (id 1..16): tábuas e blocos em escala de construção, tecido e pele finos
static const float kKezorTiling[17] = { 1.0, 2.2, 2.6, 1.4, 2.2, 2.4, 1.1, 0.55, 0.45, 0.85, 1.2, 1.0, 1.0, 1.0, 1.0, 0.42, 0.38 };
static const float kKezorBump[17]   = { 0.0, 0.2, 0.6, 0.35, 0.6, 0.5, 0.7, 1.0, 0.5, 0.9, 0.7, 0.0, 0.0, 0.0, 0.0, 0.9, 1.0 };
float KezorTilingFor(int mid) { return kKezorTiling[clamp(mid, 0, 16)] * (_KezorDetailTiling > 0 ? _KezorDetailTiling : 1.0); }
float KezorBumpFor(int mid) { return kKezorBump[clamp(mid, 0, 16)]; }

half4 KezorDetailTri4(float3 p, float3 n, float layer, float t)
{
    float3 w = pow(abs(n), 6.0); w /= max(w.x + w.y + w.z, 1e-4);
    half4 a = SAMPLE_TEXTURE2D_ARRAY(_KezorDetail, sampler_KezorDetail, p.zy * t, layer);
    half4 b = SAMPLE_TEXTURE2D_ARRAY(_KezorDetail, sampler_KezorDetail, p.xz * t, layer);
    half4 c = SAMPLE_TEXTURE2D_ARRAY(_KezorDetail, sampler_KezorDetail, p.xy * t, layer);
    return a * w.x + b * w.y + c * w.z;
}

// Relevo sem UV nem tangente (Mikkelsen, "bump mapping unparametrized surfaces"): inclina a normal pela derivada
// da altura na tela. Serve para qualquer projeção (triplanar local ou do mundo).
float3 KezorBumpFromHeight(float3 n, float3 wp, float h, float k)
{
    if (k <= 0.0) return n;
    float3 dpdx = ddx(wp), dpdy = ddy(wp);
    float dhx = ddx(h), dhy = ddy(h);
    float3 r1 = cross(dpdy, n), r2 = cross(n, dpdx);
    float det = dot(dpdx, r1);
    float3 grad = sign(det) * (dhx * r1 + dhy * r2);
    float3 nn = normalize(abs(det) * n - grad * k * 0.06);
    return dot(nn, nn) > 0.5 ? nn : n;
}

// Luz URP (Blinn-Phong quase fosco) sobre o albedo: sombra da luz principal, Forward+, SSAO e ambiente.
half3 KezorLit(half3 albedo, float3 positionWS, half3 normalWS, float4 positionCS)
{
    InputData d = (InputData)0;
    d.positionWS = positionWS;
    d.normalWS = normalWS;
    d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    d.shadowCoord = TransformWorldToShadowCoord(positionWS);
    d.fogCoord = 0;
    // ambiente hemisférico (céu em cima, chão embaixo), igual ao visual antigo e sem depender de sonda bakeada
    d.bakedGI = lerp(_KezorGroundColor.rgb, _KezorSkyColor.rgb, normalWS.y * 0.5 + 0.5);
    d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    d.shadowMask = half4(1, 1, 1, 1);
    SurfaceData s = (SurfaceData)0;
    s.albedo = albedo;
    s.alpha = 1;
    s.specular = half3(0.04, 0.04, 0.04);
    s.smoothness = 0.25;
    s.occlusion = 1;
    return UniversalFragmentBlinnPhong(d, s).rgb;
}

// Névoa por distância do Kezor (_KezorFogParams, por mapa) + névoa rasteira nas baixadas.
// fogCoord fica na assinatura por compatibilidade; a névoa não depende do RenderSettings.
half3 KezorFog(half3 col, float3 wp, float fogCoord, half emissive)
{
    float d = distance(wp, _WorldSpaceCameraPos);
    float f = saturate((d - _KezorFogParams.x) / max(0.001, _KezorFogParams.y - _KezorFogParams.x)) * _KezorFogParams.z;
    col = lerp(col, _KezorFogColor.rgb, f * (1.0 - emissive * 0.7));
    if (_KezorLowFog.z > 0.001)
    {
        float wave = sin(wp.x * 0.13 + _Time.y * 0.35) * sin(wp.z * 0.11 - _Time.y * 0.27) * 0.35;
        float h = saturate((_KezorLowFog.x + wave - wp.y) / max(0.05, _KezorLowFog.y));
        float lf = h * h * _KezorLowFog.z * saturate((d - 6.0) / 18.0);
        col = lerp(col, _KezorLowFogColor.rgb, lf * (1.0 - emissive));
    }
    return col;
}

#endif
