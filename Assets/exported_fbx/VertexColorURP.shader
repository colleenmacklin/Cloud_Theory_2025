Shader "Custom/VertexColorURP"
{
    Properties
    {
        _Smoothness ("Smoothness", Range(0,1)) = 0.0

        // Subtle rim glow so animals read better against dark/moonlit backgrounds
        // without changing the existing diffuse + ambient lighting response.
        _GlowColor ("Glow Color", Color) = (0.55, 0.65, 1.0, 1)
        _GlowIntensity ("Glow Intensity", Range(0,2)) = 0.3
        _GlowPower ("Glow Power (edge falloff)", Range(0.5,8)) = 3.0

        // Emission: makes the whole surface glow in its own vertex colors,
        // independent of scene lighting. Values > 1 will bloom if Bloom is enabled.
        [HDR] _EmissiveTint ("Emission Tint", Color) = (1, 1, 1, 1)
        _EmissionStrength ("Emission Strength", Range(0,10)) = 0.0
        _EmissionTintMix ("Emission Tint Mix (0 = vertex color, 1 = tint only)", Range(0,1)) = 0.0
        _EmissionDarkOnly ("Glow Only In Dark (0 = always, 1 = only unlit areas)", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Basic URP light keywords so the model reacts to scene lighting
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 _GlowColor;
            half _GlowIntensity;
            half _GlowPower;
            half4 _EmissiveTint;
            half _EmissionStrength;
            half _EmissionTintMix;
            half _EmissionDarkOnly;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float4 color       : COLOR;
                float fogCoord     : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs vni = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = vpi.positionCS;
                OUT.normalWS = vni.normalWS;
                OUT.color = IN.color;
                OUT.fogCoord = ComputeFogFactor(vpi.positionCS.z);
                OUT.positionWS = vpi.positionWS;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();
                half3 normal = normalize(IN.normalWS);
                half ndotl = saturate(dot(normal, mainLight.direction));
                half3 lighting = mainLight.color * ndotl + unity_AmbientSky.rgb;
                half3 albedo = IN.color.rgb;
                half3 result = albedo * lighting;

                // Subtle rim glow at grazing angles, independent of scene light intensity,
                // so silhouettes stay readable in dark/moonlit areas without touching the
                // lighting computed above.
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half fresnel = pow(1.0 - saturate(dot(normal, viewDirWS)), _GlowPower);
                half3 glow = _GlowColor.rgb * _GlowIntensity * fresnel;
                result += glow;

                // Emission: vertex color (optionally blended toward the tint) scaled by strength.
                // _EmissionDarkOnly fades the emission out where the surface is already lit,
                // so it reads as "glow in the dark".
                half3 emissionBase = lerp(albedo, half3(1, 1, 1), _EmissionTintMix) * _EmissiveTint.rgb;
                half brightness = saturate(dot(lighting, half3(0.299, 0.587, 0.114)));
                half darkMask = lerp(1.0, 1.0 - brightness, _EmissionDarkOnly);
                result += emissionBase * _EmissionStrength * darkMask;

                result = MixFog(result, IN.fogCoord);
                return half4(result, IN.color.a);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
