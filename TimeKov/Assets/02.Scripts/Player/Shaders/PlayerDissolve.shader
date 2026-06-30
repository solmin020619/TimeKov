// =====================================================================
// PlayerDissolve.shader  (URP)  — 노이즈 디졸브 + HDR 발광 엣지 + 프레넬 림
// 명일방주:엔드필드 류의 "세련된 소멸" 느낌:
//   - 머리(위)→발(아래) 노이즈 침식으로 깔끔하게 사라짐
//   - 사라지는 경계에 HDR 발광(핫 코어 + 컬러 글로우) → 블룸과 만나면 빛남
//   - 사라질수록 실루엣 외곽(프레넬 림)이 빛나며 소멸
// _DissolveAmount(0→1)을 PlayerDeathFade 가 시간에 따라 올린다.
// 프로퍼티 이름(_BaseMap/_BaseColor)이 URP/Lit 과 같아 셰이더만 바꿔도 텍스처/색 유지.
// * 블룸(Post-process Bloom)이 켜져 있으면 발광이 제대로 살아난다.
// =====================================================================
Shader "TIMEKOV/PlayerDissolve"
{
    Properties
    {
        _BaseMap   ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _MinY ("World Min Y", Float) = 0
        _MaxY ("World Max Y", Float) = 2
        _NoiseScale ("Noise Scale", Float) = 8
        _NoiseAmount ("Noise Amount", Range(0,1)) = 0.5
        _EdgeWidth ("Edge Width", Range(0.001,0.4)) = 0.1
        [HDR] _EdgeColor ("Edge Color (HDR)", Color) = (1,1,1,1)
        _EdgeIntensity ("Edge Intensity", Float) = 3
        _RimIntensity ("Rim Intensity", Float) = 2
        _FresnelPower ("Fresnel Power", Float) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Back

        Pass
        {
            Name "ForwardDissolve"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _DissolveAmount;
                float  _MinY;
                float  _MaxY;
                float  _NoiseScale;
                float  _NoiseAmount;
                float  _EdgeWidth;
                float4 _EdgeColor;
                float  _EdgeIntensity;
                float  _RimIntensity;
                float  _FresnelPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
            };

            float hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            // 2옥타브로 조금 더 디테일한 침식
            float fbm(float2 p)
            {
                return valueNoise(p) * 0.65 + valueNoise(p * 2.03) * 0.35;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.normalWS    = n.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 높이 그라디언트: 위(머리)=0, 아래(발)=1 → 위가 먼저 사라짐
                float heightT = saturate((_MaxY - IN.positionWS.y) / max(_MaxY - _MinY, 1e-4));

                float2 np = IN.positionWS.xz * _NoiseScale + IN.positionWS.y * _NoiseScale * 0.5;
                float n = fbm(np);

                float threshold = lerp(heightT, n, _NoiseAmount);
                float remain = threshold - _DissolveAmount;   // >0 보임
                clip(remain);

                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // 라이팅
                float3 N = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                half ndl = saturate(dot(N, mainLight.direction));
                half3 ambient = SampleSH(N);
                half3 lit = baseCol.rgb * (mainLight.color * ndl + ambient);

                // --- 발광 경계: 컬러 글로우(넓게) + 핫 화이트 코어(아주 얇게) ---
                float glow = saturate(1.0 - remain / max(_EdgeWidth, 1e-4));
                float core = saturate(1.0 - remain / max(_EdgeWidth * 0.25, 1e-4));
                float3 emissive = _EdgeColor.rgb * (_EdgeIntensity * glow * glow);
                emissive += core * core * 4.0;   // 핫 화이트 라인(블룸용)

                // --- 프레넬 림: 사라질수록 외곽이 빛남 ---
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                emissive += _EdgeColor.rgb * (fres * _RimIntensity * saturate(_DissolveAmount));

                return half4(lit + emissive, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
