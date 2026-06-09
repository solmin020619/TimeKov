using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 카메라와 플레이어 사이를 실제로 가로막는 오브젝트(벽/건물/나무)를 반투명 처리.
/// 엔드필드식 — 시야를 가리는 것만 투과시켜 플레이어가 항상 보이게 한다.
///
/// 감지 방식: 카메라 -> 플레이어 광선을 RaycastAll 로 쏴서 "실제로 관통하는 콜라이더"만 투명화.
/// (기존 Bounds 교차 방식은 옆에 비껴 선 건물도 통째로 투명해지고 내부 메시까지 비쳐서 교체함.)
///
/// Player GameObject 에 부착. occlusionMask 에 가릴 수 있는 레이어(Ground/Building/나무 등) 지정.
/// </summary>
public class WallOcclusionFader : MonoBehaviour
{
    [Header("감지 설정")]
    [Tooltip("가림 판정할 레이어. 카메라와 플레이어 사이를 막을 수 있는 것(Ground/Building/나무 등).\n" +
             "콜라이더가 있어야 Raycast 로 잡힌다.")]
    public LayerMask occlusionMask;

    [Tooltip("플레이어 조준점 높이 오프셋 (가슴 정도). 광선의 도착 지점.")]
    public float heightOffset = 1.0f;

    [Tooltip("플레이어 주변 이 반경 안의 가림만 처리 (RaycastAll 의 SphereCast 두께).\n" +
             "0 이면 얇은 광선, 0.3~0.5 면 플레이어 폭만큼 넉넉히 감지.")]
    public float castRadius = 0.4f;

    [Header("페이드 설정")]
    [Tooltip("투명해지는/복원되는 속도")]
    public float fadeSpeed = 8f;

    [Tooltip("가려질 때 최소 알파 (0=완전투명, 0.2=살짝 보임). 플레이어 위치 가늠되게 약간 남기는 걸 권장.")]
    [Range(0f, 0.5f)]
    public float minAlpha = 0.2f;

    // ── 내부 상태 ────────────────────────────────────────────────────

    private class FadeState
    {
        public Renderer   renderer;
        public Material[] originalShared;
        public Material[] instances;
        public float      alpha = 1f;
    }

    private readonly Dictionary<Renderer, FadeState> _states    = new();
    private readonly HashSet<Renderer>               _thisFrame = new();
    // RaycastAll 결과 버퍼 (GC 방지용 재사용)
    private readonly RaycastHit[]                     _hitBuffer = new RaycastHit[16];

    // 콜라이더 없는 occluder(SpeedTree 등) 폴백 감지용 Renderer 캐시
    private readonly List<Renderer> _colliderlessCandidates = new();

    void Start()
    {
        // occlusionMask 미설정 시 안전 폴백: Default/Ground (나무/지형만)
        if (occlusionMask.value == 0)
            occlusionMask = LayerMask.GetMask("Default", "Ground");

        // 구조물(건물/설비)은 반투명 제외 (#22) — 건물이 투과되면 내부 메시가 비쳐 보기 안 좋다는 QA.
        // 인스펙터에서 Building/BuildPort 가 켜져 있어도 강제로 끈다. 나무/지형 가림은 유지(플레이어 가림 방지).
        occlusionMask.value &= ~LayerMask.GetMask("Building", "BuildPort");

        CacheColliderlessCandidates();
    }

