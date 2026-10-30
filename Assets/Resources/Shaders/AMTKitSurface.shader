// Lit surface for the kit parts. Adds a soft rim light for shape definition and a pulsing
// highlight driven per renderer through a MaterialPropertyBlock (_Highlight, _HighlightColor).
Shader "AMT/KitSurface"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0.1
        _Glossiness ("Smoothness", Range(0, 1)) = 0.45
        _RimColor ("Rim color", Color) = (0.45, 0.55, 0.65, 1)
        _RimPower ("Rim power", Range(0.5, 8)) = 3
        _RimStrength ("Rim strength", Range(0, 1)) = 0.22
        _HighlightColor ("Highlight color", Color) = (1, 0.65, 0.1, 1)
        _Highlight ("Highlight amount", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200
        // The source CAD materials are double-sided.
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        struct Input
        {
            float3 viewDir;
            float3 worldNormal;
        };

        fixed4 _Color;
        half _Metallic;
        half _Glossiness;
        fixed4 _RimColor;
        half _RimPower;
        half _RimStrength;
        fixed4 _HighlightColor;
        half _Highlight;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            half facing = abs(dot(normalize(IN.viewDir), normalize(IN.worldNormal)));
            half rim = 1.0 - saturate(facing);
            half pulse = 0.6 + 0.4 * sin(_Time.y * 5.0);
            o.Albedo = lerp(_Color.rgb, _HighlightColor.rgb, _Highlight * 0.6);
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _RimColor.rgb * pow(rim, _RimPower) * _RimStrength
                       + _HighlightColor.rgb * _Highlight * pulse * (0.35 + 0.65 * pow(rim, 1.5));
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
