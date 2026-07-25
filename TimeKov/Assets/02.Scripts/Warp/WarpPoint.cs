using UnityEngine;

// 맵 워프 지점(밟으면 자동 발동, 통과형). 단일 World.unity 안에서 씬 내 텔레포트로 이동한다.
//   - RegionPoint : 각 테마 맵(설산/사막/용암)에 배치. 최초로 밟으면 '활성화'(저장), 활성화 후 다시 밟으면 기지로 복귀.
//   - BaseOutbound: 기지에 배치(테마별 1개). 밟으면 해당 테마의 '활성화된' RegionPoint 중 하나로 랜덤 워프.
// 실제 이동/페이드/VFX/저장은 WarpManager 가 처리한다. 이 컴포넌트는 트리거 감지만.
[RequireComponent(typeof(Collider))]
public class WarpPoint : MonoBehaviour
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

    // 사운드는 GameSfxConfig 로 통합 관리 — 충전 루프(SfxId.WarpChargeLoop) / 도착(SfxId.WarpArrive).

    // 워프로 이 지점에 방금 도착 → 트리거에서 나갔다 다시 들어오기 전까지 재발동 무시(즉시 재텔레포트 방지).
    private bool _ignoreUntilExit;

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

    private void OnTriggerEnter(Collider other)
    {
        if (_ignoreUntilExit) return;
        var player = other.GetComponentInParent<PlayerMovementComponent>();
        if (player == null) return;
        WarpManager.Instance?.OnWarpStepped(this, player.transform);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovementComponent>() == null) return;
        _ignoreUntilExit = false;
        WarpManager.Instance?.OnWarpExited(this);   // 충전 중 나가면 취소
    }

    /// <summary>워프로 이 지점에 도착했을 때 호출 — 나갔다 다시 밟기 전까지 재발동을 막는다.</summary>
    public void SuppressUntilExit() => _ignoreUntilExit = true;
}
