// Kezor/Terrain (URP): malha suave com splat por vértice (UV1 = grama, terra; UV2 = calçamento, cascalho). A cor base
// vem na cor do vértice; as texturas de detalhe das quatro camadas dão relevo e variação. Recebe sombra, luzes
// adicionais Forward+, SSAO e névoa da URP (+ névoa rasteira).
Shader "Kezor/Terrain"
{
    Properties
    {
        _Tiling ("Repetições por metro", Float) = 0.5
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
        float _Tiling;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _LIGHT_LAYERS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "KezorCommon.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float4 w : TEXCOORD2; float fogCoord : TEXCOORD3; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.w = float4(v.uv1, v.uv2);
                o.fogCoord = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // camadas (DetailTextures): 10 grama, 11 terra, 12 calçamento, 13 cascalho
            half3 Layer(float2 uv, float layer) { return SAMPLE_TEXTURE2D_ARRAY(_KezorDetail, sampler_KezorDetail, uv, layer).rgb; }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                half detail = 1.0;
                if (_KezorDetailOn > 0.5)
                {
                    float4 w = i.w / max(i.w.x + i.w.y + i.w.z + i.w.w, 1e-3);
                    float2 uv = i.positionWS.xz * _Tiling;
                    half3 d = 0;
                    if (w.x > 0.01) d += Layer(uv * 1.7, 10) * w.x;
                    if (w.y > 0.01) d += Layer(uv * 1.2 + 0.37, 11) * w.y;
                    if (w.z > 0.01) d += Layer(uv, 12) * w.z;
                    if (w.w > 0.01) d += Layer(uv * 2.1 + 0.71, 13) * w.w;
                    detail = 0.72 + 0.56 * d.b;
                    float2 bump = (d.rg - 0.5) * 1.4;
                    n = normalize(n + float3(bump.x, 0, bump.y));
                }
                half3 col = KezorLit(i.color.rgb * detail, i.positionWS, n, i.positionCS);
                col = KezorFog(col, i.positionWS, i.fogCoord, 0);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert (Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half frag (Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert (Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 frag (Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
    Fallback Off
}
