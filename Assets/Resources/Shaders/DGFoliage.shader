// Divergent Genesis - foliage (grass tufts, flowers, leaves detail)
// Alpha-tested, double sided, with a cheap wind sway. Alpha testing instead of
// blending so grass does not need sorting and costs no overdraw blending.

Shader "DG/Foliage"
{
    Properties
    {
        _SunDir ("Sun Direction", Vector) = (0.35, 0.78, 0.30, 0)
        _SunColor ("Sun Colour", Color) = (1.0, 0.96, 0.88, 1)
        _Ambient ("Ambient Sky", Color) = (0.42, 0.48, 0.58, 1)
        _FogColor ("Fog Colour", Color) = (0.62, 0.74, 0.88, 1)
        _FogDensity ("Fog Density", Float) = 0.0014
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.35
        _WindStrength ("Wind Strength", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        LOD 80

        Pass
        {
            Cull Off
            ZWrite On
            AlphaTest [_Cutoff]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _SunColor;
            fixed4 _Ambient;
            fixed4 _FogColor;
            float4 _SunDir;
            float  _FogDensity;
            float  _Cutoff;
            float  _WindStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos  : SV_POSITION;
                fixed3 col  : TEXCOORD0;
                float  fog  : TEXCOORD1;
                float2 uv   : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                // sway grows with height above the plant base (uv.y = 0 at roots)
                float sway = sin(_Time.y * 1.6 + worldPos.x * 0.6 + worldPos.z * 0.5);
                worldPos.x += sway * 0.11 * v.uv.y * _WindStrength;
                worldPos.z += sway * 0.07 * v.uv.y * _WindStrength;

                float3 n = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float ndl = saturate(dot(n, normalize(_SunDir.xyz)) * 0.5 + 0.5);
                o.col = v.color.rgb * (_Ambient.rgb + _SunColor.rgb * ndl * 0.7);
                o.uv = v.uv;
                o.pos = UnityWorldToClipPos(float4(worldPos, 1.0));

                float dist = distance(worldPos, _WorldSpaceCameraPos);
                o.fog = saturate(1.0 - exp(-pow(dist * _FogDensity, 2.0)));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // procedural leaf mask: no texture needed, uv drives a soft blade shape
                float blade = step(i.uv.x, 0.62) * step(0.18, i.uv.x);
                clip(blade - _Cutoff);
                fixed3 c = lerp(i.col, _FogColor.rgb, i.fog);
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }

    FallBack "Transparent/Cutout/Diffuse"
}
