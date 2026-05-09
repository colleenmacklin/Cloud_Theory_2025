Shader "Custom/Reticule"
{
    Properties
    {
        _GlassColor    ("Glass Tint",         Color)         = (0.75, 0.92, 1.0, 0.12)
        _SpecularColor ("Specular",           Color)         = (1, 1, 1, 1)
        _SpecularSharp ("Specular Sharpness", Range(5, 200)) = 80.0
        _FresnelPower  ("Fresnel Power",      Range(0.5, 5)) = 2.0
        _RimOpacity    ("Rim Opacity",        Range(0, 1))   = 0.55
        _GlowColor     ("Glow Color",         Color)         = (1, 0, 0.6, 1)
        _GlowAmount    ("Glow Amount",        Range(0, 1))   = 0
        _Scale         ("Glow Scale",         Range(1, 3))   = 1.5
        _Opacity       ("Opacity",            Range(0, 1))   = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Overlay+1"
        }

        Blend  SrcAlpha OneMinusSrcAlpha
        ZTest  Off
        ZWrite Off
        Cull   Back

        Pass
        {
            Name "ReticulePass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalVS    : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _GlassColor;
                half4  _SpecularColor;
                float  _SpecularSharp;
                float  _FresnelPower;
                float  _RimOpacity;
                half4  _GlowColor;
                float  _GlowAmount;
                float  _Scale;
                float  _Opacity;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 pos = v.positionOS.xyz * lerp(1.0, _Scale, _GlowAmount);
                o.positionHCS = TransformObjectToHClip(pos);

                // Transform normal into view space so shading never depends on
                // the object's own rotation or world position
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.normalVS = mul((float3x3)UNITY_MATRIX_V, normalWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // View-space sphere normal — always consistent, no UV seam
                float3 N = normalize(i.normalVS);

                // Camera looks along -Z in view space, so toward-camera = +Z
                float3 V = float3(0.0, 0.0, 1.0);

                // Fresnel: 0 at centre (N faces camera directly), 1 at rim
                float NdotV  = saturate(dot(N, V));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);

                // Specular highlight — fixed upper-left in view space, never moves
                float3 L   = normalize(float3(-0.5, 0.7, 1.0));
                float3 R   = reflect(-L, N);
                float  spec = pow(saturate(dot(R, V)), _SpecularSharp);

                // Glass colour tints toward glow colour as _GlowAmount rises
                half3 glassRGB = lerp(_GlassColor.rgb, _GlowColor.rgb, _GlowAmount);

                float baseAlpha = fresnel * _RimOpacity;
                float alpha     = saturate(baseAlpha + spec);

                // Soft centre bloom when glowing
                float innerGlow = _GlowAmount * NdotV * 0.35;
                half3 col = lerp(glassRGB, _GlowColor.rgb, innerGlow);
                alpha = saturate(alpha + innerGlow);

                // Specular overrides colour at the highlight peak
                col = lerp(col, _SpecularColor.rgb, spec);

                return half4(col, alpha * _Opacity);
            }
            ENDHLSL
        }
    }
}
