using UnityEngine;

// ── 기믹 "대상"(Target) 공용 베이스 ──────────────────────────────────────────
// 트리거(GimmickTrigger)가 조건을 충족하면 열리는 모든 대상의 부모.
//   결계 해제 / 봉인 개방 / 문 열림 / 다리 생성 등 겉모습은 달라도 "열림/닫힘 상태 1개"로 공유한다.
//   IsOpen == true  = 해결됨(통행 가능/개방)  /  false = 잠김(막힘).
//   실제 개폐 동작은 파생 클래스가 ApplyState 로 구현한다(GimmickBarrier 참고).
//   전환 사운드/VFX 는 여기서 공통 처리 → 파생은 "무엇을 켜고 끌지"만 신경 쓰면 된다.
public abstract class GimmickTarget : MonoBehaviour
{
    [Header("초기 상태")]
    [Tooltip("체크하면 시작부터 '열림'(해결된) 상태. 보통 꺼둔다(잠김에서 시작).")]
    [SerializeField] protected bool startOpen = false;

    [Header("열릴 때 효과 (선택)")]
    [Tooltip("열리는 순간 1회 재생할 사운드. None 이면 무음.")]
    [SerializeField] private SfxId openSfx = SfxId.None;
    [Tooltip("열리는 순간 생성할 VFX 프리팹. 비우면 없음.")]
    [SerializeField] private GameObject openVfxPrefab;
    [Tooltip("사운드/VFX 재생 위치. 비우면 이 오브젝트 위치.")]
    [SerializeField] private Transform effectAnchor;

    // 현재 열림 여부. 트리거가 SetOpen 으로 바꾼다.
    public bool IsOpen { get; private set; }

    protected virtual void Start()
    {
        IsOpen = startOpen;
        ApplyState(startOpen, instant: true);   // 시작 상태는 효과 없이 즉시 반영
    }

    // 트리거가 호출: 열림/닫힘 전환. 이미 같은 상태면 무시.
    public void SetOpen(bool open)
    {
        if (IsOpen == open) return;
        IsOpen = open;
        ApplyState(open, instant: false);
        if (open) PlayOpenEffects();
    }

    private void PlayOpenEffects()
    {
        Vector3 pos = effectAnchor != null ? effectAnchor.position : transform.position;
        GameSfx.Play(openSfx, pos);   // None 은 GameSfx 가 알아서 무음 처리
        if (openVfxPrefab != null) Instantiate(openVfxPrefab, pos, Quaternion.identity);
    }

    // 파생 구현: open=true 면 "열린(통행 가능)" 모습으로, false 면 "막힌" 모습으로.
    //   instant=true 는 시작 시 초기화 호출(연출 없이 즉시). false 는 실제 전환.
    protected abstract void ApplyState(bool open, bool instant);
}
