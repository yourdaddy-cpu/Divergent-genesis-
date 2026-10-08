// Divergent Genesis - sky dome
//
// A vertical gradient with a sun disc and glow, plus the ritual: when the Node
// Sovereign is summoned the palette is driven to purple from the C# side and
// _Cracks opens a branching, glowing, slowly drifting fracture network overhead.
// The cracks are pure procedural noise, so nothing has to be authored and two
// rituals never look the same (the caller re-rolls _CrackSeed each time).

Shader "DG/Sky"
{
    Properties
    {
        _Zenith ("Zenith Colour", Color) = (0.24, 0.48, 0.86, 1)
        _Horizon ("Horizon Colour", Color) = (0.70, 0.84, 0.97, 1)
        _SunColor ("Sun Colour", Color) = (1.0, 0.97, 0.90, 1)
        _SunDir ("Sun Direction", Vector) = (0.35, 0.78, 0.30, 0)

        _Corrupt ("Corruption", Range(0,1)) = 0
        _Cracks ("Crack Amount", Range(0,1.6)) = 0
        _CrackColor ("Crack Colour", Color) = (0.86, 0.36, 1.0, 1)
        _CrackSeed ("Crack Seed", Float) = 11.3
        _Flare ("Flare", Range(0,2)) = 0
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
            fixed4 _CrackColor;
            float _Corrupt;
            float _Cracks;
            float _CrackSeed;
            float _Flare;

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

            // ---- cheap 3D value noise, enough for a fracture field
            float hash31(float3 p)
            {
                p = frac(p * 0.3183099 + float3(0.71, 0.113, 0.419));
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(lerp(hash31(i + float3(0, 0, 0)), hash31(i + float3(1, 0, 0)), f.x),
                         lerp(hash31(i + float3(0, 1, 0)), hash31(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(hash31(i + float3(0, 0, 1)), hash31(i + float3(1, 0, 1)), f.x),
                         lerp(hash31(i + float3(0, 1, 1)), hash31(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float fbm(float3 p)
            {
                float a = 0.5, s = 0.0;
                for (int k = 0; k < 4; k++)
                {
                    s += a * noise3(p);
                    p *= 2.03;
                    a *= 0.5;
                }
                return s;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float t = saturate(dir.y * 1.25);

                fixed3 col = lerp(_Horizon.rgb, _Zenith.rgb, pow(t, 0.65));

                // ---- sun
                float3 sun = normalize(_SunDir.xyz);
                float sd = saturate(dot(dir, sun));
                col += _SunColor.rgb * pow(sd, 6.0) * 0.32;
                col += _SunColor.rgb * smoothstep(0.9975, 0.9992, sd) * 2.4;
                col = lerp(col, _SunColor.rgb * 0.95, smoothstep(0.12, 0.0, abs(dir.y)) * 0.18);

                // ---- the tear -------------------------------------------------
                if (_Cracks > 0.001)
                {
                    float3 p = dir * 4.6 + _CrackSeed;
                    p.y *= 0.62;                          // stretch the cells vertically
                    p += float3(0.0, _Time.y * 0.012, _Time.y * 0.008);

                    float n = fbm(p);
                    float ridge = 1.0 - abs(2.0 * n - 1.0);

                    // a second, finer field breaks the main lines into branches
                    float n2 = fbm(p * 2.9 + 31.7);
                    float ridge2 = 1.0 - abs(2.0 * n2 - 1.0);

                    float main = smoothstep(0.72, 0.985, ridge);
                    float branch = smoothstep(0.86, 1.0, ridge2) * 0.55;
                    float crack = saturate((main + branch * main) * _Cracks);

                    // overhead only, fading out as it approaches the horizon
                    float mask = smoothstep(0.02, 0.42, dir.y) * (1.0 - _Corrupt * 0.15);
                    crack *= mask;

                    // the seams glow violet, and pulse gently as if something breathes
                    float pulse = 0.75 + 0.45 * sin(_Time.y * 1.6 + n * 9.0);
                    col += _CrackColor.rgb * crack * pulse * (1.25 + _Flare);
                    col *= 1.0 - crack * 0.45;            // and they swallow light around them
                }

                // ---- raw corruption haze, even before the cracks open
                if (_Corrupt > 0.001)
                {
                    float h = fbm(dir * 3.1 + _CrackSeed * 0.37 + _Time.y * 0.02);
                    col = lerp(col, _CrackColor.rgb * (0.18 + 0.30 * h), _Corrupt * 0.35);
                    col *= 1.0 + _Corrupt * 0.12;
                }

                col *= 1.0 + _Flare * 0.6;
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}