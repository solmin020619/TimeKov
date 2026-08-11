using System.Collections.Generic;
using UnityEngine;

// ── 위험 건물 표면 일렁임 (구역 표시) ────────────────────────────────────────
// 시간 급속감소 구역인 건물을 "밖에서 봐도 알아보게" 겉면에 은은한 표식을 입힌다.
//
//   원리: 대상 렌더러의 메시를 그대로 복제한 '껍질(skin)'을 자식으로 만들고,
//         거기에만 TIMEKOV/TimeHazardSurface 셰이더를 입힌다.
//   ★원본 렌더러/머티리얼은 건드리지 않는다 — 공유 프리팹/머티리얼 오염 방지.
//     (renderer.materials 에 덧붙이는 방식은 서브메시가 여러 개면 마지막 하나에만 덧그려진다)
//
//   ★실내 처리: '어느 렌더러/면이 바깥인가'를 기하로 가려내려던 장치(외피 선별·법선 판정)는
//     전부 걷어냈다. 다층 건물에서 자꾸 틀렸고, 외벽까지 같이 잘려 정면이 텅 비는 부작용이 컸다.
//     대신 모든 렌더러를 덮고, '카메라가 건물 안이면 통째로 끈다'로 해결한다(단순하고 확실).
//
//   붙이는 곳: 건물 오브젝트(루트). Tools/TIMEKOV/시간구역 ③ 으로 자동 부착 가능.
//   ★껍질은 Start() 에서 만든다 → 에디터 씬 뷰엔 안 보이고 플레이해야 보인다
//     (씬에 껍질 오브젝트가 저장되지 않게 하기 위함). 플레이 중 값 변경은 즉시 반영된다.
public class TimeHazardSurfaceFx : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("표식을 입힐 렌더러들. 비우면 자식 렌더러를 전부 쓴다(권장).")]
    [SerializeField] private Renderer[] targets;

    [Tooltip("이름에 이 조각이 든 렌더러는 제외(소문자 비교). 유리·데칼 등 덧입히면 이상한 것.")]
    [SerializeField] private string[] excludeNameContains = { "decal" };

    [Header("모양")]
    [Tooltip("일렁임 색(HDR). 파란 계열 기본. 블룸이 켜져 있으면 은은하게 번진다.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color color = new Color(0.15f, 0.45f, 0.85f, 1f);

    [Tooltip("전체 세기. 표식이라 은은한 게 좋다(0.15~0.3 권장). 너무 높으면 건물 재질이 묻힌다.")]
    [SerializeField, Range(0f, 2f)] private float alpha = 0.22f;

    [Tooltip("무늬 크기. 작을수록 무늬가 커진다(월드 좌표 기준이라 건물 크기와 무관하게 일정).")]
    [SerializeField, Range(0.05f, 2f)] private float noiseScale = 0.35f;

    [Tooltip("표면 전체에 기본으로 깔리는 정도. 0 이면 노이즈가 진한 얼룩에만 나와 빈 면이 생긴다.\n" +
             "올릴수록 고르게 덮이고 일렁임은 그 위에서 오르내린다.")]
    [SerializeField, Range(0f, 1f)] private float baseLevel = 0.25f;

    [Tooltip("일렁임이 흐르는 속도. 화면 효과의 Shimmer Speed 와 같은 값이면 결이 맞는다.")]
    [SerializeField, Range(0f, 5f)] private float scrollSpeed = 1.8f;

    [Tooltip("왜곡(무늬가 늘었다 줄었다 하는 정도). 화면 효과의 Warp Amount 와 같은 역할.")]
    [SerializeField, Range(0f, 0.3f)] private float warpAmount = 0.1f;

    [Tooltip("외곽(실루엣) 강조 세기. 은은하게 두려면 낮게.")]
    [SerializeField, Range(0f, 4f)] private float rimIntensity = 0.6f;
    [Tooltip("외곽 강조가 퍼지는 범위. 낮을수록 넓게 빛난다.")]
    [SerializeField, Range(0.5f, 8f)] private float fresnelPower = 3f;

    [Tooltip("원본 표면에서 껍질을 띄우는 거리(m). 너무 작으면 지글거리고, 크면 떠 보인다.")]
    [SerializeField, Range(0f, 0.2f)] private float outset = 0.02f;

    [Header("실내에서 끄기")]
    [Tooltip("체크: 플레이어가 시간 구역(TimeHazardZone) 안에 들어가면 표식을 통째로 끈다.\n" +
             "판정은 그 구역의 트리거 콜라이더 하나로 한다 — 이미 건물에 맞춰 둔 범위라\n" +
             "여기서 경계를 따로 계산하지 않는다(계단·안테나 때문에 어긋나던 문제도 없어진다).")]
    [SerializeField] private bool hideWhenPlayerInside = true;

    [Tooltip("기준이 될 시간 구역. 비우면 이 건물과 겹치는 구역을 자동으로 찾는다.")]
    [SerializeField] private TimeHazardZone zone;

    private const string ShaderName = "TIMEKOV/TimeHazardSurface";
    private const string SkinName   = "TimeHazardSkin";

    private Material _mat;
    private Bounds _building;   // 기즈모 표시용(대상 렌더러 통합 경계)
    private bool _hidden;
    private readonly List<GameObject> _skins = new();

    private void Start()
    {
        Build();
        if (zone == null) zone = FindZone();
        if (hideWhenPlayerInside && zone == null)
            Debug.LogWarning($"[시간구역] {name}: 기준으로 삼을 TimeHazardZone 을 찾지 못했다. " +
                             "Zone 을 직접 연결하거나, 구역 콜라이더가 이 건물과 겹치는지 확인하라.", this);
    }

    // 구역 트리거 안이면 껍질을 끈다. 구역이 이미 출입을 판정하고 있으므로 그 결과만 따라간다.
    private void Update()
    {
        if (!hideWhenPlayerInside || zone == null || _skins.Count == 0) return;

        bool inside = zone.PlayerInside;
        if (inside == _hidden) return;   // 상태 변화 없을 때는 아무것도 안 한다
        _hidden = inside;
        SetVisible(!inside);
    }

    // 이 건물과 겹치는 시간 구역을 찾는다(여러 개면 가장 많이 겹치는 것).
    private TimeHazardZone FindZone()
    {
        TimeHazardZone best = null;
        foreach (var z in FindObjectsByType<TimeHazardZone>(FindObjectsSortMode.None))
        {
            var col = z.GetComponent<Collider>();
            if (col == null || !col.bounds.Intersects(_building)) continue;
            if (best == null) best = z;
        }
        return best;
    }

    private void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }

    private void Build()
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[시간구역] {name}: 셰이더 '{ShaderName}' 를 찾지 못해 표면 표식을 만들지 못했다.", this);
            return;
        }

        var picked = ResolveTargets();
        if (picked.Count == 0)
        {
            Debug.LogWarning($"[시간구역] {name}: 표식을 입힐 렌더러가 없다.", this);
            return;
        }

        _building = CombinedBounds(picked);

        _mat = new Material(shader) { hideFlags = HideFlags.DontSave };
        ApplyParams();

        foreach (var r in picked)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;   // 스킨드 메시 등은 제외(건물용)
            CreateSkin(r.transform, mf.sharedMesh);
        }
    }

    // 대상 = 지정이 있으면 그대로, 없으면 자식 렌더러 전부(이름 제외만 적용).
    private List<Renderer> ResolveTargets()
    {
        var result = new List<Renderer>();

        if (targets != null && targets.Length > 0)
        {
            foreach (var r in targets) if (r != null) result.Add(r);
            return result;
        }

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            if (r.gameObject.name == SkinName) continue;   // 자기 껍질 재복제 방지
            if (IsExcludedByName(r.gameObject.name)) continue;
            result.Add(r);
        }
        return result;
    }

    private static Bounds CombinedBounds(List<Renderer> list)
    {
        bool has = false;
        Bounds b = default;
        for (int i = 0; i < list.Count; i++)
        {
            var r = list[i];
            if (r == null) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        return has ? b : new Bounds(Vector3.zero, Vector3.one);
    }

    private bool IsExcludedByName(string objName)
    {
        if (excludeNameContains == null) return false;
        string lower = objName.ToLowerInvariant();
        foreach (var key in excludeNameContains)
            if (!string.IsNullOrEmpty(key) && lower.Contains(key.ToLowerInvariant())) return true;
        return false;
    }

    // ── 껍질 생성 ────────────────────────────────────────────────────────────
    private void CreateSkin(Transform src, Mesh mesh)
    {
        var go = new GameObject(SkinName);
        go.transform.SetParent(src, false);   // 원본과 같은 위치/회전/스케일
        go.layer = src.gameObject.layer;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        // 서브메시 수만큼 같은 머티리얼을 채워 전 면적을 덮는다(재질이 나뉜 건물 대응).
        var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
        for (int i = 0; i < mats.Length; i++) mats[i] = _mat;
        mr.sharedMaterials = mats;

        mr.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows       = false;
        mr.lightProbeUsage      = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        _skins.Add(go);
    }

    private void ApplyParams()
    {
        if (_mat == null) return;
        _mat.SetColor("_Color", color);
        _mat.SetFloat("_Alpha", alpha);
        _mat.SetFloat("_NoiseScale", noiseScale);
        _mat.SetFloat("_BaseLevel", baseLevel);
        _mat.SetFloat("_ScrollSpeed", scrollSpeed);
        _mat.SetFloat("_WarpAmount", warpAmount);
        _mat.SetFloat("_FresnelPower", fresnelPower);
        _mat.SetFloat("_RimIntensity", rimIntensity);
        _mat.SetFloat("_Outset", outset);
    }

    // 플레이 중 인스펙터 조절이 바로 보이게(색·세기 맞추기 편하게).
    private void OnValidate()
    {
        if (Application.isPlaying) ApplyParams();
    }

    // 선택했을 때 판정 기준이 되는 구역 콜라이더를 보여준다(어떤 구역을 따라가는지 눈으로 확인).
    //   하늘색 = 이 안에 플레이어가 들어오면 표식이 꺼진다.
    private void OnDrawGizmosSelected()
    {
        if (!hideWhenPlayerInside) return;

        var z = zone;
        if (z == null)
        {
            // 에디터에선 아직 자동 탐색 전이므로 여기서 임시로 찾아 보여준다.
            _building = CombinedBounds(ResolveTargets());
            z = FindZone();
        }
        if (z == null) return;

        var col = z.GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }

    /// 표식 켜고 끄기(연출용 — 예: 구역이 해제되면 표식도 사라지게).
    public void SetVisible(bool on)
    {
        for (int i = 0; i < _skins.Count; i++)
            if (_skins[i] != null) _skins[i].SetActive(on);
    }
}
