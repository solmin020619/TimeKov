using UnityEngine;

// ── 안전지대 (위험 구역 안의 쉼터) ────────────────────────────────────────────
// 시간 급속 감소 구역(TimeHazardZone) 안에 두는 작은 안전 구역.
// 플레이어가 이 안에 있는 동안 시간 감소가 '완전히' 멈추고(배율 0) 화면 효과도 걷힌다.
// 나가면 다시 바깥 위험 구역의 배율로 돌아간다.
//
//   ★위험 구역보다 우선한다(TimeHazardSystem 이 판정). 위험 구역 안쪽에 겹쳐 두면 된다.
//   ★기지 결계(BaseZone)와는 다른 물건이다. 여긴 '감소만' 멈출 뿐, 기지 판정(IsInBase)은
//     건드리지 않는다 — 귀환석 쿨타임 등 기지 연동 시스템에 영향을 주지 않기 위함.
//
//   세팅: 빈 오브젝트 + BoxCollider(isTrigger 체크) + 이 컴포넌트.
[RequireComponent(typeof(Collider))]
public class TimeSafeZone : MonoBehaviour
{
    [Header("연출 (선택)")]
    [Tooltip("안전지대에 들어와 있는 동안 켜둘 오브젝트(빛기둥/파티클 등). 비우면 없음.")]
    [SerializeField] private GameObject activeVisual;
    // ★None 이면 기본음으로 폴백(GimmickBarrier 와 같은 방식). 신규 필드가 생기기 전에 씬에 배치된
    //   구역은 None 으로 저장돼 있어 그냥 두면 무음이 되기 때문. 무음을 원하면 muteSfx 를 체크한다.
    [Tooltip("들어올 때 1회 재생(안도감). None 이면 기본음(TimeSafeEnter).")]
    [SerializeField] private SfxId enterSfx = SfxId.TimeSafeEnter;
    // 이탈음은 기본 무음(유저 결정). ★폴백 없음 — 필요하면 인스펙터에서 직접 고른다.
    [Tooltip("나갈 때 1회 재생. 기본 없음(무음) — 필요하면 직접 고른다.")]
    [SerializeField] private SfxId exitSfx = SfxId.None;
    [Tooltip("체크: 이 안전지대는 소리를 내지 않는다(위 폴백도 무시).")]
    [SerializeField] private bool muteSfx = false;

    private bool _inside;

    private SfxId Sfx(SfxId chosen, SfxId fallback) =>
        muteSfx ? SfxId.None : (chosen == SfxId.None ? fallback : chosen);

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[안전지대] {name}: Collider 가 isTrigger 가 아니라 플레이어를 감지하지 못한다.", this);

        if (activeVisual != null) activeVisual.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_inside) return;
        var stat = FindStat(other);
        if (stat == null) return;

        _inside = true;
        TimeHazardSystem.EnterSafe(this, stat);
        if (activeVisual != null) activeVisual.SetActive(true);
        GameSfx.Play(Sfx(enterSfx, SfxId.TimeSafeEnter));   // 2D — 구역 알림이라 위치감 없이 또렷하게
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_inside) return;
        if (FindStat(other) == null) return;

        _inside = false;
        TimeHazardSystem.ExitSafe(this);
        if (activeVisual != null) activeVisual.SetActive(false);
        GameSfx.Play(Sfx(exitSfx, SfxId.None));   // 폴백 없음 = 지정 안 하면 무음
    }

    private void OnDisable()
    {
        if (!_inside) return;
        _inside = false;
        TimeHazardSystem.ExitSafe(this);
        if (activeVisual != null) activeVisual.SetActive(false);
    }

    private static PlayerStatComponent FindStat(Collider other)
    {
        var player = other.GetComponentInParent<Player>();
        return player != null ? player.Stat : null;
    }
}
