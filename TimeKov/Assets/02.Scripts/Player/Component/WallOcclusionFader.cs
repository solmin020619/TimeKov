using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 카메라와 플레이어 사이에 있는 벽/오브젝트를 반투명하게 페이드
/// 콜라이더 유무와 관계없이 Renderer의 Bounds로 감지
/// Player GameObject에 부착
/// </summary>
public class WallOcclusionFader : MonoBehaviour
{
    [Header("감지 설정")]
    [Tooltip("감지할 레이어 (Ground 등 씬 오브젝트 레이어)")]
    public LayerMask occlusionMask;

    [Tooltip("캐릭터 중심 높이 오프셋")]
    public float heightOffset = 1.0f;

    [Tooltip("Bounds 교차 판정 여유 (값이 클수록 더 넓게 감지)")]
    public float boundsExpand = 0.1f;

    [Header("페이드 설정")]
    [Tooltip("투명해지는/복원되는 속도")]
    public float fadeSpeed = 6f;

    [Tooltip("가려질 때 최소 알파 (0=완전투명, 0.15=살짝 보임)")]
    [Range(0f, 0.5f)]
    public float minAlpha = 0.15f;

    // ── 내부 상태 ────────────────────────────────────────────────────

    private class FadeState
    {
        public Renderer   renderer;
        public Material[] originalShared;
        public Material[] instances;
        public float      alpha = 1f;
    }

    // 씬에서 occlusionMask 레이어에 해당하는 모든 Renderer 캐시
    private readonly List<Renderer>                _candidates = new();
    private readonly Dictionary<Renderer, FadeState> _states   = new();
    private readonly HashSet<Renderer>              _thisFrame  = new();

    // ── 초기화 ───────────────────────────────────────────────────────

    void Start()
    {
        CacheRenderers();
    }

    void CacheRenderers()
    {
        _candidates.Clear();

        // 씬의 모든 Renderer 중 occlusionMask 레이어인 것만 캐시
        var all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in all)
        {
            if (r == null) continue;
            // 자기 자신 하위 오브젝트 제외
            if (r.transform.IsChildOf(transform)) continue;

            int layer = r.gameObject.layer;
            if ((occlusionMask.value & (1 << layer)) != 0)
                _candidates.Add(r);
        }

        Debug.Log($"[WallOcclusionFader] 감지 대상 렌더러 {_candidates.Count}개 캐시 완료");
    }

    // ── 메인 루프 ────────────────────────────────────────────────────

    void LateUpdate()
    {
        _thisFrame.Clear();
        FindOccluders();
        UpdateFade();
    }

    void FindOccluders()
    {
        if (Camera.main == null) return;

        Vector3 camPos    = Camera.main.transform.position;
        Vector3 playerPos = transform.position + Vector3.up * heightOffset;

        // 카메라~플레이어 선분과 Renderer Bounds가 교차하는 오브젝트를 수집
        foreach (var r in _candidates)
        {
            if (r == null) continue;

            Bounds b = r.bounds;
            b.Expand(boundsExpand); // 약간 넉넉하게 확장

            if (BoundsIntersectsSegment(b, camPos, playerPos))
                _thisFrame.Add(r);
        }
    }

    void UpdateFade()
    {
        // 이번 프레임에 가리는 오브젝트 → 투명 방향으로 페이드
        foreach (var r in _thisFrame)
        {
            var state = GetOrCreate(r);
            state.alpha = Mathf.MoveTowards(state.alpha, minAlpha, fadeSpeed * Time.deltaTime);
            ApplyAlpha(state);
        }

        // 더 이상 안 가리는 오브젝트 → 불투명 복원
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

    // 카메라~플레이어 선분과 Bounds 교차 판정
    static bool BoundsIntersectsSegment(Bounds bounds, Vector3 a, Vector3 b)
    {
        Vector3 dir  = b - a;
        float   dist = dir.magnitude;
        if (dist < 0.001f) return false;

        Ray ray = new Ray(a, dir / dist);
        // IntersectRay: 교차 시 true, out 거리 반환
        return bounds.IntersectRay(ray, out float d) && d <= dist;
    }

    FadeState GetOrCreate(Renderer r)
    {
        if (_states.TryGetValue(r, out var existing)) return existing;

        var state = new FadeState { renderer = r };

        // 원본 sharedMaterials 저장
        state.originalShared = r.sharedMaterials;

        // 투명 처리용 인스턴스 생성
        state.instances = new Material[state.originalShared.Length];
        for (int i = 0; i < state.originalShared.Length; i++)
        {
            state.instances[i] = new Material(state.originalShared[i]);
            SwitchToTransparent(state.instances[i]);
        }

        r.materials = state.instances;
        state.alpha  = 1f;

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

    // URP Lit Opaque → Transparent 런타임 전환
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
