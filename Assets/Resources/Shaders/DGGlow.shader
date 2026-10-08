// Divergent Genesis - additive glow
//
// Unlit and additive: wisps, projectile cores, dragon breath, the rift, arrow
// trails and the pulse that runs down a pedestal when it accepts an offering.
// Colours are pure vertex colour so one material covers every effect, and the
// brightness is deliberately allowed past 1.0 for the HDR bloom to catch.

Shader "DG/Glow"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0,4)) = 1.0
        _FogColor ("Fog Colour", Color) = (0.62, 0.74, 0.88, 1)
        _FogDensity ("Fog Density", Float) = 0.0014
        _Pulse ("Pulse", Range(0,2)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _FogColor;
            float _Intensity;
            float _FogDensity;
            float _Pulse;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 col : TEXCOORD0;
                float  fog : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityWorldToClipPos(float4(worldPos, 1.0));

                float pulse = 1.0 + _Pulse * sin(_Time.y * 5.0);
                o.col = v.color * _Color * (_Intensity * pulse);

                float dist = distance(worldPos, _WorldSpaceCameraPos);
                o.fog = saturate(1.0 - exp(-pow(dist * _FogDensity, 2.0)));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = i.col;
                // additive, so fog fades the glow out rather than tinting it grey
                c.rgb *= 1.0 - i.fog * 0.9;
                return c;
            }
            ENDCG
        }
    }

    FallBack Off
}