using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 퀵슬롯 한 칸의 설비 아이콘을 관리한다.
/// Quick Slot_png (Image) 오브젝트에 컴포넌트로 추가해 사용.
///
/// [인스펙터 연결]
/// - iconImage  : Quick Slot_png 의 Image 컴포넌트 (자기 자신)
/// - slotNumber : Quick Slot Number 의 TextMeshProUGUI (선택)
/// </summary>
public class QuickSlotIconUI : MonoBehaviour
{
    [Tooltip("설비 아이콘을 표시할 Image (Quick Slot_png)")]
    [SerializeField] private Image iconImage;

    [Tooltip("슬롯 번호 텍스트 (Quick Slot Number). 비워두면 무시.")]
    [SerializeField] private TextMeshProUGUI slotNumber;

    // SetFacility()가 Awake()보다 먼저 호출됐는지 추적
    // (QuickSlotPanel이 비활성일 때 F키로 해금 → 패널 오픈 시 Awake() 실행 순서 역전 방지)
    private bool _facilityAssigned = false;

    // 튜토리얼 "quick_slots" 스포트라이트 — 설비가 들어간(채워진) 슬롯만 강조.
    // 슬롯 박스 = 형제 'Quick Slot' 프레임(실제 보이는 칸). 컨테이너 rect는 번호라벨까지 포함해 더 큼.
    private RectTransform SpotlightRect
    {
        get
        {
            var parent = transform.parent;
            if (parent != null)
            {
                var frame = parent.Find("Quick Slot") as RectTransform;
                if (frame != null) return frame;
                return parent as RectTransform;
            }
            return transform as RectTransform;
        }
    }

    private void Awake()
    {
        // iconImage 미연결 시 자신의 Image 컴포넌트를 자동으로 사용
        if (iconImage == null)
            iconImage = GetComponent<Image>();

        // SetFacility()가 먼저 실행됐으면 Clear() 생략 (패널 오픈 시 Awake 역전 버그 방지)
        if (!_facilityAssigned)
            Clear();
    }

    // ── Public API ────────────────────────────────────────────────

    /// <summary>설비 아이콘을 표시한다.</summary>
    public void SetFacility(int facilityId)
    {
        _facilityAssigned = true;

        // 채워진 슬롯을 튜토리얼 강조 대상으로 등록 (건축투어 "여기서 설비 고르기" 단계가 이 슬롯만 강조)
        TutorialOverlay.RegisterTarget("quick_slots", SpotlightRect);

        // iconImage가 아직 null이면 (Awake 미실행 상태) 지금 바로 찾기
        if (iconImage == null)
            iconImage = GetComponent<Image>();

        // GameObject가 비활성화 상태면 활성화
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        Sprite sprite = FacilityIconDatabase.Instance != null
            ? FacilityIconDatabase.Instance.GetIcon(facilityId)
            : null;

        if (iconImage != null)
        {
            iconImage.sprite  = sprite;
            iconImage.color   = Color.white;
            iconImage.enabled = sprite != null;

            if (sprite == null)
                Debug.LogWarning($"[QuickSlotIconUI] facilityId={facilityId} 아이콘 없음. " +
                                 "FacilityIconDatabase에 등록 필요.");
        }
    }

    /// <summary>슬롯을 비운다 (해금 전 상태).</summary>
    public void Clear()
    {
        _facilityAssigned = false;

        // 빈 슬롯은 튜토리얼 강조 대상에서 제외
        TutorialOverlay.UnregisterTarget("quick_slots", SpotlightRect);

        if (iconImage != null)
        {
            iconImage.sprite  = null;
            iconImage.enabled = false;
        }
    }

    private void OnDestroy()
    {
        // 씬 정리 시 정적 레지스트리에 죽은 rect 잔재 방지
        TutorialOverlay.UnregisterTarget("quick_slots", SpotlightRect);
    }

    /// <summary>슬롯 번호 텍스트 설정 (1-based).</summary>
    public void SetSlotNumber(int oneBased)
    {
        if (slotNumber != null)
            slotNumber.text = oneBased.ToString();
    }
}
