// Kezor/LowPoly (URP): cor por vértice, normais suaves, textura de detalhe por material (UV2.x = id), luz da URP
// (sombra da luz principal, luzes adicionais Forward+, SSAO, ambiente), névoa da URP + névoa rasteira, emissivo por
// alfa do vértice (< 0,5 brilha; o Bloom do Volume pega acima de 1), brilho de runa, fluxo de refino +11/+13/+15
// (Fresnel × ruído subindo) e recorte pontilhado na frente do herói. Compatível com SRP Batcher e instancing.
Shader "Kezor/LowPoly"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Flash ("Flash", Range(0,1)) = 0
        _Occluder ("Recorta na frente do herói", Float) = 0
        _Glow ("Brilho de runa", Range(0,1)) = 0
        [HDR] _GlowColor ("Cor do brilho (HDR)", Color) = (1,0.8,0.4,1)
        _Hdr ("Brilho HDR extra (refino alto, épicas, asas)", Range(0,8)) = 0
        _Wind ("Vento (grama, flores: balanço pela altura)", Range(0,1)) = 0
        _RefineFlow ("Fluxo de refino (+11 fraco, +13/+15 forte; 0 = desligado)", Range(0,2)) = 0
        _GlowStrength ("Força do fluxo", Range(0,4)) = 0.55
        _FresnelPower ("Potência do Fresnel", Range(0.5,8)) = 2.6
        _FlowContrast ("Contraste do fluxo", Range(0.5,6)) = 2.6
        _ScrollSpeed ("Velocidade de subida", Range(0,3)) = 0.45
        _BaseGlow ("Brilho base do fluxo", Range(0,1)) = 0.18
        _FlowScale ("Escala do ruído (repetições/m)", Range(0.2,8)) = 2.2
        _Unlit ("Sem luz (partículas)", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
        half4 _Tint;
        half _Flash;
        half _Occluder;
        half _Glow;
        half4 _GlowColor;
        half _Hdr;
        half _Wind;
        half _RefineFlow, _GlowStrength, _FresnelPower, _FlowContrast, _ScrollSpeed, _BaseGlow, _FlowScale;
        half _Unlit;
    CBUFFER_END

    // balanço do vento: cresce com a altura do vértice no objeto (a base fica presa)
    float3 KezorWind(float3 positionOS, float3 wp)
    {
        if (_Wind > 0.001)
        {
            float hgt = max(positionOS.y, 0.0);
            float t = _Time.y * 1.7 + wp.x * 0.35 + wp.z * 0.27;
            wp.xz += float2(sin(t), cos(t * 0.8)) * _Wind * hgt * (0.6 + 0.4 * sin(t * 2.3));
        }
        return wp;
    }
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
            #pragma multi_compile_instancing
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv1 : TEXCOORD1; float3 uv3 : TEXCOORD3; float3 uv4 : TEXCOORD4; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; half4 color : COLOR; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2; float3 normalOS : TEXCOORD3; float mid : TEXCOORD4; float fogCoord : TEXCOORD5;
                float3 texPos : TEXCOORD6; float3 texNrm : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert (Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 wp = KezorWind(v.positionOS.xyz, TransformObjectToWorld(v.positionOS.xyz));
                o.positionCS = TransformWorldToHClip(wp);
                o.positionWS = wp;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.color.rgb *= _Tint.rgb;
                o.positionOS = v.positionOS.xyz;
                o.normalOS = v.normalOS;
                o.mid = v.uv1.x;
                // espaço da textura: malhas combinadas (StaticBatch) trazem a posição/normal local do modelo em UV3/UV4,
                // assim tábuas e blocos seguem a parede de cada casa, qualquer que seja o giro dela
                bool hasTex = dot(v.uv4, v.uv4) > 0.25;
                o.texPos = hasTex ? v.uv3 : v.positionOS.xyz;
                o.texNrm = hasTex ? v.uv4 : v.normalOS;
                o.fogCoord = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // Limiares de Bayer 4x4 para o pontilhado (transparência sem ordenar malhas)
            static const float kBayer[16] = { 0.03, 0.53, 0.16, 0.66, 0.78, 0.28, 0.91, 0.41, 0.22, 0.72, 0.09, 0.59, 0.97, 0.47, 0.84, 0.34 };

            float FlowNoise(float3 p, float3 n, float t, float scale)
            {   // ruído projetado no objeto (triplanar); nas laterais o fluxo "sobe"
                float3 w = pow(abs(n), 4.0); w /= max(w.x + w.y + w.z, 1e-4);
                p *= scale;
                float a = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, float2(p.z, p.y - t)).r;
                float b = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, float2(p.x, p.z + t)).r;
                float c = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, float2(p.x, p.y - t)).r;
                return a * w.x + b * w.y + c * w.z;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                if (_Occluder > 0.5 && _KezorSeeTarget.w > 0.5)
                {   // o que fica entre a câmera e o herói vira pontilhado (some ~90% no centro)
                    float3 L = _KezorSeeTarget.xyz - _WorldSpaceCameraPos;
                    float len = length(L);
                    float3 dir = L / max(len, 0.001);
                    float3 v = i.positionWS - _WorldSpaceCameraPos;
                    float t = dot(v, dir);
                    if (t > 0.0 && t < len - 0.9)
                    {
                        float perp = length(v - dir * t);
                        float k = saturate(1.25 - perp / 2.8) * saturate((len - 0.9 - t) / 1.5);
                        uint2 px = (uint2)i.positionCS.xy % 4;
                        if (kBayer[px.y * 4 + px.x] < k * 0.92) discard;
                    }
                }
                float3 n = normalize(i.normalWS);
                half3 albedo = i.color.rgb;
                if (_KezorDetailOn > 0.5 && i.mid > 0.5)
                {   // textura do material: tom, cavidade (juntas e frestas escurecem) e relevo pela derivada do tom
                    int mid = (int)floor(i.mid + 0.5);
                    half4 d = KezorDetailTri4(i.texPos, normalize(i.texNrm), mid - 1.0, KezorTilingFor(mid));
                    half tone = 0.42 + 1.16 * d.b;
                    albedo *= tone;
                    albedo = lerp(albedo, albedo * half3(0.40, 0.34, 0.29), d.a * 0.85);
                    // variação grande (manchas de 5-15 m) para paredes e chão não parecerem pintados de uma cor só
                    half macro = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, i.positionWS.xz * 0.045 + i.positionWS.y * 0.02).r;
                    albedo *= 0.9 + 0.2 * macro;
                    n = KezorBumpFromHeight(n, i.positionWS, d.b - d.a * 0.6, KezorBumpFor(mid));
                }
                half e = saturate((0.5 - i.color.a) * 2.0);
                half3 lit = _Unlit > 0.5 ? albedo : KezorLit(albedo, i.positionWS, n, i.positionCS);
                half emis = 1.5 * (_KezorEmissive > 0.0 ? _KezorEmissive : 1.0) * (1.0 + _Hdr);
                half3 col = lerp(lit, i.color.rgb * emis, e);
                col = KezorFog(col, i.positionWS, i.fogCoord, e);
                if (_RefineFlow > 0.001)
                {   // fluxo de refino: Fresnel na silhueta × energia que sobe pelo ruído (+11 brasa, +13 violeta, +15 ciano)
                    float3 on = normalize(i.normalOS);
                    float t = _Time.y * _ScrollSpeed;
                    float n1 = FlowNoise(i.positionOS, on, t, _FlowScale);
                    float n2 = FlowNoise(i.positionOS, on, -0.6 * t, _FlowScale * 1.7);
                    float flow = pow(saturate(n1 * n2 * 2.0), _FlowContrast);
                    float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                    float fres = pow(1.0 - saturate(dot(n, V)), _FresnelPower);
                    float mask = fres * (0.4 + 1.6 * flow) + flow * _BaseGlow;
                    col += _GlowColor.rgb * _GlowStrength * _RefineFlow * mask;
                }
                // brilho de runa (+7..+15): pulsa somado à cor; as partes emissivas brilham mais
                if (_Glow > 0.001) col += _GlowColor.rgb * _Glow * (0.65 + 0.35 * sin(_Time.y * (2.0 + _Glow * 4.0) + i.positionWS.y * 2.0)) * (1.0 + e) * (1.0 + _Hdr * 0.2);
                col = lerp(col, half3(1, 1, 1), _Flash);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert (Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 wp = KezorWind(v.positionOS.xyz, TransformObjectToWorld(v.positionOS.xyz));
                float3 nw = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 ld = normalize(_LightPosition - wp);
            #else
                float3 ld = _LightDirection;
            #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(wp, nw, ld)));
                return o;
            }
            half4 frag (Varyings i) : SV_Target { return 0; }
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
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert (Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                o.positionCS = TransformWorldToHClip(KezorWind(v.positionOS.xyz, TransformObjectToWorld(v.positionOS.xyz)));
                return o;
            }
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
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert (Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                o.positionCS = TransformWorldToHClip(KezorWind(v.positionOS.xyz, TransformObjectToWorld(v.positionOS.xyz)));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag (Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
    Fallback Off
}
