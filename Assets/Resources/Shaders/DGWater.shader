// Divergent Genesis - water surface
// Transparent, animated, vertex coloured by depth. ZWrite off so overlapping
// water quads from neighbouring LOD tiles never fight.

Shader "DG/Water"
{
    Properties
    {
        _SunDir ("Sun Direction", Vector) = (0.35, 0.78, 0.30, 0)
        _SunColor ("Sun Colour", Color) = (1.0, 0.96, 0.88, 1)
        _Ambient ("Ambient Sky", Color) = (0.42, 0.48, 0.58, 1)
        _SkyColor ("Sky Reflection", Color) = (0.55, 0.72, 0.95, 1)
        _FogColor ("Fog Colour", Color) = (0.62, 0.74, 0.88, 1)
        _FogDensity ("Fog Density", Float) = 0.0014
        _Opacity ("Opacity", Range(0,1)) = 0.82
        _WaveScale ("Wave Scale", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        LOD 100

        Pass
        {
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _SunColor;
            fixed4 _Ambient;
            fixed4 _SkyColor;
            fixed4 _FogColor;
            float4 _SunDir;
            float  _FogDensity;
            float  _Opacity;
            float  _WaveScale;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed3 col : TEXCOORD0;
                float  fog : TEXCOORD1;
                float3 wpos: TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                // gentle swell - two crossed sine waves, no texture
                float t = _Time.y * 0.9;
                worldPos.y += (sin(worldPos.x * 0.35 + t) + sin(worldPos.z * 0.29 - t * 0.8)) * 0.045 * _WaveScale;

                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                float3 n = normalize(float3(
                    sin(worldPos.x * 0.35 + t) * 0.10,
                    1.0,
                    sin(worldPos.z * 0.29 - t * 0.8) * 0.10));

                float fres = pow(1.0 - saturate(dot(n, viewDir)), 3.0);
                float ndl = saturate(dot(n, normalize(_SunDir.xyz)));
                float3 lit = _Ambient.rgb + _SunColor.rgb * ndl * 0.5;
                float3 col = v.color.rgb * lit;
                col = lerp(col, _SkyColor.rgb, fres * 0.65);
                col += _SunColor.rgb * pow(saturate(dot(reflect(-viewDir, n), normalize(_SunDir.xyz))), 48.0) * 0.5;

                o.col = col;
                o.wpos = worldPos;
                o.pos = UnityWorldToClipPos(float4(worldPos, 1.0));

                float dist = distance(worldPos, _WorldSpaceCameraPos);
                o.fog = saturate(1.0 - exp(-pow(dist * _FogDensity, 2.0)));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 c = lerp(i.col, _FogColor.rgb, i.fog);
                return fixed4(c, _Opacity);
            }
            ENDCG
        }
    }

    FallBack "Mobile/Diffuse"
}
