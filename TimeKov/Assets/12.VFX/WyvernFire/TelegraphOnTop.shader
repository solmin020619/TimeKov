// 지면 범위 텔레그래프 전용. additive + ZTest Always 라 경사/지형에 파묻혀도 항상 위에 그려져 어디서든 보인다.
// 파티클 색(COLOR 스트림)으로 틴트+펄스. URP 네이티브.
Shader "Wyvern/TelegraphOnTop"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [HDR] _BaseColor ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+200"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }
        Blend One One        // additive
        ZWrite Off
        ZTest Always         // 지형/경사 뒤여도 항상 위에 = 어디서든 보임
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half a = tex.a * _BaseColor.a * IN.color.a;            // 링 마스크 x 펄스
                half3 rgb = _BaseColor.rgb * IN.color.rgb;             // 틴트 x 파티클색
                return half4(rgb * a, a);                              // premultiplied additive(알파로 밝기=펄스)
            }
            ENDHLSL
        }
    }
}