    // 씬에서 occlusionMask 레이어이면서 콜라이더가 없는 Renderer 캐시.
    // 콜라이더가 있는 것은 Raycast 로 잡고, 없는 것(SpeedTree 등)은 Bounds 로 보조 감지.
    void CacheColliderlessCandidates()
    {
        _colliderlessCandidates.Clear();
        var all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in all)
        {
            if (r == null) continue;
            if (r.transform.IsChildOf(transform)) continue;

            int layer = r.gameObject.layer;
            if ((occlusionMask.value & (1 << layer)) == 0) continue;

            // 콜라이더가 있으면 Raycast 로 처리되므로 폴백 후보에서 제외
            if (r.GetComponentInParent<Collider>() != null) continue;

            _colliderlessCandidates.Add(r);
        }
    }

    // ── 메인 루프 ────────────────────────────────────────────────────

    void LateUpdate()
    {
        _thisFrame.Clear();
        FindOccluders();
        FindColliderlessOccluders();
        UpdateFade();
    }

    // 콜라이더 없는 후보(SpeedTree 등)는 카메라~플레이어 선분과 Bounds 교차로 감지
    void FindColliderlessOccluders()
    {
        if (_colliderlessCandidates.Count == 0 || Camera.main == null) return;

        Vector3 camPos    = Camera.main.transform.position;
        Vector3 playerPos = transform.position + Vector3.up * heightOffset;

        foreach (var r in _colliderlessCandidates)
        {
            if (r == null) continue;
            Bounds b = r.bounds;
            b.Expand(castRadius);

            Vector3 d = playerPos - camPos;
            float dist = d.magnitude;
            if (dist < 0.01f) continue;
            Ray ray = new Ray(camPos, d / dist);
            if (b.IntersectRay(ray, out float hitD) && hitD <= dist)
                _thisFrame.Add(r);
        }
    }

    void FindOccluders()
    {
        if (Camera.main == null) return;

        Vector3 camPos    = Camera.main.transform.position;
        Vector3 playerPos = transform.position + Vector3.up * heightOffset;

        Vector3 dir  = playerPos - camPos;
        float   dist = dir.magnitude;
        if (dist < 0.01f) return;
        dir /= dist;

        // 카메라 -> 플레이어 사이를 실제로 막는 콜라이더만 수집 (관통 전부)
        int count = Physics.SphereCastNonAlloc(
            camPos, castRadius, dir, _hitBuffer, dist,
            occlusionMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            var col = _hitBuffer[i].collider;
            if (col == null) continue;

            // 자기 자신(플레이어) 하위는 제외
            if (col.transform.IsChildOf(transform)) continue;

            var r = col.GetComponentInChildren<Renderer>();
            if (r == null) r = col.GetComponent<Renderer>();
            if (r != null) _thisFrame.Add(r);
        }
    }

    void UpdateFade()
    {
        // 이번 프레임에 가리는 것 → 투명 방향
        foreach (var r in _thisFrame)
        {
            var state = GetOrCreate(r);
            state.alpha = Mathf.MoveTowards(state.alpha, minAlpha, fadeSpeed * Time.deltaTime);
            ApplyAlpha(state);
        }

        // 더 이상 안 가리는 것 → 불투명 복원 후 제거
        var toRemove = new List<Renderer>();
        foreach (var kvp in _states)
        {
            if (_thisFrame.Contains(kvp.Key)) continue;

            var state = kvp.Value;
            state.alpha = Mathf.MoveTowards(state.alpha, 1f, fadeSpeed * Time.deltaTime);
            ApplyAlpha(state);

            if (state.alpha >= 0.999f)
            {
                Restore(state);
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var r in toRemove)
            _states.Remove(r);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    FadeState GetOrCreate(Renderer r)
    {
        if (_states.TryGetValue(r, out var existing)) return existing;

        var state = new FadeState { renderer = r };
        state.originalShared = r.sharedMaterials;

        // 투명 처리용 인스턴스 생성 (원본 sharedMaterial 은 안 건드림)
        state.instances = new Material[state.originalShared.Length];
        for (int i = 0; i < state.originalShared.Length; i++)
        {
            state.instances[i] = new Material(state.originalShared[i]);
            SwitchToTransparent(state.instances[i]);
        }

        r.materials = state.instances;
        state.alpha = 1f;

        _states[r] = state;
        return state;
    }

    void ApplyAlpha(FadeState state)
    {
        foreach (var mat in state.instances)
        {
            if (mat == null) continue;
            Color c = mat.color;
            c.a = state.alpha;
            mat.color = c;
        }
    }

    void Restore(FadeState state)
    {
        if (state.renderer != null)
            state.renderer.sharedMaterials = state.originalShared;

        foreach (var mat in state.instances)
            if (mat != null) Destroy(mat);
    }

    // URP Lit Opaque -> Transparent 런타임 전환
    static void SwitchToTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend",   0f);
        mat.SetInt("_SrcBlend",      (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend",      (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
        mat.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite",        0);
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    void OnDestroy()
    {
        foreach (var kvp in _states)
            Restore(kvp.Value);
        _states.Clear();
    }
}
