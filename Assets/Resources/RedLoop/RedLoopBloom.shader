// RED LOOP — hafif bloom (parıltı) + vinyet. Built-in render pipeline, OnRenderImage ile kullanılır.
Shader "Hidden/RedLoop/Bloom"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float _Threshold;
    float _Intensity;
    float _Vignette;

    // Geçersiz (NaN / negatif / aşırı büyük) renkleri temizler.
    // Temizlenmezse tek bir bozuk piksel küçültme adımlarında büyüyüp ekranda kare kare siyah lekelere dönüşür.
    float3 Safe(float3 v)
    {
        v = (v >= 0.0) ? v : 0.0;   // NaN karşılaştırması her zaman false döner → 0
        return min(v, 16.0);
    }

    float3 Box(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-d, d).xxyy;
        float3 s = Safe(tex2D(_MainTex, uv + o.xy).rgb) + Safe(tex2D(_MainTex, uv + o.zy).rgb) +
                   Safe(tex2D(_MainTex, uv + o.xw).rgb) + Safe(tex2D(_MainTex, uv + o.zw).rgb);
        return s * 0.25;
    }

    float4 FragPrefilter(v2f_img i) : SV_Target
    {
        float3 c = Box(i.uv, 1.0);
        float br = max(c.r, max(c.g, c.b));
        float knee = 0.5;
        float soft = clamp(br - _Threshold + knee, 0.0, 2.0 * knee);
        soft = soft * soft / (4.0 * knee + 1e-4);
        float contrib = max(soft, br - _Threshold) / max(br, 1e-4);
        return float4(Safe(c * contrib), 1.0);
    }

    float4 FragDown(v2f_img i) : SV_Target
    {
        return float4(Box(i.uv, 1.0), 1.0);
    }

    float4 FragUp(v2f_img i) : SV_Target
    {
        return float4(Box(i.uv, 0.5), 1.0);
    }

    float4 FragFinal(v2f_img i) : SV_Target
    {
        float4 c = tex2D(_MainTex, i.uv);
        c.rgb = Safe(c.rgb);
        c.rgb += Safe(tex2D(_BloomTex, i.uv).rgb) * _Intensity;
        float2 d = i.uv - 0.5;
        float v = 1.0 - dot(d, d) * _Vignette * 2.2;
        c.rgb *= saturate(v);
        return c;
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragPrefilter
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragDown
            ENDCG
        }
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragUp
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragFinal
            ENDCG
        }
    }
    Fallback Off
}
