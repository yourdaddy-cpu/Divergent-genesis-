// Divergent Genesis - HDR post processing
//
// Pass 0  threshold the HDR image down to its bright parts (quarter res)
// Pass 1  separable gaussian, horizontal
// Pass 2  separable gaussian, vertical
// Pass 3  composite: add the bloom back, expose, tone-map, saturate, vignette
//
// The tone map is the important part: without it a plasma bolt and a snowfield
// both clip to white and the world looks flat.

Shader "DG/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
        _Threshold ("Threshold", Float) = 0.86
        _Knee ("Knee", Float) = 0.43
        _Exposure ("Exposure", Float) = 1.08
        _Bloom ("Bloom", Float) = 0.85
        _Saturation ("Saturation", Float) = 1.10
        _Gamma ("Gamma", Float) = 2.2
        _Vignette ("Vignette", Float) = 0.34
        _Underwater ("Underwater", Float) = 0.0
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        // ---------------------------------------------------- 0: bright pass
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBright
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Threshold;
            float _Knee;
            float _Exposure;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 fragBright(v2f i) : SV_Target
            {
                fixed3 c = tex2D(_MainTex, i.uv).rgb * _Exposure;

                float lum = dot(c, float3(0.2126, 0.7152, 0.0722));
                float knee = max(_Knee, 0.0001);
                float soft = saturate((lum - _Threshold + knee) / (2.0 * knee));
                soft = soft * soft * knee;
                float contribution = max(soft, lum - _Threshold) / max(lum, 0.0001);

                return fixed4(c * contribution, 1.0);
            }
            ENDCG
        }

        // ------------------------------------------------------- 1 + 2: blur
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _TexelSize;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 fragBlur(v2f i) : SV_Target
            {
                // 9 tap, weights from a normalised gaussian (sigma ~ 2)
                float2 d = _TexelSize.xy;
                fixed3 sum = tex2D(_MainTex, i.uv).rgb * 0.227027;
                sum += tex2D(_MainTex, i.uv + d * 1.384615).rgb * 0.316216;
                sum += tex2D(_MainTex, i.uv - d * 1.384615).rgb * 0.316216;
                sum += tex2D(_MainTex, i.uv + d * 3.230769).rgb * 0.070270;
                sum += tex2D(_MainTex, i.uv - d * 3.230769).rgb * 0.070270;
                return fixed4(sum, 1.0);
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _TexelSize;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 fragBlur(v2f i) : SV_Target
            {
                float2 d = _TexelSize.xy;
                fixed3 sum = tex2D(_MainTex, i.uv).rgb * 0.227027;
                sum += tex2D(_MainTex, i.uv + d * 1.384615).rgb * 0.316216;
                sum += tex2D(_MainTex, i.uv - d * 1.384615).rgb * 0.316216;
                sum += tex2D(_MainTex, i.uv + d * 3.230769).rgb * 0.070270;
                sum += tex2D(_MainTex, i.uv - d * 3.230769).rgb * 0.070270;
                return fixed4(sum, 1.0);
            }
            ENDCG
        }

        // ---------------------------------------------------- 3: composite
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BloomTex;
            float _Exposure;
            float _Bloom;
            float _Saturation;
            float _Gamma;
            float _Vignette;
            float _Underwater;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 fragComposite(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                fixed3 scene = tex2D(_MainTex, uv).rgb * _Exposure;
                fixed3 bloom = tex2D(_BloomTex, uv).rgb;
                fixed3 c = scene + bloom * _Bloom;

                // underwater: a green-blue wobble and dimming
                if (_Underwater > 0.001)
                {
                    float2 wob = float2(sin(uv.y * 42.0 + _Time.y * 2.2),
                                        cos(uv.x * 38.0 + _Time.y * 1.7)) * 0.0022 * _Underwater;
                    fixed3 warped = tex2D(_MainTex, uv + wob).rgb * _Exposure;
                    c = lerp(c, warped * fixed3(0.42, 0.72, 0.92) + bloom * _Bloom * 0.5, _Underwater);
                }

                // ACES-ish filmic curve, then gamma
                c = (c * (2.51 * c + 0.03)) / (c * (2.43 * c + 0.59) + 0.14);
                c = pow(saturate(c), 1.0 / _Gamma);

                // saturation around luminance
                float lum = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = lerp(lum.xxx, c, _Saturation);

                // vignette, driven from a slightly squashed centred radius
                float2 q = uv - 0.5;
                float v = 1.0 - dot(q, q) * 1.9 * _Vignette;
                c *= saturate(v);

                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}