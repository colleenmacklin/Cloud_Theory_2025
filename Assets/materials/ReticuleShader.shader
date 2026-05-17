Shader "Custom/Reticule"
{
    Properties
    {
        _GlassColor         ("Glass Tint",          Color)          = (0.75, 0.92, 1.0, 0.12)
        _SpecularColor      ("Specular",            Color)          = (1, 1, 1, 1)
        _SpecularSharp      ("Specular Sharpness",  Range(5, 200))  = 80.0
        _FresnelPower       ("Fresnel Power",       Range(0.5, 5))  = 2.0
        _RimOpacity         ("Rim Opacity",         Range(0, 1))    = 0.55
        _RefractionStrength ("Refraction Strength", Range(0, 0.1))  = 0.03
        _RippleFrequency    ("Ripple Frequency",    Range(1, 20))   = 8.0
        _RippleSpeed        ("Ripple Speed",        Range(0, 10))   = 3.0
        _RippleStrength     ("Ripple Strength",     Range(0, 0.05)) = 0.015
        _GlowColor          ("Glow Color",          Color)          = (1, 0, 0.6, 1)
        _GlowAmount         ("Glow Amount",         Range(0, 1))    = 0
        _Scale              ("Glow Scale",          Range(1, 3))    = 1.5
        _Opacity            ("Opacity",             Range(0, 1))    = 1
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

            // Declared outside CBUFFER — textures are not constant-buffer data
            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalVS    : TEXCOORD0;
                float4 screenPos   : TEXCOORD1;  // perspective-correct screen UV
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _GlassColor;
                half4  _SpecularColor;
                float  _SpecularSharp;
                float  _FresnelPower;
                float  _RimOpacity;
                float  _RefractionStrength;
                float  _RippleFrequency;
                float  _RippleSpeed;
                float  _RippleStrength;
                half4  _GlowColor;
                float  _GlowAmount;
                float  _Scale;
                float  _Opacity;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 pos    = v.positionOS.xyz * lerp(1.0, _Scale, _GlowAmount);
                o.positionHCS = TransformObjectToHClip(pos);
                o.screenPos   = ComputeScreenPos(o.positionHCS);  // handles platform UV flip

                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.normalVS      = mul((float3x3)UNITY_MATRIX_V, normalWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalVS);
                float3 V = float3(0.0, 0.0, 1.0);

                float NdotV  = saturate(dot(N, V));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);

                // Radial distance from ball centre (0 = centre, 1 = rim)
                float  r         = length(N.xy);
                float2 radialDir = r > 0.001 ? N.xy / r : float2(0.0, 0.0);

                // --- Ripple ---
                // Concentric sine waves grow outward from centre, active only when glowing
                float rippleMag = 0.0;
                if (_GlowAmount > 0.0)
                {
                    float phase    = r * _RippleFrequency - _Time.y * _RippleSpeed;
                    float envelope = smoothstep(0.0, 0.25, r) * smoothstep(1.0, 0.55, r);
                    rippleMag      = sin(phase) * envelope * _GlowAmount * _RippleStrength;
                }

                // --- Refraction ---
                // Shift the screen UV by the sphere normal XY (lens distortion) plus
                // the radial ripple offset so the refracted background shimmers in rings
                float2 screenUV  = i.screenPos.xy / i.screenPos.w;
                float2 refractUV = screenUV + N.xy * _RefractionStrength + radialDir * rippleMag;
                half3  refractBg = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, refractUV).rgb;

                // --- Glass surface ---
                float3 L    = normalize(float3(-0.5, 0.7, 1.0));
                float3 R    = reflect(-L, N);
                float  spec = pow(saturate(dot(R, V)), _SpecularSharp);

                half3 glassRGB   = lerp(_GlassColor.rgb, _GlowColor.rgb, _GlowAmount);
                float glassAlpha = saturate(fresnel * _RimOpacity + spec);

                float innerGlow = _GlowAmount * NdotV * 0.35;
                half3 col       = lerp(glassRGB, _GlowColor.rgb, innerGlow);
                glassAlpha      = saturate(glassAlpha + innerGlow);
                col             = lerp(col, _SpecularColor.rgb, spec);

                // Composite glass layer over refracted background
                half3 finalColor = lerp(refractBg, col, glassAlpha);

                float edgeFade = smoothstep(0.0, 0.8, NdotV);
                return half4(finalColor, edgeFade * _Opacity);
            }
            ENDHLSL
        }
    }
}
