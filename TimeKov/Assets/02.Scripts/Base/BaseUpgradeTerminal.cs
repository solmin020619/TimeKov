// =====================================================================
// BaseUpgradeTerminal.cs
// 기지(핵심 구역) 업그레이드 터미널 상호작용 오브젝트.
// 플레이어가 F키로 상호작용 → 사선 카메라 전환 + 업그레이드 UI 오픈.
// (CoreUpgradeTerminal 과 동일 패턴 — 코어 강화와는 별개 시스템)
// =====================================================================

using UnityEngine;

public class BaseUpgradeTerminal : MonoBehaviour, IInteractable
{
    [Tooltip("기지 내부(결계존)에서만 상호작용 허용.")]
    [SerializeField] private bool requireInBase = true;

    // 항상 표시 — 세부 조건은 Interact 안에서 검사
    public bool CanInteract => true;

    public void Interact(Player player)
    {
        if (player == null) return;

        // F로 방금 닫은 직후(같은~다음 프레임)엔 재오픈 방지 (한 F 입력이 닫기+재오픈으로 깜빡이는 것 방지)
        if (Time.frameCount - BaseUpgradeUI.LastCloseFrame <= 1) return;

        if (requireInBase && !player.Stat.IsInBase) return;

        // 플레이어 상태 조건 (행동 중 열림 방지)
        if (player.Stat.IsDead || player.Stat.IsHurt ||
            player.Skill.IsExecuting || player.Dash.IsDashing)
            return;

        // 패널이 씬에 없으면(빌더 미실행 등) 상태만 바뀌어 HUD가 사라지는 먹통을 막는다.
        if (BaseUpgradeUI.Instance == null)
        {
            Debug.LogWarning("[BaseUpgradeTerminal] BaseUpgradeUI 가 씬에 없음 — 'Tools/TIMEKOV/기지 업그레이드 UI 생성' 메뉴를 실행했는지 확인.");
            return;
        }

        GameUIController.Instance?.OpenBaseUpgradeUI();
        BaseUpgradeUI.Instance.Open();
    }
}
