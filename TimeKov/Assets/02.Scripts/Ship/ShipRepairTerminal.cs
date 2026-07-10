using UnityEngine;

// 폐우주선(공사 펜스) 상호작용 오브젝트. F -> 다음 단계 수리 시도.
// UI는 추후 작업 — 지금은 로직 검증용으로 직접 수리한다. UI 완성 시 이 자리를 패널 오픈으로 교체.
// (BaseUpgradeTerminal 과 동일 패턴)
public class ShipRepairTerminal : MonoBehaviour, IInteractable
{
    // 다음 단계 수리 부품을 회수했을 때만 F 후보로 켜진다.
    // 평소(부품 없음)엔 F 풀에서 빠져 있어 공장 설비 상호작용과 안 겹친다.
    public bool CanInteract =>
        ShipRepairManager.Instance != null && ShipRepairManager.Instance.CanRepairNext();

    public void Interact(Player player)
    {
        if (player == null) return;

        // 행동 중 열림 방지
        if (player.Stat.IsDead || player.Stat.IsHurt ||
            player.Skill.IsExecuting || player.Dash.IsDashing)
            return;

        // F로 방금 닫은 직후 재오픈 방지(한 F가 닫기+재오픈으로 깜빡이는 것 방지)
        if (Time.frameCount - ShipRepairUI.LastCloseFrame <= 1) return;

        var mgr = ShipRepairManager.Instance;
        if (mgr == null)
        {
            Debug.LogWarning("[ShipRepairTerminal] ShipRepairManager 가 씬에 없음.");
            return;
        }

        // UI 패널이 있으면(꺼져 있어도 EnsureInstance 가 활성화) 열고, 없으면(빌더 미실행) 즉시 수리 폴백.
        var ui = ShipRepairUI.EnsureInstance();
        if (ui != null)
        {
            GameUIController.Instance?.OpenShipRepairUI();
            ui.Open();
        }
        else
        {
            mgr.TryRepairNext();
        }
    }
}
