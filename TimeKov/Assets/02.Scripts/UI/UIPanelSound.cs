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
    [Tooltip("비워두면 기본 패널 열기음(GameSfx SfxId.UIPanelOpen) 사용")]
    [SerializeField] private AudioClip overrideOpenClip;

    [Tooltip("비워두면 기본 패널 닫기음(GameSfx SfxId.UIPanelClose) 사용")]
    [SerializeField] private AudioClip overrideCloseClip;

    // 씬 시작 시 OnEnable이 자동 발동되는 것을 방지
    private bool _initialized = false;

    private void Start()
    {
        _initialized = true;
    }

    // [2026-07 사운드 통합] 패널 열닫음은 각 패널을 여는 컨트롤러에서 패널별 SfxId 로 직접 재생하도록 이관했다
    //   (스탯/설정=GameUIController, 창고=InventoryUIController, 전송기=TransmissionComputerUI, 수리=ShipRepairUI …).
    //   이 범용 컴포넌트가 UIPanelOpen/Close 를 내면 새 패널별 사운드와 중복되므로 재생을 비활성화한다.
    //   override 클립을 쓰던 패널이 있으면 그 클립만 유지되도록 아래 분기에서 override 만 재생한다(기본 범용음은 무음).

    private void OnEnable()
    {
        if (!_initialized) return;
        if (overrideOpenClip != null) UISoundManager.Instance?.PlayClip(overrideOpenClip);
    }

    private void OnDisable()
    {
        if (!_initialized) return;
        if (overrideCloseClip != null) UISoundManager.Instance?.PlayClip(overrideCloseClip);
    }
}
