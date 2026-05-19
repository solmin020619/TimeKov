// CategoryFilterUI.cs
// FilterBar 오브젝트에 붙이는 스크립트
// 카테고리 버튼 클릭 시 InventoryGridUI 에 필터 변경 알림
// null 이면 전체 표시, 값이 있으면 해당 카테고리만 표시

using System;
using UnityEngine;
using UnityEngine.UI;

public class CategoryFilterUI : MonoBehaviour
{
    // 버튼 배열 (Inspector 에서 순서대로 할당)
    // 0: AllFilterBtn
    // 1: RawMaterialBtn     (RawMaterial)
    // 2: ProcessedTier1Btn  (ProcessedTier1)
    // 3: ProcessedTier2Btn  (ProcessedTier2)
    // 4: TacticalBtn        (TacticalConsumable)
    // 5: CoreBtn            (CoreUpgrade)
    // 6: SpecialBtn         (Special)
    [Header("카테고리 버튼 (0번: 전체, 1~6번: 카테고리 순서대로)")]
    [SerializeField] private Button[] filterButtons;

    [Header("색상")]
    [SerializeField] private Color selectedColor = new Color(0.30f, 0.60f, 0.90f, 1f);
    [SerializeField] private Color normalColor = new Color(0.20f, 0.25f, 0.35f, 1f);

    // 필터 변경 이벤트
    // null 이면 전체, 값이 있으면 해당 카테고리
    public event Action<ItemCategory?> OnFilterChanged;

    // 현재 선택된 카테고리 (null = 전체)
    private ItemCategory? _current = null;

    // 현재 선택된 버튼 인덱스 (0 = 전체)
    private int _selectedIndex = 0;

    // 카테고리 버튼 인덱스와 실제 ItemCategory 값 매핑 테이블
    // filterButtons[0] = 전체(null), filterButtons[1] = RawMaterial, ...
    private static readonly ItemCategory?[] IndexToCategory = new ItemCategory?[]
    {
        null,                              // 0: 전체
        ItemCategory.RawMaterial,          // 1: 원초 재료
        ItemCategory.ProcessedTier1,       // 2: 1차 가공품
        ItemCategory.ProcessedTier2,       // 3: 2차 심화 가공품
        ItemCategory.TacticalConsumable,   // 4: 전술 소모품
        ItemCategory.CoreUpgrade,          // 5: 코어 강화 재료
        ItemCategory.Special               // 6: 특수
    };

    private void Start()
    {
        if (filterButtons == null || filterButtons.Length == 0) return;

        // 버튼마다 인덱스 기반 클릭 이벤트 등록
        for (int i = 0; i < filterButtons.Length; i++)
        {
            if (filterButtons[i] == null) continue;
            int capturedIndex = i;
            filterButtons[i].onClick.AddListener(() => SetFilterByIndex(capturedIndex));
        }

        // 첫 번째 버튼(전체) 선택 상태로 초기화
        UpdateButtonColors();
    }

    // 버튼 인덱스로 필터 설정
    public void SetFilterByIndex(int index)
    {
        if (index < 0 || index >= IndexToCategory.Length) return;

        _selectedIndex = index;
        _current = IndexToCategory[index];

        UpdateButtonColors();
        OnFilterChanged?.Invoke(_current);
    }

    // 버튼 색상 갱신
    private void UpdateButtonColors()
    {
        if (filterButtons == null) return;

        for (int i = 0; i < filterButtons.Length; i++)
        {
            if (filterButtons[i] == null) continue;

            var img = filterButtons[i].GetComponent<Image>();
            if (img == null) continue;

            img.color = (i == _selectedIndex) ? selectedColor : normalColor;
        }
    }

    // 외부에서 전체 필터 초기화 (인벤토리 닫을 때 호출)
    public void ResetToAll()
    {
        SetFilterByIndex(0);
    }
}