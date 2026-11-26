// RED LOOP — ışık saçan nesneler için ışıksız (unlit) parlak shader, kenar parıltılı.
Shader "RedLoop/Glow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 2
        _Rim ("Rim", Float) = 0.8
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Intensity;
            float _Rim;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half rim = 1.0 - saturate(dot(normalize(i.normal), normalize(i.viewDir)));
                half k = (1.0 - _Rim * 0.5) + _Rim * rim * rim * 1.5;
                half4 c = half4(_Color.rgb * _Intensity * k, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
