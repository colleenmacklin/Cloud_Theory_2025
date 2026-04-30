Shader "Custom/InvertSpriteColor"
{
    Properties
    {
        [MainTexture] _MainTex ("Shape Texture", 2D) = "white" {}
        _GlowColor ("Glow Color", Color) = (1, 0, 0.6, 1)
        _Intensity ("Glow Intensity", Range(1, 10)) = 4.0
        _OutlineWidth ("Outline Width (px)", Range(1, 20)) = 3.0
        _Threshold ("Cloud Threshold", Range(0.01, 1.0)) = 0.5
        [Toggle] _FillMode ("Fill Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Overlay"
        }

        // Additive blending: bright colors add on top of whatever is underneath
        Blend SrcAlpha One
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
                float _FillMode;
            CBUFFER_END

            // Shapes are black silhouettes on transparent backgrounds.
            // Returns 1 where shape is (opaque), 0 where background is (transparent).
            half ShapePresence(float2 uv)
            {
                half4 s = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                return step(_Threshold, s.a);
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

                half4 result;
                result.rgb = _GlowColor.rgb * _Intensity;
                result.a = mask * _GlowColor.a;
                return result;
            }
            ENDHLSL
        }
    }
}
