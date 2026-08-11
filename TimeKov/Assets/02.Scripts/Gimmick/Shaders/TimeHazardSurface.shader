// =====================================================================
// TimeHazardSurface.shader  (URP)  — 시간 급속감소 구역 건물 표면 일렁임
// 건물 겉면에 얇게 덧씌우는 "위험 표식".
//
// ★일렁임의 형태는 화면 효과(TimeHazardScreenFx)와 같은 수식이다 — 둘이 같은 결로 보이게:
//     · 3옥타브 fbm 값노이즈 (가중치 0.5 / 0.3 / 0.2, 주파수 1 / 2 / 4)
//     · 대비 곡선  saturate((n - 0.35) / 0.4) 뒤 smoothstep
//     · 두 겹을 서로 다른 방향·속도로 흘리고(B는 1.3배 크기, 0.7 세기) 겹쳐 간섭
//     · uv 크기를 sin/cos 로 흔들어 왜곡(_WarpAmount)
//   화면은 2D 라 그대로 흘리면 되지만 건물은 3D 면이라 삼중평면(triplanar)으로 투영해
//   벽·지붕 어느 면에서도 무늬가 늘어지지 않고 감긴다.
//
// ★실내 처리: '어느 면이 바깥인가'를 기하로 가려내려던 시도(법선/경계 판정)는 전부 걷어냈다.
//   다층 건물에서 번번이 틀렸고(중심보다 아래의 천장이 '바깥면'으로 통과), 무엇보다
//   외벽까지 같이 잘려 정면이 텅 비는 부작용이 컸다.
//   대신 '플레이어가 건물 안이면 껍질을 통째로 끈다' — TimeHazardSurfaceFx 가 처리한다.
//   (셰이더에서 카메라 위치로 판정하던 방식은 3인칭 카메라가 플레이어보다 뒤·위에 있어
//    안에 들어가도 카메라는 아직 밖이라 안 꺼지는 문제가 있었다)
//
// 원본 렌더러는 건드리지 않고, 같은 메시를 덮어씌운 '껍질(skin)'에만 쓴다.
// 겹침 z-파이팅은 _Outset 으로 띄워 방지. 가산 합성이라 건물 색을 어둡게 만들지 않는다.
// =====================================================================
Shader "TIMEKOV/TimeHazardSurface"
{
    Properties
    {
        [HDR] _Color ("Color (HDR)", Color) = (0.15, 0.45, 0.85, 1)
        _Alpha ("Alpha", Range(0,2)) = 0.22
        _NoiseScale ("Noise Scale", Float) = 0.35
        _BaseLevel ("Base Level", Range(0,1)) = 0.25
        _ScrollSpeed ("Scroll Speed", Float) = 1.8
        _WarpAmount ("Warp Amount", Range(0,0.3)) = 0.1
        _FresnelPower ("Fresnel Power", Range(0.5,8)) = 3.0
        _RimIntensity ("Rim Intensity", Range(0,4)) = 0.6
        _Outset ("Surface Outset", Range(0,0.2)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Name "TimeHazardSkin"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha One      // 가산 — 원래 건물 색을 덮지 않고 얹는다
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Alpha;
                float  _NoiseScale;
                float  _BaseLevel;
                float  _ScrollSpeed;
                float  _WarpAmount;
                float  _FresnelPower;
                float  _RimIntensity;
                float  _Outset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
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

            // 화면 효과와 동일한 3옥타브 구성(가중치 0.5 / 0.3 / 0.2).
            float fbm(float2 p)
            {
                return valueNoise(p) * 0.5 + valueNoise(p * 2.0) * 0.3 + valueNoise(p * 4.0) * 0.2;
            }

            // 화면 효과와 동일한 대비 곡선.
            float contrast(float n)
            {
                n = saturate((n - 0.35) / 0.4);
                return n * n * (3.0 - 2.0 * n);
            }

            // 한 평면에서의 일렁임: 두 겹을 다른 방향·속도로 흘려 겹친다(화면 효과와 동일).
            float shimmer(float2 p, float t, float sA, float sB)
            {
                float2 uvA = p * sA       + float2( t * 0.050, t * 0.030);
                float2 uvB = p * sB * 1.3 + float2(-t * 0.032, t * 0.045);

                float aA = contrast(fbm(uvA));
                float aB = contrast(fbm(uvB)) * 0.7;
                return aA + aB * (1.0 - aA);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS);

                // 법선 방향으로 살짝 띄운다 = 원본과 같은 자리 겹침(z-파이팅) 방지.
                //   월드 공간에서 밀어야 오브젝트 스케일이 달라도 두께가 일정하다.
                float3 posWS = p.positionWS + n.normalWS * _Outset;

                OUT.positionWS  = posWS;
                OUT.normalWS    = n.normalWS;
                OUT.positionHCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y * _ScrollSpeed;

                // 화면 효과의 '크기 흔들림'과 같은 왜곡.
                float sA = 1.0 + sin(t * 0.7) * _WarpAmount;
                float sB = 1.0 + cos(t * 0.5) * _WarpAmount;

                float3 N = normalize(IN.normalWS);
                float3 P = IN.positionWS * _NoiseScale;

                // 삼중평면 투영 — 벽/지붕 어디서도 무늬가 늘어지지 않게 법선으로 가중 혼합.
                float3 bw = abs(N);
                bw /= max(bw.x + bw.y + bw.z, 1e-4);

                float n = shimmer(P.zy, t, sA, sB) * bw.x
                        + shimmer(P.xz, t, sA, sB) * bw.y
                        + shimmer(P.xy, t, sA, sB) * bw.z;

                // 기본 깔림 — 대비 곡선이 노이즈 절반을 0으로 깎아 생기는 '빈 면'을 메운다.
                n = _BaseLevel + (1.0 - _BaseLevel) * n;

                // 프레넬: 스칠수록(외곽) 조금 더 밝게 — 은은하게 실루엣만 잡아준다.
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);

                float a = saturate((n + fres * _RimIntensity) * _Alpha);
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
