Shader "Custom/TreeFogPlane"
{
    // Transparent fog plane with a vertical gradient for URP.
    // Place on a Quad in front of the tree line.
    // UV.y = 0 at the bottom of the quad (full fog), UV.y = 1 at the top (transparent).

    Properties
    {
        _FogColor   ("Fog Color",        Color)  = (0.56, 0.64, 0.69, 1.0)
        _BottomAlpha("Bottom Alpha",     Range(0, 1)) = 0.45
        _TopAlpha   ("Top Alpha",        Range(0, 1)) = 0.0
        _Curve      ("Gradient Curve",   Range(0.25, 4)) = 1.5
        // Higher Curve = fog stays thin longer then builds up sharply at the bottom.
        // Lower Curve (< 1) = fog starts immediately and fades more gradually.
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "TreeFogPlanePass"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off   // visible from both sides so rotation doesn't matter

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                half  _BottomAlpha;
                half  _TopAlpha;
                half  _Curve;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // UV.y = 0 at bottom, 1 at top on a standard Unity Quad.
                // We want maximum fog at the bottom, transparent at the top.
                float t = 1.0 - IN.uv.y;             // 0 = top, 1 = bottom
                t = pow(saturate(t), _Curve);         // apply gradient curve

                half alpha = lerp(_TopAlpha, _BottomAlpha, t);

                return half4(_FogColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
