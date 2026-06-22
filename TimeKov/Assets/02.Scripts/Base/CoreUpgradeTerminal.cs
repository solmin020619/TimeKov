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

    // 튜토리얼: '코어 단말 열기' 퀘스트 활성 시 위치 화살표 안내
    private bool _arrowShown = false;
    private void Update()
    {
        var mgr = TimeKov.UI.HintArrowManager.I;
        if (mgr == null) return;
        bool show = IsCoreOpenQuestActive();
        if (show == _arrowShown) return;
        _arrowShown = show;
        if (show) mgr.Show("core_terminal_hint", transform, 0f);
        else       mgr.Hide("core_terminal_hint");
    }

    private bool IsCoreOpenQuestActive()
    {
        var qm = QuestManager.Instance;
        if (qm == null || !qm.IsReady) return false;
        foreach (var rt in qm.Runtimes)
        {
            if (rt?.activeObjectives == null) continue;
            foreach (var obj in rt.activeObjectives)
                if (obj is CoreOpenObjective && !obj.IsCompleted) return true;
        }
        return false;
    }

    public void Interact(Player player)
    {
        if (player == null) return;

        // F로 방금 닫은 직후(같은~다음 프레임)엔 다시 열지 않음 (한 F 입력이 닫기+재오픈으로 깜빡이는 것 방지)
        if (Time.frameCount - CoreUpgradeUI.LastCloseFrame <= 1) return;

        // 기지 내부 조건 검사
        if (!player.Stat.IsInBase)
        {
            // TODO: 토스트 메시지 출력
            // ToastNotification.Show("기지 내부에서만 강화할 수 있습니다.");
            return;
        }

        // 플레이어 상태 조건 검사
        if (player.Stat.IsDead || player.Stat.IsHurt ||
            player.Skill.IsExecuting || player.Dash.IsDashing)
        {
            return;
        }

        // 코어 강화 UI 오픈
        GameUIController.Instance?.OpenCoreUpgradeUI();
        CoreUpgradeUI.Instance?.Open();
    }
}
