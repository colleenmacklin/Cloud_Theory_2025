Shader "Custom/InvertSpriteColor"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,0.5) // Change initial alpha to 0.5 for semi-transparency
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"


            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Get the original color from the texture
                fixed4 originalCol = tex2D(_MainTex, i.uv);

                // Invert the RGB channels: (1.0 - red, 1.0 - green, 1.0 - blue)
                // This formula correctly inverts the colors.
                fixed4 invertedCol;
                invertedCol.rgb = 1.0 - originalCol.rgb;

                // Apply the alpha from the material's _Color property
                // This allows you to control the overall transparency in the Inspector.
                invertedCol.a = _Color.a;

                // To combine the inverted colors with the sprite's original transparency,
                // and correctly handle the new material transparency, multiply them together.
                // This preserves the transparent parts of the original sprite.
                invertedCol *= originalCol.a;

                // Set the final output color, applying the tint from the material if desired
                return invertedCol * _Color;
            }
            ENDCG
        }
    }
}