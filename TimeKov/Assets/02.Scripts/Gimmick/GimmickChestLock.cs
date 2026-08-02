using UnityEngine;

// ── 기믹 타깃: '상자 잠금'을 푸는 대상 ────────────────────────────────────────
// SwitchTrigger(스위치 여러 개) / 에너지 노드 등 어떤 GimmickTrigger 든 이걸 열면,
//   연결된 상자(gimmickLocked 로 완전 잠금 시작)들이 열 수 있게 풀린다.
//   상자(ChestInteractable)는 자체 상태머신이 있어 GimmickTarget 이 될 수 없다 → 이 컴포넌트가
//   "트리거 ↔ 상자"를 이어주는 얇은 어댑터. SwitchTrigger 의 targets 에 이걸 넣으면 된다.
//
//   ★보통은 상자 오브젝트에 이 컴포넌트를 그냥 같이 붙이면 된다(별도 오브젝트 불필요).
//     Chests 를 비워두면 같은/자식 오브젝트의 ChestInteractable 을 자동으로 잡는다.
//   예) 스위치 2개 → SwitchTrigger(requireAll) → targets 에 이 GimmickChestLock → 상자 잠금 해제.
public class GimmickChestLock : GimmickTarget
{
    [Header("잠금 해제할 상자들 (비우면 이 오브젝트/자식의 상자 자동 사용)")]
    [SerializeField] private ChestInteractable[] chests;

    protected override void Start()
    {
        // 지정 안 했으면 같은 오브젝트(및 자식)의 상자를 자동으로 잡는다 → 상자에 그냥 붙이기만 하면 됨.
        if (chests == null || chests.Length == 0)
            chests = GetComponentsInChildren<ChestInteractable>(true);
        base.Start();
    }

    protected override void ApplyState(bool open, bool instant)
    {
        // 열릴 때만 상자를 푼다. (다시 잠그는 동작은 없음 — 한 번 풀면 끝)
        if (!open || chests == null) return;
        foreach (var c in chests)
            if (c != null) c.GimmickUnlock();
    }
}
