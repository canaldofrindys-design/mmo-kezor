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

half3 KezorDetailTri(float3 p, float3 n, float layer)
{
    float3 w = pow(abs(n), 4.0); w /= max(w.x + w.y + w.z, 1e-4);
    float t = _KezorDetailTiling > 0 ? _KezorDetailTiling : 1.6;
    half3 a = SAMPLE_TEXTURE2D_ARRAY(_KezorDetail, sampler_KezorDetail, p.zy * t, layer).rgb;
    half3 b = SAMPLE_TEXTURE2D_ARRAY(_KezorDetail, sampler_KezorDetail, p.xz * t, layer).rgb;
    half3 c = SAMPLE_TEXTURE2D_ARRAY(_KezorDetail, sampler_KezorDetail, p.xy * t, layer).rgb;
    return a * w.x + b * w.y + c * w.z;
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
    d.bakedGI = SampleSH(normalWS);
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

// Névoa da URP (linear, RenderSettings) + névoa rasteira do Kezor nas baixadas
half3 KezorFog(half3 col, float3 wp, float fogCoord, half emissive)
{
    half3 fogged = MixFog(col, fogCoord);
    col = lerp(fogged, col, emissive * 0.7);
    if (_KezorLowFog.z > 0.001)
    {
        float d = distance(wp, _WorldSpaceCameraPos);
        float wave = sin(wp.x * 0.13 + _Time.y * 0.35) * sin(wp.z * 0.11 - _Time.y * 0.27) * 0.35;
        float h = saturate((_KezorLowFog.x + wave - wp.y) / max(0.05, _KezorLowFog.y));
        float lf = h * h * _KezorLowFog.z * saturate((d - 6.0) / 18.0);
        col = lerp(col, _KezorLowFogColor.rgb, lf * (1.0 - emissive));
    }
    return col;
}

#endif
