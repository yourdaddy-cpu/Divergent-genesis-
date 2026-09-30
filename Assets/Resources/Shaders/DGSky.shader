// Divergent Genesis - sky dome
// Vertical gradient + sun disc and glow. One fullscreen-ish pass, no texture.

Shader "DG/Sky"
{
    Properties
    {
        _Zenith ("Zenith Colour", Color) = (0.24, 0.48, 0.86, 1)
        _Horizon ("Horizon Colour", Color) = (0.70, 0.84, 0.97, 1)
        _SunColor ("Sun Colour", Color) = (1.0, 0.97, 0.90, 1)
        _SunDir ("Sun Direction", Vector) = (0.35, 0.78, 0.30, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Zenith;
            fixed4 _Horizon;
            fixed4 _SunColor;
            float4 _SunDir;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.dir = v.vertex.xyz;
                // strip translation so the dome never moves relative to the camera
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityWorldToClipPos(float4(wp.xyz, 1.0));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float t = saturate(dir.y * 1.25);

                fixed3 col = lerp(_Horizon.rgb, _Zenith.rgb, pow(t, 0.65));

                float3 sun = normalize(_SunDir.xyz);
                float sd = saturate(dot(dir, sun));

                // broad atmospheric glow
                col += _SunColor.rgb * pow(sd, 6.0) * 0.32;
                // the disc itself
                col += _SunColor.rgb * smoothstep(0.9975, 0.9992, sd) * 2.4;

                // warm band right on the horizon
                col = lerp(col, _SunColor.rgb * 0.95, smoothstep(0.12, 0.0, abs(dir.y)) * 0.18);

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
