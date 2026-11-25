// Bloom do Kezor (pipeline Built-in): pega o que passa do limiar no buffer HDR, desfoca numa cadeia de
// reduções/ampliações e soma de volta. Passes: 0 = pré-filtro com joelho suave, 1 = redução (4 amostras),
// 2 = ampliação aditiva (tenda 3x3), 3 = composição final com leve tonemap.
Shader "Hidden/Kezor/Bloom"
{
    Properties { _MainTex ("", 2D) = "black" {} _BloomTex ("", 2D) = "black" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex; float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float4 _Params;   // x = limiar, y = joelho, z = intensidade, w = espalhamento
    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    half3 Box4(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }
    half4 fragPrefilter (v2f i) : SV_Target
    {
        half3 c = Box4(i.uv, 1.0);
        half br = max(c.r, max(c.g, c.b));
        half soft = clamp(br - _Params.x + _Params.y, 0, 2 * _Params.y);
        soft = soft * soft / (4 * _Params.y + 1e-4);
        half contrib = max(soft, br - _Params.x) / max(br, 1e-4);
        return half4(min(c * contrib, 30.0), 1);
    }
    half4 fragDown (v2f i) : SV_Target { return half4(Box4(i.uv, 1.0), 1); }
    half4 fragUp (v2f i) : SV_Target
    {
        float4 d = _MainTex_TexelSize.xyxy * float4(1, 1, -1, 0) * _Params.w;
        half3 s = tex2D(_MainTex, i.uv - d.xy).rgb;
        s += tex2D(_MainTex, i.uv - d.wy).rgb * 2;
        s += tex2D(_MainTex, i.uv - d.zy).rgb;
        s += tex2D(_MainTex, i.uv + d.zw).rgb * 2;
        s += tex2D(_MainTex, i.uv).rgb * 4;
        s += tex2D(_MainTex, i.uv + d.xw).rgb * 2;
        s += tex2D(_MainTex, i.uv + d.zy).rgb;
        s += tex2D(_MainTex, i.uv + d.wy).rgb * 2;
        s += tex2D(_MainTex, i.uv + d.xy).rgb;
        return half4(s * (1.0 / 16), 1);
    }
    half4 fragCombine (v2f i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb + tex2D(_BloomTex, i.uv).rgb * _Params.z;
        // ombro suave: até 0,9 fica igual, acima comprime em vez de estourar em branco chapado
        half3 over = max(c - 0.9, 0);
        c = min(c, 0.9) + over / (1 + over * 1.6);
        return half4(c, 1);
    }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown
            ENDCG }
        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragCombine
            ENDCG }
    }
    Fallback Off
}
