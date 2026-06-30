// =====================================================================
// PlayerDeathFade.cs
// 사망 처리(래그돌 대체): 죽는 애니메이션이 재생되는 동안 캐릭터를 "머리(위)에서
// 발(아래)로" 서서히 잘라내려가며 사라지게 한다(월드 Y 디졸브) + 작은 이펙트.
// 부활 시 즉시 원상복구.
//
// 동작: 각 렌더러 머티리얼 인스턴스의 셰이더를 TIMEKOV/PlayerDissolve 로 교체하고
//       _CutoffY 를 캐릭터의 최상단→최하단으로 내린다. 프로퍼티 이름(_BaseMap/_BaseColor)이
//       URP/Lit 과 같아 셰이더만 바꿔도 텍스처/색이 유지된다.
// RespawnManager 가 사망 시 FadeOut(), 부활 시 RestoreImmediate() 를 호출한다.
// =====================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDeathFade : MonoBehaviour
{
    [Header("디졸브")]
    [Tooltip("디졸브 시작 전 죽는 애니를 보여줄 지연(초).")]
    [SerializeField] private float fadeStartDelay = 0.35f;
    [Tooltip("머리→발 완전히 사라지기까지 걸리는 시간(초).")]
    [SerializeField] private float fadeDuration = 2.5f;
    [Tooltip("사라지는 경계 발광 색(HDR). 흰색이 기본, 살짝 청록(cyan)을 섞으면 더 하이테크.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color edgeColor = Color.white;
    [Tooltip("경계 발광 두께(0~0.4).")]
    [Range(0.001f, 0.4f)]
    [SerializeField] private float edgeWidth = 0.1f;
    [Tooltip("경계 발광 세기(블룸 켜져 있으면 빛남).")]
    [SerializeField] private float edgeIntensity = 3f;
    [Tooltip("사라질수록 실루엣 외곽이 빛나는 정도(프레넬 림).")]
    [SerializeField] private float rimIntensity = 2f;
    [Tooltip("프레넬 림 날카로움(클수록 외곽만).")]
    [SerializeField] private float fresnelPower = 3f;
    [Tooltip("노이즈 빈도(클수록 잘게 얼룩짐).")]
    [SerializeField] private float noiseScale = 8f;
    [Tooltip("0=위에서 아래로 깔끔한 그라디언트, 1=완전 노이즈. 0.5 권장.")]
    [Range(0f, 1f)]
    [SerializeField] private float noiseAmount = 0.5f;

    [Header("이펙트 (선택)")]
    [Tooltip("사라질 때 낼 VFX 프리팹. 나중에 에셋 나오면 여기에 연결(비우면 이펙트 없음).")]
    [SerializeField] private GameObject deathVfxPrefab;

    private Renderer[] _renderers;
    private Material[][] _originalShared;            // 렌더러별 원본 공유 머티리얼
    private readonly List<Material> _instances = new List<Material>();  // 디졸브용 인스턴스(복구 시 파기)
    private Coroutine _co;
    private Shader _dissolveShader;

    private static readonly int DissolveId   = Shader.PropertyToID("_DissolveAmount");
    private static readonly int MinYId       = Shader.PropertyToID("_MinY");
    private static readonly int MaxYId       = Shader.PropertyToID("_MaxY");
    private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private static readonly int NoiseAmountId= Shader.PropertyToID("_NoiseAmount");
    private static readonly int EdgeWidthId  = Shader.PropertyToID("_EdgeWidth");
    private static readonly int EdgeColorId  = Shader.PropertyToID("_EdgeColor");
    private static readonly int EdgeIntId    = Shader.PropertyToID("_EdgeIntensity");
    private static readonly int RimIntId     = Shader.PropertyToID("_RimIntensity");
    private static readonly int FresnelId    = Shader.PropertyToID("_FresnelPower");

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _originalShared = new Material[_renderers.Length][];
        for (int i = 0; i < _renderers.Length; i++)
            _originalShared[i] = _renderers[i].sharedMaterials;

        _dissolveShader = Shader.Find("TIMEKOV/PlayerDissolve");
        if (_dissolveShader == null)
            Debug.LogWarning("[PlayerDeathFade] 'TIMEKOV/PlayerDissolve' 셰이더를 찾지 못함 — 디졸브 대신 즉시 숨김으로 폴백.");
    }

    /// <summary>사망: 작은 이펙트 + 죽는 애니 동안 머리→발로 서서히 사라짐.</summary>
    public void FadeOut()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(FadeRoutine());
    }

    /// <summary>부활: 즉시 원상복구(렌더러 켜고 원본 머티리얼 복원).</summary>
    public void RestoreImmediate()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _renderers[i].enabled = true;
            _renderers[i].sharedMaterials = _originalShared[i];   // 원본(불투명) 공유 머티리얼로 복귀
        }

        foreach (var m in _instances)
            if (m != null) Destroy(m);
        _instances.Clear();
    }

    private IEnumerator FadeRoutine()
    {
        SpawnVfx();   // 이펙트 한 번

        if (fadeStartDelay > 0f) yield return new WaitForSeconds(fadeStartDelay);

        // 셰이더가 없으면 디졸브 불가 → 잠시 후 그냥 숨김(안전 폴백)
        if (_dissolveShader == null)
        {
            yield return new WaitForSeconds(fadeDuration);
            foreach (var r in _renderers) if (r != null) r.enabled = false;
            _co = null;
            yield break;
        }

        // 경계는 "시작 시 한 번만" 고정(매 프레임 재면 죽는 애니로 출렁여 튄다).
        GetWorldYBounds(out float minY, out float maxY);

        // 렌더러별 머티리얼 인스턴스를 만들어 디졸브 셰이더로 교체(_BaseMap/_BaseColor 유지됨)
        _instances.Clear();
        var dissolveMats = new List<Material>();
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            var insts = r.materials;          // 인스턴스 복제본 생성·할당
            foreach (var m in insts)
            {
                if (m == null) continue;
                m.shader = _dissolveShader;
                m.SetFloat(MinYId, minY);
                m.SetFloat(MaxYId, maxY);
                m.SetFloat(NoiseScaleId, noiseScale);
                m.SetFloat(NoiseAmountId, noiseAmount);
                m.SetFloat(EdgeWidthId, edgeWidth);
                m.SetColor(EdgeColorId, edgeColor);
                m.SetFloat(EdgeIntId, edgeIntensity);
                m.SetFloat(RimIntId, rimIntensity);
                m.SetFloat(FresnelId, fresnelPower);
                m.SetFloat(DissolveId, 0f);   // 시작은 온전
                dissolveMats.Add(m);
                _instances.Add(m);
            }
        }

        // 디졸브 진행도 0 → 1.05(끝에 확실히 전부 사라지게 살짝 넘김). 머리→발 순서로 녹듯이.
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float amount = Mathf.Clamp01(t / fadeDuration) * 1.05f;
            foreach (var m in dissolveMats) m.SetFloat(DissolveId, amount);
            yield return null;
        }

        foreach (var r in _renderers)
            if (r != null) r.enabled = false;

        _co = null;
    }

    // 현재 모든 렌더러를 감싸는 월드 Y 범위.
    private void GetWorldYBounds(out float minY, out float maxY)
    {
        bool any = false;
        Bounds b = default;
        foreach (var r in _renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any) { minY = transform.position.y; maxY = minY + 2f; return; }
        minY = b.min.y;
        maxY = b.max.y;
    }

    private void SpawnVfx()
    {
        // VFX 프리팹이 연결돼 있을 때만 생성. (아직 에셋 없으면 이펙트 없이 디졸브만)
        if (deathVfxPrefab == null) return;

        Vector3 pos = transform.position + Vector3.up * 1f;
        var go = Instantiate(deathVfxPrefab, pos, Quaternion.identity);
        Destroy(go, 4f);
    }
}
