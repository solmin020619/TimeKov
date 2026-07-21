Shader "Custom/UIBackgroundBlur"
{
    Properties
    {
        _MainTex  ("Main Tex (unused)", 2D) = "white" {}
        _Color    ("Tint", Color)           = (0.05, 0.05, 0.10, 0.80)
        _BlurSize ("Blur Radius (px)", Float) = 2.0

        _StencilComp     ("Stencil Comparison", Float) = 8
        _Stencil         ("Stencil ID",         Float) = 0
        _StencilOp       ("Stencil Operation",  Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask",  Float) = 255
        _ColorMask       ("Color Mask",         Float) = 15
    }
    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
            "RenderType"     = "Transparent"
            "IgnoreProjector"= "True"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Blend     SrcAlpha OneMinusSrcAlpha
        ZWrite    Off
        ZTest     Always
        Cull      Off
        ColorMask [_ColorMask]

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                float  _BlurSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;   // ComputeScreenPos 결과
                float4 color      : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                // ComputeScreenPos: DX/GL 플랫폼 Y-flip을 내부적으로 처리
                OUT.screenPos  = ComputeScreenPos(OUT.positionCS);
                OUT.color      = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // perspective divide 후 UV 확보
                // ComputeScreenPos는 _ProjectionParams.x로 DX Y-flip을 내부 처리
                // → 결과 uv.y: DX=0이 상단, GL=1이 상단
                // _CameraOpaqueTexture도 동일 컨벤션이므로 추가 flip 불필요
                float2 uv = IN.screenPos.xy / IN.screenPos.w;

                float2 ts = _CameraOpaqueTexture_TexelSize.xy * _BlurSize;

                half4 col = (half4)0;
                for (int x = -2; x <= 2; x++)
                    for (int y = -2; y <= 2; y++)
                        col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture,
                                               sampler_CameraOpaqueTexture,
                                               uv + float2(x * ts.x, y * ts.y));
                col *= 0.04h;

                col.rgb = lerp(col.rgb, _Color.rgb, 0.55h);
                col.a   = _Color.a * IN.color.a;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
