Shader "Custom/UIBackgroundBlur"
{
    Properties
    {
        _Color    ("Tint", Color)               = (0.05, 0.05, 0.10, 0.80)
        _BlurSize ("Blur Radius (px)", Float)   = 2.5
    }
    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
            "RenderType"     = "Transparent"
            "IgnoreProjector"= "True"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest  Always
        Cull   Off

        Pass
        {
            Name "UIBackgroundBlur"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);
            float4 _CameraOpaqueTexture_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _BlurSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color      = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // SV_POSITION.xy = screen pixel coords (0..Screen.width, 0..Screen.height)
                float2 uv = IN.positionCS.xy * _CameraOpaqueTexture_TexelSize.xy;

                // DX platforms have Y-origin at top; flip to match _CameraOpaqueTexture
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y;
                #endif

                float2 ts = _CameraOpaqueTexture_TexelSize.xy * _BlurSize;

                // 5x5 box blur (25 samples)
                half4 col = (half4)0;
                for (int x = -2; x <= 2; x++)
                    for (int y = -2; y <= 2; y++)
                        col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture,
                                               sampler_CameraOpaqueTexture,
                                               uv + float2(x * ts.x, y * ts.y));
                col *= 0.04h; // /25

                // Dark tint over blurred scene
                col.rgb = lerp(col.rgb, _Color.rgb, 0.55h);
                // CanvasGroup.alpha is passed via vertex color alpha
                col.a   = _Color.a * IN.color.a;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
