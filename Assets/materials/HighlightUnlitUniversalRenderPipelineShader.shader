Shader "Custom/InvertSpriteColor"
{
    Properties
    {
        [MainTexture] _MainTex ("Shape Texture", 2D) = "white" {}
        _GlowColor ("Glow Color", Color) = (1, 0, 0.6, 1)
        _Intensity ("Glow Intensity", Range(0, 1)) = 0.8
        _OutlineWidth ("Outline Width (px)", Range(1, 20)) = 3.0
        _Threshold ("Cloud Threshold", Range(0.01, 1.0)) = 0.5
        _Feather ("Edge Feather", Range(0.0, 0.5)) = 0.1
        [Toggle] _FillMode ("Fill Mode", Float) = 0
        [Toggle] _CycleRainbow ("Cycle Rainbow", Float) = 1
        _RainbowIntensity ("Rainbow Intensity", Range(0, 1)) = 0.2
        _RainbowCycleSpeed ("Rainbow Cycle Speed", Range(0.1, 5)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Overlay"
        }

        // Alpha blending: highlight composites over the cloud as an opaque colour
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "CloudHighlightGlow"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4 _GlowColor;
                float _Intensity;
                float _OutlineWidth;
                float _Threshold;
                float _Feather;
                float _FillMode;
                float _CycleRainbow;
                float _RainbowIntensity;
                float _RainbowCycleSpeed;
            CBUFFER_END

            // Returns soft 0-1 presence value — smoothstep creates a gradient at the shape boundary.
            half ShapePresence(float2 uv)
            {
                half4 s = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                return smoothstep(max(0.0, _Threshold - _Feather), min(1.0, _Threshold + _Feather), s.a);
            }

            // hue in [0,1) -> RGB, fully saturated and at full value
            half3 HueToRGB(half hue)
            {
                half r = abs(hue * 6.0 - 3.0) - 1.0;
                half g = 2.0 - abs(hue * 6.0 - 2.0);
                half b = 2.0 - abs(hue * 6.0 - 4.0);
                return saturate(half3(r, g, b));
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half current = ShapePresence(i.uv);

                // Sample 8 neighbors for outline edge detection
                float2 ts = _MainTex_TexelSize.xy * _OutlineWidth;
                half maxNearby = 0;
                maxNearby = max(maxNearby, ShapePresence(i.uv + float2( ts.x,    0)));
                maxNearby = max(maxNearby, ShapePresence(i.uv + float2(-ts.x,    0)));
                maxNearby = max(maxNearby, ShapePresence(i.uv + float2(    0,  ts.y)));
                maxNearby = max(maxNearby, ShapePresence(i.uv + float2(    0, -ts.y)));
                maxNearby = max(maxNearby, ShapePresence(i.uv + float2( ts.x,  ts.y)));
                maxNearby = max(maxNearby, ShapePresence(i.uv + float2(-ts.x,  ts.y)));
                maxNearby = max(maxNearby, ShapePresence(i.uv + float2( ts.x, -ts.y)));
                maxNearby = max(maxNearby, ShapePresence(i.uv + float2(-ts.x, -ts.y)));

                // Outline: glow only at edges. Fill: glow the whole shape.
                half outlineMask = (1.0 - current) * maxNearby;
                half mask = lerp(outlineMask, current, _FillMode);

                // Subtle cycling rainbow tint — same low-intensity blend used on the eye pupil,
                // mixed into the glow color rather than replacing it outright.
                half hue = frac(_Time.y * _RainbowCycleSpeed);
                half3 rainbow = HueToRGB(hue);
                half3 tintedGlow = lerp(_GlowColor.rgb, rainbow, _RainbowIntensity * _CycleRainbow);

                half4 result;
                result.rgb = tintedGlow;
                result.a = mask * _GlowColor.a;
                return result;
            }
            ENDHLSL
        }
    }
}
