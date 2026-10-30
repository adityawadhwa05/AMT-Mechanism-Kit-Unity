// Ground disc that fades out radially and darkens where the kit casts a shadow.
// Viewer: soft floor colour. AR: set _BaseAlpha to 0 to get a shadow-only catcher on real surfaces.
Shader "AMT/Ground"
{
    Properties
    {
        _Color ("Floor color", Color) = (0.16, 0.19, 0.23, 1)
        _BaseAlpha ("Floor opacity", Range(0, 1)) = 1
        _ShadowColor ("Shadow color", Color) = (0, 0, 0, 1)
        _ShadowStrength ("Shadow strength", Range(0, 1)) = 0.55
        _Fade ("Edge fade start", Range(0, 1)) = 0.35
    }
    SubShader
    {
        // Rendered with the opaque geometry so it receives the directional light's shadows.
        Tags { "Queue" = "Geometry+1" "RenderType" = "Opaque" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"

            fixed4 _Color;
            half _BaseAlpha;
            fixed4 _ShadowColor;
            half _ShadowStrength;
            half _Fade;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                SHADOW_COORDS(1)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half r = length(i.uv - 0.5) * 2.0;
                half edge = 1.0 - smoothstep(_Fade, 1.0, r);
                half shadow = 1.0 - SHADOW_ATTENUATION(i);
                half shade = shadow * _ShadowStrength;
                fixed3 rgb = lerp(_Color.rgb, _ShadowColor.rgb, saturate(shade / max(_BaseAlpha + shade, 0.001)));
                half alpha = saturate(_BaseAlpha + shade) * edge;
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
    // Supplies the shadow-caster pass so the disc is part of the camera depth texture.
    FallBack "Legacy Shaders/VertexLit"
}
