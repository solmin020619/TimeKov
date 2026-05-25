// =====================================================================
// BuildModeWindowAdapter.cs
// 건축 모드를 WindowManager에 등록하는 마커 어댑터.
//
// 빌드 모드는 패널 SetActive가 아니라 카메라/입력 전환이라 OnOpen/OnClose에서
// 직접 처리하지 않음. 진입/종료는 BuildManager가 자체 책임.
//
// 어댑터 부착 효과:
//   - GameUIController.SetState(Build) → WindowManager.Open("BuildMode") 가 정상 등록 처리됨
//     (어댑터 없으면 LogWarning 발생)
//   - OpenPolicy에서 "BuildMode 열 때 Inventory 닫기" 같은 룰 등록 가능
//   - 추후 빌드 모드 ESC 통합 시 WindowManager.Close 디스패치 대상이 됨
//
// 다음 단계(빌드 모드 정리)에서 BuildManager에 EnterBuildMode/ExitBuildMode public 메서드를
// 추가하면 이 어댑터의 OnOpen/OnClose가 BuildManager 호출로 채워질 예정.
// =====================================================================

using UnityEngine;
using TimeKov.UI;

public class BuildModeWindowAdapter : MonoBehaviour, IWindow
{
    [SerializeField] string windowId = "BuildMode";
    [SerializeField] UILayer layer = UILayer.Window;

    [Tooltip("빌드 모드 진입 시 시간 정지 — 보통 false")]
    [SerializeField] bool pausesGame = false;

    [Tooltip("빌드 모드 진입 시 플레이어 이동 입력 차단 + 커서 표시. true 권장 — BuildManager의 Cursor 직접 조작 제거 이후 이 어댑터가 처리.")]
    [SerializeField] bool locksGameplayInput = true;

    [Tooltip("닫기 호출 시 BuildManager.ExitBuildMode를 호출하려면 여기에 참조. " +
             "지금은 호출 안 됨 — 다음 단계에서 BuildManager 정리 후 활성화 예정.")]
    [SerializeField] BuildManager buildManager;

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
        // WindowManager.Open("BuildMode") 진입점 — BuildManager로 위임
        // (B 키 진입 흐름은 BuildManager가 직접 EnterBuildMode를 호출 → mirror로 WindowManager.Open)
        if (buildManager != null) buildManager.EnterBuildMode();
    }

    public void OnClose()
    {
        // WindowManager.Close("BuildMode") 진입점 — BuildManager로 위임
        // ESC 디스패치 흐름의 종착점. 우클릭/B키 종료는 BuildManager가 직접 ExitBuildMode 호출.
        if (buildManager != null) buildManager.ExitBuildMode();
    }
}
