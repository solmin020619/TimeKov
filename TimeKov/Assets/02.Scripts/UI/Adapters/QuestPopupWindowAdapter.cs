// =====================================================================
// QuestPopupWindowAdapter.cs
// J키로 여닫는 퀘스트 수락/조회 팝업(GameUIController.questPanel)을
// Window 레이어로 등록.
//
// - 항상 떠 있는 questHud(트래커)는 HUD 레이어 — 이 어댑터와 무관.
// - Window이므로 인벤 등 다른 Window와 동시 표시 가능.
//
// Inspector:
//   windowId = "QuestPopup"
//   layer = Window
//   pausesGame = false
//   locksGameplayInput = true
//   panel = QuestPanel GameObject
// =====================================================================

using UnityEngine;
using TimeKov.UI;

public class QuestPopupWindowAdapter : PanelWindowAdapter
{
    void Reset()
    {
        windowId = "QuestPopup";
        layer = UILayer.Window;
        pausesGame = false;
        locksGameplayInput = true;
    }

    // ESC로 닫혔을 때 GameUIController._currentState도 동기화
    protected override void AfterClose()
    {
        var gui = GameUIController.Instance;
        if (gui != null && gui.GetCurrentState() == GameUIController.UIState.Quest)
            gui.CloseAll();
    }
}
