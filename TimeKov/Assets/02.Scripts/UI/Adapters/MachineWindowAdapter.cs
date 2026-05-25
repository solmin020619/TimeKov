// =====================================================================
// MachineWindowAdapter.cs
// 설비(공장) UI를 WindowManager에 등록하는 mirror 어댑터.
//
// MachineInteraction.OpenUI/CloseUI는 F키 진입과 함께 동작하며,
// 그 안에서 GameUIController.OpenFactoryUI/CloseFactoryUI를 이미 호출함.
// 이 어댑터는 그 호출이 WindowManager에도 mirror되도록 등록 책임만 가짐.
//
// 닫기 호출은 WindowManager.Close("Factory") → 어댑터 OnClose →
// MachineInteraction이 ForceClose()로 정리 (이미 GameUIController.GetCurrentState()
// 가 Factory가 아닐 때 자동 ForceClose 코드 있음 — 양쪽 mirror 보장).
//
// Inspector:
//   windowId = "Factory"
//   layer = Window
//   pausesGame = false
//   locksGameplayInput = true
// =====================================================================

using UnityEngine;
using TimeKov.UI;

public class MachineWindowAdapter : MonoBehaviour, IWindow
{
    [SerializeField] string windowId = "Factory";
    [SerializeField] UILayer layer = UILayer.Window;
    [SerializeField] bool pausesGame = false;
    [SerializeField] bool locksGameplayInput = true;

    [Tooltip("닫힐 때 호출할 MachineUI (없으면 닫기 mirror 생략 — MachineInteraction이 자체 감지).")]
    [SerializeField] MachineUI machineUI;

    public UILayer Layer => layer;
    public string  WindowId => windowId;
    public bool    PausesGame => pausesGame;
    public bool    LocksGameplayInput => locksGameplayInput;

    void OnEnable()
    {
        if (WindowManager.I != null)
            WindowManager.I.Register(this);
        else
            StartCoroutine(DelayedRegister());
    }

    void OnDisable()
    {
        WindowManager.I?.Unregister(this);
    }

    System.Collections.IEnumerator DelayedRegister()
    {
        yield return null;
        WindowManager.I?.Register(this);
    }

    public void OnOpen()
    {
        // OpenFor는 어느 머신을 열지 컨텍스트가 필요 → MachineInteraction이 처리.
        // 외부에서 WindowManager.Open("Factory")를 호출하는 케이스는 보통 없음.
        // 호출되면 가까운 머신 자동 선택 같은 로직은 추후 필요 시 추가.
    }

    public void OnClose()
    {
        // MachineInteraction.Update()가 GameUIController.GetCurrentState() != Factory를 감지해 ForceClose 호출하는 경로 외에,
        // WindowManager에서 직접 Close를 요청한 경우의 안전망.
        if (machineUI != null)
            machineUI.Close();
    }
}
