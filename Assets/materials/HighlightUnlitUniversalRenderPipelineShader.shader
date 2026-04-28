Shader "Custom/InvertSpriteColor"
{
    Properties
    {
        [MainTexture] _MainTex ("Shape Texture", 2D) = "white" {}
        _GlowColor ("Glow Color", Color) = (1, 0, 0.6, 1)
        _Intensity ("Glow Intensity", Range(1, 10)) = 4.0
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
                half4 _GlowColor;
                float _Intensity;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                // Texture is inverted: cloud shape is transparent, background is opaque.
                // Invert so the cloud silhouette glows and the background is invisible.
                half mask = 1.0 - max(tex.a, dot(tex.rgb, half3(0.299, 0.587, 0.114)));
                half4 result;
                result.rgb = _GlowColor.rgb * _Intensity;
                result.a = mask * _GlowColor.a;
                return result;
            }
            ENDHLSL
        }
    }
}
