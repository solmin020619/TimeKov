using UnityEngine;

// 맵 워프 지점. 단일 World.unity 안에서 씬 내 텔레포트로 이동한다.
//   - RegionPoint : 각 테마 맵(설산/사막/용암)에 배치. 최초 1회는 'F 상호작용'으로 활성화(저장),
//                   활성화 후 밟으면 기지로 복귀. (IInteractable/IInteractHint 로 F 알약 표시)
//   - BaseOutbound: 기지에 배치(테마별 1개). 밟으면 해당 테마의 '활성화된' RegionPoint 중 하나로 랜덤 워프.
// 실제 이동/페이드/VFX/저장은 WarpManager 가 처리한다. 이 컴포넌트는 트리거 감지 + F 활성화만.
[RequireComponent(typeof(Collider))]
public class WarpPoint : MonoBehaviour, IInteractable, IInteractHint
{
    public enum WarpKind { BaseOutbound, RegionPoint }

    [Header("종류 / 지역")]
    public WarpKind kind = WarpKind.RegionPoint;

    [Tooltip("BaseOutbound=목적지 테마, RegionPoint=자신이 속한 테마. (자연 제외: 설산/사막/용암)")]
    public TransmissionRegion region = TransmissionRegion.Snow;

    [Tooltip("저장용 고유 ID. 씬에서 유일해야 한다. 예) Snow_1, Desert_2. RegionPoint 만 사용.")]
    public string warpId = "";

    [Header("도착 위치")]
    [Tooltip("이 지점으로 워프해 올 때 플레이어가 나타날 위치(원통 밖 지면). 비우면 이 오브젝트 위치(원통 중앙).")]
    public Transform arrivalPoint;

    [Header("통 색상")]
    [Tooltip("켜면 실행 시 이 워프 통(및 자식)의 발광/HDR 색을 지역 색으로 바꾼다. 설산=원본 유지.")]
    public bool recolorTube = true;
    [Tooltip("색을 바꿀 렌더러를 직접 지정(비우면 자식 렌더러 전체에서 발광 색만 자동으로 찾음).")]
    public Renderer[] tubeRenderers;

    [Header("VFX (비우면 없음)")]
    [Tooltip("출발(사라짐) 연출 프리팹.")]
    public GameObject departureVfx;
    [Tooltip("도착(등장) 연출 프리팹.")]
    public GameObject arrivalVfx;

    [Header("근접 힌트 (F 활성화 — 지역 지점만 사용)")]
    [Tooltip("가까이 가면 켤 F 알약 UI. 비우면 씬의 공용 패널(FacilityUnlockSelectPanel)을 런타임에 자동으로 찾아 쓴다.")]
    [SerializeField] private GameObject hintUI;
    [Tooltip("알약에 표시할 이름.")]
    [SerializeField] private string hintLabel = "워프 활성화";
    [Tooltip("알약 왼쪽 아이콘. 비우면 패널에 원래 박혀 있는 아이콘을 그대로 쓴다.")]
    [SerializeField] private Sprite hintIcon;
    [Tooltip("가까이 가면 외곽선을 켤 대상들. 비우면 이 오브젝트(원통)를 쓴다.")]
    [SerializeField] private Transform[] outlineTargets;

    // 사운드는 GameSfxConfig 로 통합 관리 — 충전 루프(SfxId.WarpChargeLoop) / 도착(SfxId.WarpArrive).

    // 워프로 이 지점에 방금 도착 → 트리거에서 나갔다 다시 들어오기 전까지 재발동 무시(즉시 재텔레포트 방지).
    private bool _ignoreUntilExit;
    private InteractHighlight _highlight;   // 근접 외곽선(지역 지점만)

    public bool IsRegionPoint => kind == WarpKind.RegionPoint;

    /// <summary>원통의 기하 중심(수평). 피벗이 치우쳐 있어도 트리거 콜라이더 중심을 쓴다. 높이는 피벗 기준(지면).</summary>
    public Vector3 Center
    {
        get
        {
            Vector3 p = transform.position;
            foreach (var c in GetComponents<Collider>())
                if (c.isTrigger) { var b = c.bounds.center; return new Vector3(b.x, p.y, b.z); }
            return p;
        }
    }

    /// <summary>이 지점으로 워프해 올 때 플레이어가 실제로 나타날 위치.</summary>
    public Vector3 ArrivalPosition => arrivalPoint != null ? arrivalPoint.position : Center;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        // 지역 워프 지점만 F 상호작용으로 최초 활성화한다. (기지 아웃바운드는 밟기 전용)
        if (kind != WarpKind.RegionPoint) return;

        // PlayerInteractComponent 의 OverlapSphere 는 Interactable 레이어만 훑는다.
        //   워프통은 Ground 레이어라 그대로면 F 감지가 안 된다. 트리거 전용이라 옮겨도 지면판정과 무관
        //   (지면 판정은 전부 QueryTriggerInteraction.Ignore 라 트리거를 지면으로 안 침).
        int il = LayerMask.NameToLayer("Interactable");
        if (il >= 0) gameObject.layer = il;

        // 공용 F 알약 패널을 인스펙터에서 안 넣었으면 씬에서 자동으로 찾는다(수동 드래그 생략).
        if (hintUI == null) hintUI = InteractHintPanel.FindSharedPanel();

        _highlight = new InteractHighlight(outlineTargets, transform);
        InteractHintPanel.Prime(hintUI, this);
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<PlayerMovementComponent>();
        if (player == null) return;
        PlayerInside = true;   // 억제 중이어도 '통 안' 상태는 추적(영상 후 워프 재개 판정용)
        if (_ignoreUntilExit) return;
        WarpManager.Instance?.OnWarpStepped(this, player.transform);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovementComponent>() == null) return;
        PlayerInside = false;
        _ignoreUntilExit = false;
        WarpManager.Instance?.OnWarpExited(this);   // 충전 중 나가면 취소
    }

    /// <summary>플레이어가 이 워프 트리거 안에 있는지(영상 팝업 뒤 워프 재개 판정용).</summary>
    public bool PlayerInside { get; private set; }

    /// <summary>워프로 이 지점에 도착했을 때 호출 — 나갔다 다시 밟기 전까지 재발동을 막는다.</summary>
    public void SuppressUntilExit() => _ignoreUntilExit = true;

    // ── F 상호작용 (지역 지점 최초 활성화) ────────────────────────────
    //   활성화 전에만 F 후보가 된다. 활성화되면 CanInteract=false 가 되어 이후엔 F 힌트가 사라지고,
    //   밟기(OnTriggerEnter)로 기지 복귀가 동작한다.
    public bool CanInteract =>
        kind == WarpKind.RegionPoint && !string.IsNullOrEmpty(warpId)
        && WarpManager.Instance != null && !WarpManager.Instance.IsActivated(warpId);

    public void Interact(Player player)
    {
        WarpManager.Instance?.ActivateRegionPoint(this, player != null ? player.transform : null);
    }

    public void ShowHint(bool show)
    {
        // InteractHintPanel 은 받은 문자열을 그대로 찍는다. 번역은 부르는 쪽 몫이다
        // (다른 상호작용 9곳은 전부 Loc.Get 을 거치는데 여기만 빠져 있었다).
        InteractHintPanel.Show(hintUI, show, Loc.Get(hintLabel), hintIcon);
        _highlight?.Set(show);
    }
}
