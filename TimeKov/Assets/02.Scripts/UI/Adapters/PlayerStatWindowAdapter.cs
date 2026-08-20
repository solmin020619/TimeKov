// =====================================================================
// PlayerStatWindowAdapter.cs
// C키로 여닫는 플레이어 스탯 패널 어댑터.
//
// 정보 전용 패널이라 게임 일시정지/입력 차단은 안 함.
// 다른 Window(인벤·공장)와 자유롭게 공존 가능.
//
// Inspector:
//   windowId = "PlayerStat"
//   layer = Window
//   pausesGame = false
//   locksGameplayInput = false   ← 정보용이라 게임 진행 그대로
//   panel = StatPanel GameObject
// =====================================================================

using UnityEngine;
using TimeKov.UI;

public class PlayerStatWindowAdapter : PanelWindowAdapter
{
    void Reset()
    {
        windowId = "PlayerStat";
        layer = UILayer.Window;
        pausesGame = false;
        locksGameplayInput = false;
    }

    // ── 여닫는 연출 ──────────────────────────────────────────────────────
    // 설정창과 같은 연출(MenuPanelAnim)을 쓴다. 기본 구현은 SetActive 로 툭 켜고 끈다.
    //
    // ★여기서도 처리해야 한다. ESC 는 GameUIController.TogglePlayerStat 을 안 거치고
    //   WindowManager.HandleEscape → 이 어댑터로 바로 들어온다. C 키만 고치면
    //   여는 건 부드러운데 ESC 로 닫을 때만 툭 사라진다.
    //
    // ★이미 그 상태면 아무것도 안 한다. C 키 경로는 MenuPanelAnim 을 먼저 부르고
    //   WindowManager 에 알리는 순서라, 가드가 없으면 연출이 두 번 시작되어 깜빡인다.

    public override void OnOpen()
    {
        if (panel != null && !MenuPanelAnim.IsOpen(panel)) MenuPanelAnim.Open(panel);
        AfterOpen();   // 훅은 어느 경로로 왔든 부른다(기본 구현과 같은 계약)
    }

    public override void OnClose()
    {
        if (panel != null && MenuPanelAnim.IsOpen(panel)) MenuPanelAnim.Close(this, panel);
        AfterClose();
    }
}
