// X-ray style transparent material used to keep context visible while one part is isolated.
Shader "AMT/Ghost"
{
    Properties
    {
        _Color ("Fill", Color) = (0.55, 0.68, 0.80, 0.06)
        _RimColor ("Edge", Color) = (0.60, 0.80, 0.95, 0.45)
        _RimPower ("Edge power", Range(0.5, 8)) = 2.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        Cull Back
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _RimColor;
            half _RimPower;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 viewDir : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half rim = 1.0 - saturate(abs(dot(normalize(i.normal), normalize(i.viewDir))));
                return lerp(_Color, _RimColor, pow(rim, _RimPower));
            }
            ENDCG
        }
    }
    FallBack Off
}
