// =====================================================================
// SettingsModalAdapter.cs
// 설정창(ESC로 열리는 패널)을 Modal 레이어로 등록.
// - WindowManager.Open("Settings") / Close("Settings") 진입점
// - Modal이므로 게임 일시정지 + 입력 차단 + (Blocker prefab 있으면) 뒤 클릭 차단
//
// 부착 위치: Settings Panel GameObject 자체 또는 옆 GO에 부착.
// Inspector 설정:
//   windowId = "Settings"
//   layer    = Modal
//   pausesGame = true
//   locksGameplayInput = true
//   panel    = SettingsPanel GameObject
// =====================================================================

using UnityEngine;
using TimeKov.UI;

public class SettingsModalAdapter : PanelWindowAdapter
{
    void Reset()
    {
        // 인스펙터에서 컴포넌트 처음 부착할 때 기본값
        windowId = "Settings";
        layer = UILayer.Modal;
        pausesGame = true;
        locksGameplayInput = true;
    }

    // ESC로 닫혔을 때 GameUIController._currentState도 None으로 동기화
    // (SetState → mirror Close는 IsOpen false라 재진입 안전)
    protected override void AfterClose()
    {
        var gui = GameUIController.Instance;
        if (gui != null && gui.GetCurrentState() == GameUIController.UIState.Settings)
            gui.CloseSettings();
    }

    // ESC(WindowManager.defaultEscapeWindowId)처럼 GlobalSettingsManager.OpenSettings()를
    // 거치지 않고 패널이 열리는 경로도 있어, 여기서 항상 _pending 리셋 + UI 동기화를 보장한다.
    // (안 하면 직전에 적용 안 하고 닫은 변경사항이 _pending/_isDirty에 그대로 남아있어
    //  마치 저장 안 한 변경이 "적용된 것처럼" 다시 보이는 버그가 생김)
    protected override void AfterOpen()
    {
        GlobalSettingsManager.Instance?.RefreshOnOpen();
    }
}
