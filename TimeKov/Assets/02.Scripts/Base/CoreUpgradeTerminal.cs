// =====================================================================
// CoreUpgradeTerminal.cs
// 기지 내부에 배치하는 코어 강화 터미널 상호작용 오브젝트
// 플레이어가 F키로 상호작용 → 코어 강화 UI 오픈
// =====================================================================

using UnityEngine;

public class CoreUpgradeTerminal : MonoBehaviour, IInteractable
{
    // IInteractable — 항상 상호작용 표시 (기지 내부 조건은 Interact 안에서 검사)
    public bool CanInteract => true;

    public void Interact(Player player)
    {
        if (player == null) return;

        // 기지 내부 조건 검사
        if (!player.Stat.IsInBase)
        {
            Debug.Log("[CoreTerminal] 기지 결계 내부에서만 강화할 수 있습니다.");
            // TODO: 토스트 메시지 출력
            // ToastNotification.Show("기지 내부에서만 강화할 수 있습니다.");
            return;
        }

        // 플레이어 상태 조건 검사
        if (player.Stat.IsDead || player.Stat.IsHurt ||
            player.Skill.IsExecuting || player.Dash.IsDashing)
        {
            Debug.Log("[CoreTerminal] 현재 상태에서는 강화할 수 없습니다.");
            return;
        }

        // 코어 강화 UI 오픈
        GameUIController.Instance?.OpenCoreUpgradeUI();
        CoreUpgradeUI.Instance?.Open();
    }
}
