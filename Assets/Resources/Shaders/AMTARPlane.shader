// Detected-surface visual for AR: a light dot grid in world space over a faint fill.
Shader "AMT/ARPlane"
{
    Properties
    {
        _Color ("Dot color", Color) = (0.95, 0.75, 0.30, 0.65)
        _Fill ("Fill color", Color) = (0.95, 0.75, 0.30, 0.06)
        _Spacing ("Dot spacing (m)", Float) = 0.06
        _DotSize ("Dot radius (fraction)", Range(0.02, 0.5)) = 0.09
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _Fill;
            float _Spacing;
            half _DotSize;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 cell = frac(i.world.xz / _Spacing) - 0.5;
                half d = length(cell);
                half dotMask = 1.0 - smoothstep(_DotSize * 0.7, _DotSize, d);
                return lerp(_Fill, _Color, dotMask);
            }
            ENDCG
        }
    }
    FallBack Off
}
