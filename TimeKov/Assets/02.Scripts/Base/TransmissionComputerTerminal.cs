// =====================================================================
// TransmissionComputerTerminal.cs
// 기지 코어 건물 내부에 배치하는 '전송 컴퓨터' 상호작용 오브젝트 (기획서 §11)
// 플레이어가 F키로 상호작용 → 전송 컴퓨터 UI 오픈.
// 코어 강화 단말(CoreUpgradeTerminal)과 구분되는 별도 오브젝트.
// =====================================================================

using UnityEngine;

public class TransmissionComputerTerminal : MonoBehaviour, IInteractable
{
    public bool CanInteract => true;

    public void Interact(Player player)
    {
        if (player == null) return;

        // F로 방금 닫은 직후엔 다시 열지 않음 (한 F 입력이 닫기+재오픈으로 깜빡이는 것 방지)
        if (Time.frameCount - TransmissionComputerUI.LastCloseFrame <= 1) return;

        // 기지 내부에서만 사용 가능
        if (!player.Stat.IsInBase) return;

        // 플레이어 상태 조건 검사 (강화 단말과 동일)
        if (player.Stat.IsDead || player.Stat.IsHurt ||
            player.Skill.IsExecuting || player.Dash.IsDashing)
            return;

        // 전송 컴퓨터 UI 오픈 (없으면 런타임 생성)
        GameUIController.Instance?.OpenTransmissionUI();
        TransmissionComputerUI.GetOrCreate().Open();
    }
}
