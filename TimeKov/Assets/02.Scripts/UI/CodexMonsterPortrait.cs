using UnityEngine;
using UnityEngine.AI;

// 도감 몬스터 흉상 프리뷰 렌더러.
// 림보(아주 먼 위치)에 프리팹을 띄우고 전용 카메라로 RenderTexture에 그려
// 도감 페이스 칸(RawImage)에 표시한다. 도감은 게임 정지 중에만 보이므로 렌더 비용 사실상 0.
// 프레이밍(흉상)은 CodexPreviewConfig.MonsterEntry 데이터로 잡는다.
//
// 부작용 방지: 프리팹을 비활성 holder 아래로 띄워 스크립트 enabled=false 처리 후 활성화.
// (비활성에서 컴포넌트를 끄면 Start/Update/OnEnable이 안 돌고 Awake만 1회 - AI/물리 안 굴러감)
public class CodexMonsterPortrait : MonoBehaviour
{
    static readonly Vector3 Limbo = new Vector3(0f, -8000f, 0f);

    Camera _cam;
    Transform _stage;
    Transform _holder;
    GameObject _current;
    RenderTexture _rt;
    int _rtSize = -1;

    void EnsureRig()
    {
        if (_cam != null) return;

        _stage = new GameObject("Stage").transform;
        _stage.SetParent(transform, false);
        _stage.position = Limbo;

        var camGo = new GameObject("PortraitCam");
        camGo.transform.SetParent(_stage, false);
        _cam = camGo.AddComponent<Camera>();
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(208f / 255f, 218f / 255f, 230f / 255f, 1f);   // 밝은 쿨 글래스 톤(패널 가족, 어두운 덩어리 X)
        _cam.fieldOfView = 30f;
        _cam.nearClipPlane = 0.03f;
        _cam.farClipPlane = 100f;
        _cam.cullingMask = ~0;       // 림보엔 우리 인스턴스만 존재
        _cam.allowHDR = false;
        _cam.allowMSAA = false;
        _cam.enabled = false;        // 표시할 때만 켬

        // 포인트 라이트(range 제한이라 월드엔 영향 X) - 키(정면쪽 +Z) + 필. 어두워서 밝게.
        AddLight(new Vector3(2.5f, 3f, 3.5f), 3.0f, new Color(1f, 0.97f, 0.92f));
        AddLight(new Vector3(-2.5f, 1.5f, 1.5f), 1.6f, new Color(0.85f, 0.9f, 1f));

        _holder = new GameObject("Holder").transform;
        _holder.SetParent(_stage, false);
    }

    void AddLight(Vector3 localPos, float intensity, Color col)
    {
        var lg = new GameObject("L");
        lg.transform.SetParent(_stage, false);
        lg.transform.localPosition = localPos;
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = col; l.intensity = intensity; l.range = 40f;
    }

    public Texture Render(CodexPreviewConfig.MonsterEntry e, int size)
    {
        if (e == null || e.prefab == null) return null;
        EnsureRig();
        ClearInstance();

        if (_rt == null || _rtSize != size)
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            _rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { name = "CodexPortraitRT" };
            _rt.Create();
            _rtSize = size;
            _cam.targetTexture = _rt;
        }

        // 비활성 상태로 띄워 스크립트 끈 뒤 활성화 (Awake만 1회, 그 외 라이프사이클 차단)
        _holder.gameObject.SetActive(false);
        _current = Instantiate(e.prefab, _holder);
        _current.transform.localPosition = Vector3.zero;
        _current.transform.localRotation = Quaternion.identity;
        _current.transform.localScale = Vector3.one;
        StripForPreview(_current);
        _holder.gameObject.SetActive(true);

        // 일부 몹(화염보스)은 애니메이터 기본 상태가 '등장/상승' 클립이라 프리뷰가 위로 떠오른다.
        // previewAnimState 가 지정돼 있으면 그 상태(idle 등)를 강제 재생 + 즉시 평가해 안정 포즈로 잡는다.
        if (!string.IsNullOrEmpty(e.previewAnimState))
        {
            var anim = _current.GetComponentInChildren<Animator>(true);
            if (anim != null && anim.HasState(0, Animator.StringToHash(e.previewAnimState)))
            {
                anim.Play(e.previewAnimState, 0, 0f);
                anim.Update(0f);   // 바운드 계산 전에 그 포즈로 즉시 평가
            }
        }

