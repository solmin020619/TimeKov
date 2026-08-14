// =====================================================================
// PlayerDissolve.shader  (URP)  — 노이즈 디졸브 + HDR 발광 엣지 + 프레넬 림
// 명일방주:엔드필드 류의 "세련된 소멸" 느낌:
//   - 머리(위)→발(아래) 노이즈 침식으로 깔끔하게 사라짐
//   - 사라지는 경계에 HDR 발광(핫 코어 + 컬러 글로우) → 블룸과 만나면 빛남
//   - 사라질수록 실루엣 외곽(프레넬 림)이 빛나며 소멸
// _DissolveAmount(0→1)을 PlayerDeathFade / ModelDissolve 가 시간에 따라 올린다.
//
// ★조명은 URP/Lit 과 같은 PBR 경로(UniversalFragmentPBR)를 쓴다.
//   예전엔 베이스 컬러 + 단순 램버트만 계산해서, 노멀맵·메탈릭을 쓰는 오브젝트
//   (금속 우주선 등)가 디졸브 중에는 밋밋하게 보이다가 원본 머티리얼로 복구되는 순간
//   재질이 확 달라 보였다. 프로퍼티 이름도 URP/Lit 과 같게 맞춰 셰이더만 바꿔도
//   텍스처·노멀·메탈릭이 그대로 물린다.
// * 블룸(Post-process Bloom)이 켜져 있으면 발광이 제대로 살아난다.
// =====================================================================
Shader "TIMEKOV/PlayerDissolve"
{
    Properties
    {
        _BaseMap   ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        // URP/Lit 과 같은 이름 — 원본 머티리얼의 맵이 그대로 물린다.
        _BumpMap   ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _MetallicGlossMap ("Metallic Map", 2D) = "white" {}
        _Metallic   ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0,1)) = 1

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
            #pragma target 3.0

            // URP/Lit 과 같은 조명 결과를 내려면 이 키워드들이 필요하다(그림자·추가광원·안개).
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            // 추가 광원 클러스터 경로(URP 17 = _CLUSTER_LIGHT_LOOP, 이전 버전 = _FORWARD_PLUS).
            //   해당 버전에 없는 키워드는 그냥 무시되므로 둘 다 걸어 둔다.
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);          SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);          SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_OcclusionMap);     SAMPLER(sampler_OcclusionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _BumpScale;
                float  _Metallic;
                float  _Smoothness;
                float  _OcclusionStrength;
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
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 tangentWS   : TEXCOORD3;   // xyz = 접선, w = 종속접선 부호
                float  fogCoord    : TEXCOORD4;
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
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.normalWS    = n.normalWS;
                OUT.tangentWS   = float4(n.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogCoord    = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ── 디졸브 침식 ────────────────────────────────────────
                // 높이 그라디언트: 위(머리)=0, 아래(발)=1 → 위가 먼저 사라짐
                float heightT = saturate((_MaxY - IN.positionWS.y) / max(_MaxY - _MinY, 1e-4));

                float2 np = IN.positionWS.xz * _NoiseScale + IN.positionWS.y * _NoiseScale * 0.5;
                float n = fbm(np);

                float threshold = lerp(heightT, n, _NoiseAmount);
                float remain = threshold - _DissolveAmount;   // >0 보임
                clip(remain);

                // ── 표면 값 (URP/Lit 과 같은 맵 구성) ──────────────────
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                half4 mg      = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, IN.uv);
                half  occ     = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, IN.uv).g;
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv), _BumpScale);

                // ── 발광 경계: 컬러 글로우(넓게) + 핫 화이트 코어(아주 얇게) ---
                float glow = saturate(1.0 - remain / max(_EdgeWidth, 1e-4));
                float core = saturate(1.0 - remain / max(_EdgeWidth * 0.25, 1e-4));
                float3 emissive = _EdgeColor.rgb * (_EdgeIntensity * glow * glow);
                emissive += core * core * 4.0;   // 핫 화이트 라인(블룸용)

                // 접선공간 → 월드 노멀
                float sgn = IN.tangentWS.w;
                float3 bitangent = sgn * cross(IN.normalWS.xyz, IN.tangentWS.xyz);
                half3x3 tbn = half3x3(IN.tangentWS.xyz, bitangent, IN.normalWS.xyz);
                float3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tbn));

                // --- 프레넬 림: 사라질수록 외곽이 빛남 ---
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float fres = pow(1.0 - saturate(dot(normalWS, V)), _FresnelPower);
                emissive += _EdgeColor.rgb * (fres * _RimIntensity * saturate(_DissolveAmount));

                // ── URP 표준 PBR 조명 ─────────────────────────────────
                SurfaceData surface = (SurfaceData)0;
                surface.albedo     = baseCol.rgb;
                surface.metallic   = mg.r * _Metallic;
                surface.smoothness = mg.a * _Smoothness;
                surface.occlusion  = LerpWhiteTo(occ, _OcclusionStrength);
                surface.normalTS   = normalTS;
                surface.emission   = emissive;
                surface.alpha      = 1.0;

                InputData inputData = (InputData)0;
                inputData.positionWS      = IN.positionWS;
                inputData.normalWS        = normalWS;
                inputData.viewDirectionWS = V;
                inputData.shadowCoord     = TransformWorldToShadowCoord(IN.positionWS);
                inputData.bakedGI         = SampleSH(normalWS);
                inputData.fogCoord        = IN.fogCoord;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, IN.fogCoord);
                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
