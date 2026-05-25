// =====================================================================
// PanelWindowAdapter.cs
// "GameObject 하나만 켜고 끄는" 단순 UI 창용 공통 어댑터 기반 클래스.
// SettingsModalAdapter / QuestPopupWindowAdapter / PlayerStatWindowAdapter 등의 공통 베이스.
//
// 책임:
//   - WindowManager에 자기 자신 등록/해제 (OnEnable/OnDisable)
//   - WindowManager.Open → OnOpen에서 panel.SetActive(true)
//   - WindowManager.Close → OnClose에서 panel.SetActive(false)
//   - UIUnfoldEffect가 있으면 닫기 애니메이션 자동 적용
//
// 사용 예: 빈 GO에 SettingsModalAdapter 부착 → panel에 SettingsPanel 드래그 → 끝.
// =====================================================================

using UnityEngine;
using TimeKov.UI;

public abstract class PanelWindowAdapter : MonoBehaviour, IWindow
{
    [Header("Window Identity")]
    [Tooltip("창 고유 ID. WindowManager.Open(id) / OpenPolicy에서 참조. 비우면 등록 안 됨.")]
    [SerializeField] protected string windowId;

    [Tooltip("UI 레이어. sortingOrder · 동시 표시 정책에 영향.")]
    [SerializeField] protected UILayer layer = UILayer.Window;

    [Tooltip("열려있는 동안 Time.timeScale = 0. 보통 Modal만 true.")]
    [SerializeField] protected bool pausesGame = false;

    [Tooltip("열려있는 동안 플레이어 입력 차단 + 커서 표시. Window 이상 보통 true.")]
    [SerializeField] protected bool locksGameplayInput = true;

    [Header("Panel")]
    [Tooltip("Open/Close 시 SetActive를 적용할 패널 GameObject. 비우면 OnOpen/OnClose에서 패널 조작 안 함.")]
    [SerializeField] protected GameObject panel;

    public UILayer Layer => layer;
    public string  WindowId => windowId;
    public bool    PausesGame => pausesGame;
    public bool    LocksGameplayInput => locksGameplayInput;

    protected virtual void OnEnable()
    {
        // WindowManager가 늦게 생성될 수 있어 한 프레임 지연 후에도 시도
        if (WindowManager.I != null)
            WindowManager.I.Register(this);
        else
            StartCoroutine(DelayedRegister());
    }

    protected virtual void OnDisable()
    {
        WindowManager.I?.Unregister(this);
    }

    System.Collections.IEnumerator DelayedRegister()
    {
        yield return null;
        if (WindowManager.I != null)
            WindowManager.I.Register(this);
    }

    public virtual void OnOpen()
    {
        if (panel != null && !panel.activeSelf)
            panel.SetActive(true);
        AfterOpen();
    }

    public virtual void OnClose()
    {
        if (panel != null)
        {
            // UIUnfoldEffect가 있으면 애니메이션 후 SetActive(false)
            var unfold = panel.GetComponent<UIUnfoldEffect>();
            if (unfold != null && panel.activeInHierarchy)
                unfold.Close();
            else if (panel.activeSelf)
                panel.SetActive(false);
        }
        AfterClose();
    }

    /// <summary>Open 후 추가 정리 훅. 상속 클래스에서 override (예: GameUIController 상태 동기화).</summary>
    protected virtual void AfterOpen() { }

    /// <summary>Close 후 추가 정리 훅. 상속 클래스에서 override (예: GameUIController._currentState=None 동기화).</summary>
    protected virtual void AfterClose() { }
}