        var b = CalcBounds(_current);
        // 값이 0(미설정)이면 기본값. yaw 기본 30 = 3/4 뷰(정면은 4족/긴 몸체의 깊이가 안 보여 과확대됨).
        float aimH = e.aimHeight > 0.01f ? e.aimHeight : 0.5f;
        float fill = e.fillScale > 0.01f ? e.fillScale : 0.9f;
        float yaw = Mathf.Abs(e.yaw) < 0.01f ? 30f : e.yaw;
        float pitch = Mathf.Abs(e.pitch) < 0.01f ? 8f : e.pitch;
        Vector3 aim = b.center + Vector3.up * ((aimH - 0.5f) * b.size.y);
        // yaw 각도에서 "실제 투영되는 폭"으로 맞춤 -> 4족(깊이 긴)도, 직립(높이 긴)도 적정 크기
        float yawR = yaw * Mathf.Deg2Rad;
        float projW = b.size.x * Mathf.Abs(Mathf.Cos(yawR)) + b.size.z * Mathf.Abs(Mathf.Sin(yawR));
        float radius = Mathf.Max(projW, b.size.y) * 0.5f;
        float dist = radius / Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / fill;

        // +180 = 정면(프리팹 forward +Z). LookRotation 먼저 구해 수평 오프셋(xOffset) 적용.
        Quaternion rot = Quaternion.LookRotation(Quaternion.Euler(pitch, yaw + 180f, 0f) * Vector3.forward, Vector3.up);
        aim += (rot * Vector3.right) * e.xOffset;   // 프레임 내 수평 위치(+ = 왼쪽)
        Vector3 dir = rot * Vector3.forward;
        _cam.transform.position = aim - dir * dist;
        _cam.transform.rotation = rot;

        _cam.enabled = true;   // URP는 enabled 카메라를 매 프레임 RT에 렌더(수동 Render는 비권장)
        return _rt;
    }

    static Bounds CalcBounds(GameObject go)
    {
        // 캐릭터 본체(SkinnedMesh) 우선 -> 무기/방패/이펙트 MeshRenderer가 프레이밍 흔드는 것 방지
        // (본체 중심에 정렬 + 본체 크기로 꽉 채움). 본체 없으면 전체 렌더러.
        var skinned = go.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (skinned.Length > 0)
        {
            var sb = skinned[0].bounds;
            for (int i = 1; i < skinned.Length; i++) sb.Encapsulate(skinned[i].bounds);
            return sb;
        }
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    static void StripForPreview(GameObject go)
    {
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null) mb.enabled = false;
        foreach (var ag in go.GetComponentsInChildren<NavMeshAgent>(true))
            ag.enabled = false;
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            c.enabled = false;
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true; rb.detectCollisions = false;
        }
        // 애니메이터는 켜둠(정지 중 idle 재생되게 unscaled). 끄면 T포즈로 굳음.
        // applyRootMotion 끔 = 스폰/등장 클립의 루트 모션으로 프리뷰가 위로 떠오르는 것 방지(화염보스).
        // 루트 위치/스케일 커브(applyRootMotion 이 안 막는 것)는 LateUpdate 에서 추가로 고정.
        foreach (var a in go.GetComponentsInChildren<Animator>(true))
        {
            a.updateMode = AnimatorUpdateMode.UnscaledTime;
            a.applyRootMotion = false;
        }
    }

    public void ClearInstance()
    {
        if (_current != null) { Destroy(_current); _current = null; }
        if (_cam != null) _cam.enabled = false;
    }

    // 프리뷰 인스턴스가 자기 애니메이션(루트 모션 / 루트의 위치·스케일 커브)으로 프레임 밖으로
    // 떠오르거나 커지는 것 방지. 일부 클립(화염보스 등)이 루트를 위로 크게 올린다.
    // 애니메이션은 계속 재생하되(idle 유지) 루트의 위치/스케일만 매 프레임 고정한다.
    void LateUpdate()
    {
        if (_current != null)
        {
            _current.transform.localPosition = Vector3.zero;
            _current.transform.localScale = Vector3.one;
        }
    }

    void OnDestroy()
    {
        if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
    }
}
