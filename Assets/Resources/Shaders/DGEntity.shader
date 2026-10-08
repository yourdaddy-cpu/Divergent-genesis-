// Divergent Genesis - creatures and held items
//
// Same single-pass, per-vertex lighting model as the terrain (so creatures sit in
// the world instead of looking pasted on), plus two extras:
//
//  * the vertex colour's ALPHA channel is an emissive mask. The rig factory paints
//    eyes, lamp glass, dragon throats and glow sacs with alpha > 0, and those parts
//    push past 1.0 so the HDR post pass blooms them.
//  * a soft rim term that lifts the silhouette of a creature against the ground,
//    which is what makes a mob readable at a distance.

Shader "DG/Entity"
{
    Properties
    {
        _SunDir ("Sun Direction", Vector) = (0.35, 0.78, 0.30, 0)
        _SunColor ("Sun Colour", Color) = (1.0, 0.96, 0.88, 1)
        _Ambient ("Ambient Sky", Color) = (0.42, 0.48, 0.58, 1)
        _FogColor ("Fog Colour", Color) = (0.62, 0.74, 0.88, 1)
        _FogDensity ("Fog Density", Float) = 0.0014
        _Emissive ("Emissive Boost", Range(0,6)) = 2.4
        _Rim ("Rim Light", Range(0,2)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 150
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
            float _FogDensity;
            float _Emissive;
            float _Rim;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed3 col : TEXCOORD0;
                fixed3 emis : TEXCOORD1;
                float  fog : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 n = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);

                float ndl = saturate(dot(n, normalize(_SunDir.xyz)));
                float3 lit = _Ambient.rgb + _SunColor.rgb * ndl * 0.85;

                // rim: strongest where the surface turns away from the camera
                float rim = pow(1.0 - saturate(dot(n, viewDir)), 3.0) * _Rim;

                o.col = v.color.rgb * lit + _SunColor.rgb * rim;
                o.emis = v.color.rgb * v.color.a * _Emissive;
                o.pos = UnityWorldToClipPos(float4(worldPos, 1.0));

                float dist = distance(worldPos, _WorldSpaceCameraPos);
                o.fog = saturate(1.0 - exp(-pow(dist * _FogDensity, 2.0)));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 c = i.col + i.emis;
                c = lerp(c, _FogColor.rgb, i.fog);
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }

    FallBack "Mobile/Diffuse"
}