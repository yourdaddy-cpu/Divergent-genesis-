// Divergent Genesis - terrain / props shader
// Single pass, per-vertex lighting, exponential-squared fog.
// No Unity lighting system, no textures: every colour is baked into vertex
// colours by the mesher (biome tint + face shading + ambient occlusion), which
// is what keeps this fast on a Dimensity 6500.

Shader "DG/Terrain"
{
    Properties
    {
        _SunDir ("Sun Direction", Vector) = (0.35, 0.78, 0.30, 0)
        _SunColor ("Sun Colour", Color) = (1.0, 0.96, 0.88, 1)
        _Ambient ("Ambient Sky", Color) = (0.42, 0.48, 0.58, 1)
        _FogColor ("Fog Colour", Color) = (0.62, 0.74, 0.88, 1)
        _FogDensity ("Fog Density", Float) = 0.0014
        _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 120

        Pass
        {
            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _SunColor;
            fixed4 _Ambient;
            fixed4 _FogColor;
            float4 _SunDir;
            float  _FogDensity;
            float  _AlphaCutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                fixed3 col      : TEXCOORD0;
                float  fog      : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 n = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));

                // per-vertex lighting: cheap and perfectly fine for a voxel world
                float ndl = saturate(dot(n, normalize(_SunDir.xyz)));
                float3 lit = _Ambient.rgb + _SunColor.rgb * ndl * 0.85;

                o.col = v.color.rgb * lit;
                o.pos = UnityWorldToClipPos(float4(worldPos, 1.0));

                float dist = distance(worldPos, _WorldSpaceCameraPos);
                float f = 1.0 - exp(-pow(dist * _FogDensity, 2.0));
                o.fog = saturate(f);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 c = lerp(i.col, _FogColor.rgb, i.fog);
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }

    Fallback "Mobile/Diffuse"
}
