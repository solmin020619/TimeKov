// =====================================================================
// InventoryWindowAdapter.cs
// 인벤토리(다른 팀원 담당, 미완성)를 WindowManager에 등록하는 mirror 어댑터.
//
// 정책상 Inventory 폴더 코드는 직접 수정 금지 — 외부에서 InventoryUIController.Instance
// 의 public Open/Close만 호출.
//
// 책임:
//   - WindowManager.Open("Inventory") → InventoryUIController.Open()
//   - WindowManager.Close("Inventory") → InventoryUIController.Close()
//   - 양쪽 호출 진입점 어디서든 mirror 동작
//
// Inspector:
//   windowId = "Inventory"
//   layer = Window
//   pausesGame = false
//   locksGameplayInput = true
// =====================================================================

using UnityEngine;
using TimeKov.UI;

public class InventoryWindowAdapter : MonoBehaviour, IWindow
{
    [SerializeField] string windowId = "Inventory";
    [SerializeField] UILayer layer = UILayer.Window;
    [SerializeField] bool pausesGame = false;
    [SerializeField] bool locksGameplayInput = true;

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

    // 재진입 가드 — InventoryUIController.Open이 GameUIController.SetState를 호출하고
    // 그게 다시 WindowManager.Open을 호출하면 무한루프 방지
    static bool _inOpenCall;
    static bool _inCloseCall;

    public void OnOpen()
    {
        if (_inOpenCall) return;
        if (InventoryUIController.Instance == null) return;

        _inOpenCall = true;
        try { InventoryUIController.Instance.Open(); }
        finally { _inOpenCall = false; }
    }

    public void OnClose()
    {
        if (_inCloseCall) return;
        if (InventoryUIController.Instance == null) return;

        _inCloseCall = true;
        try { InventoryUIController.Instance.Close(); }
        finally { _inCloseCall = false; }
    }
}
