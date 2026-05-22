// UIPanelSound.cs
// UI 패널에 붙이는 열기/닫기 사운드 컴포넌트
// 패널이 켜질 때(OnEnable) → 열기 소리, 꺼질 때(OnDisable) → 닫기 소리 자동 재생
//
// [사용법]
//   1. 인벤토리, 설비UI, 설정창 등 각 패널 루트 오브젝트에 이 컴포넌트 추가
//   2. 클립 비워두면 UISoundManager의 기본 panelOpenClip / panelCloseClip 사용
//   3. 패널마다 다른 소리를 쓰고 싶으면 override 필드에 클립 할당

using UnityEngine;

public class UIPanelSound : MonoBehaviour
{
    [Tooltip("비워두면 UISoundManager.panelOpenClip 사용")]
    [SerializeField] private AudioClip overrideOpenClip;

    [Tooltip("비워두면 UISoundManager.panelCloseClip 사용")]
    [SerializeField] private AudioClip overrideCloseClip;

    // 씬 시작 시 OnEnable이 자동 발동되는 것을 방지
    private bool _initialized = false;

    private void Start()
    {
        _initialized = true;
    }

    private void OnEnable()
    {
        // Start() 이전에는 무시 (씬 로드 시 자동 발동 방지)
        if (!_initialized) return;

        var mgr = UISoundManager.Instance;
        if (mgr == null) return;

        if (overrideOpenClip != null)
            mgr.PlayClip(overrideOpenClip);
        else
            mgr.PlayPanelOpen();
    }

    private void OnDisable()
    {
        if (!_initialized) return;

        var mgr = UISoundManager.Instance;
        if (mgr == null) return;

        if (overrideCloseClip != null)
            mgr.PlayClip(overrideCloseClip);
        else
            mgr.PlayPanelClose();
    }
}
