using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 카메라와 플레이어 사이를 막는 오브젝트를 반투명 처리하는 컴포넌트
// Player GameObject에 부착, occlusionMask에 가릴 레이어 지정
public class WallOcclusionFader : MonoBehaviour
{
    [Header("감지 설정")]
    [Tooltip("가림 판정할 레이어. 콜라이더가 있어야 Raycast로 잡힌다.")]
    public LayerMask occlusionMask;

    [Tooltip("플레이어 조준점 높이 오프셋 (가슴 정도)")]
    public float heightOffset = 1.0f;

    [Tooltip("SphereCast 두께. 0이면 얇은 광선, 0.3~0.5면 플레이어 폭만큼 감지")]
    public float castRadius = 0.4f;

    [Header("페이드 설정")]
    [Tooltip("투명해지는/복원되는 속도")]
    public float fadeSpeed = 8f;

    [Tooltip("가려질 때 최소 알파. 0=완전투명, 0.2=살짝 보임")]
    [Range(0f, 0.5f)]
    public float minAlpha = 0.2f;

    [Header("기지 내부")]
    [Tooltip("기지 안에 있을 때 투명화를 끈다")]
    [SerializeField] private bool disableInsideBase = true;

    // 내부 상태 클래스 - 각 렌더러의 페이드 상태 저장
    private class FadeState
    {
        public Renderer renderer;
        public Material[] originalShared;
        public Material[] instances;
        public float alpha = 1f;
    }

    private readonly Dictionary<Renderer, FadeState> _states = new();
    private readonly HashSet<Renderer> _thisFrame = new();
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];
    private readonly List<Renderer> _colliderlessCandidates = new();

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId     = Shader.PropertyToID("_Color");

    // 최적화 : toRemove를 필드로 올려서 매 프레임 new 제거
    private readonly List<Renderer> _toRemove = new();

    // 최적화 : PlayerStatComponent는 Start에서 한 번만 캐싱
    private PlayerStatComponent _stat;

    private bool IsInBase()
    {
        if (!disableInsideBase) return false;
        return _stat != null && _stat.IsInBase;
    }

    void Start()
    {
        _stat = GetComponent<PlayerStatComponent>();

        if (occlusionMask.value == 0)
            occlusionMask = LayerMask.GetMask("Default", "Ground");

        // Building/BuildPort 레이어는 투명화 제외
        occlusionMask.value &= ~LayerMask.GetMask("Building", "BuildPort");

        CacheColliderlessCandidates();
    }

    // 씬에서 콜라이더 없는 Renderer 캐싱 (Start에서 한 번만 실행)
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

            if (r.GetComponentInParent<Collider>() != null) continue;
            if (!IsFadeable(r)) continue;

            _colliderlessCandidates.Add(r);
        }
    }

    void LateUpdate()
    {
        _thisFrame.Clear();

        if (!IsInBase())
        {
            FindOccluders();
            FindColliderlessOccluders();
        }

        UpdateFade();
    }

    void FindOccluders()
    {
        // 줌 카메라 대응 : Camera.main을 매 프레임 가져옴
        // 줌인/줌아웃으로 카메라가 바뀌어도 항상 현재 활성 카메라를 잡음
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;
        Vector3 playerPos = transform.position + Vector3.up * heightOffset;

        Vector3 dir = playerPos - camPos;
        float dist = dir.magnitude;
        if (dist < 0.01f) return;
        dir /= dist;

        int count = Physics.SphereCastNonAlloc(
            camPos, castRadius, dir, _hitBuffer, dist,
            occlusionMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            var col = _hitBuffer[i].collider;
            if (col == null) continue;
            if (col.transform.IsChildOf(transform)) continue;

            var r = col.GetComponentInChildren<Renderer>();
            if (r == null) r = col.GetComponent<Renderer>();
            if (r != null && IsFadeable(r)) _thisFrame.Add(r);
        }
    }

    void FindColliderlessOccluders()
    {
        if (_colliderlessCandidates.Count == 0) return;

        // 줌 카메라 대응 : Camera.main을 매 프레임 가져옴
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;
        Vector3 playerPos = transform.position + Vector3.up * heightOffset;
        Vector3 d = playerPos - camPos;
        float dist = d.magnitude;
        if (dist < 0.01f) return;

        // 최적화 : Ray를 루프 밖에서 한 번만 생성
        Ray ray = new Ray(camPos, d / dist);

        foreach (var r in _colliderlessCandidates)
        {
            if (r == null) continue;
            Bounds b = r.bounds;
            b.Expand(castRadius);

            if (b.IntersectRay(ray, out float hitD) && hitD <= dist)
                _thisFrame.Add(r);
        }
    }

    void UpdateFade()
    {
        foreach (var r in _thisFrame)
        {
            var state = GetOrCreate(r);
            state.alpha = Mathf.MoveTowards(state.alpha, minAlpha, fadeSpeed * Time.deltaTime);
            ApplyAlpha(state);
        }

        // 최적화 : _toRemove 재사용, new List 제거
        _toRemove.Clear();
        foreach (var kvp in _states)
        {
            if (_thisFrame.Contains(kvp.Key)) continue;

            var state = kvp.Value;
            state.alpha = Mathf.MoveTowards(state.alpha, 1f, fadeSpeed * Time.deltaTime);
            ApplyAlpha(state);

            if (state.alpha >= 0.999f)
            {
                Restore(state);
                _toRemove.Add(kvp.Key);
            }
        }

        foreach (var r in _toRemove)
            _states.Remove(r);
    }

    FadeState GetOrCreate(Renderer r)
    {
        if (_states.TryGetValue(r, out var existing)) return existing;

        var state = new FadeState { renderer = r };
        state.originalShared = r.sharedMaterials;

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

    // 벽으로 취급할 렌더러만 페이드한다. 파티클/트레일/라인은 연출이지 벽이 아니다.
    // (레거시 파티클 셰이더는 _Color 가 없어서 페이드하려 들면 에러도 뱉는다)
    static bool IsFadeable(Renderer r)
    {
        return r is MeshRenderer || r is SkinnedMeshRenderer;
    }

    void ApplyAlpha(FadeState state)
    {
        foreach (var mat in state.instances)
        {
            if (mat == null) continue;

            // URP Lit 은 _BaseColor, 구형/Standard 는 _Color. 둘 다 없으면 알파를 못 건드리니 건너뛴다.
            int id = mat.HasProperty(BaseColorId) ? BaseColorId
                   : mat.HasProperty(ColorId)     ? ColorId
                   : -1;
            if (id == -1) continue;

            Color c = mat.GetColor(id);
            c.a = state.alpha;
            mat.SetColor(id, c);
        }
    }

    void Restore(FadeState state)
    {
        if (state.renderer != null)
            state.renderer.sharedMaterials = state.originalShared;

        foreach (var mat in state.instances)
            if (mat != null) Destroy(mat);
    }

    static void SwitchToTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
        mat.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
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