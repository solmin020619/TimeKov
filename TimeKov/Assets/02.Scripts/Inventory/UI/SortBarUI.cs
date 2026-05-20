// SortBarUI.cs
// WarehouseBottomBar 에 붙이는 스크립트
// 정렬 기준 드롭다운, 오름/내림 토글, 창고 정리 버튼 처리

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SortBarUI : MonoBehaviour
{
    [Header("정렬 참조")]
    [SerializeField] private TMP_Dropdown sortDropdown;
    [SerializeField] private Button orderToggleBtn;
    [SerializeField] private TextMeshProUGUI orderBtnText;

    [Header("정리 버튼")]
    [SerializeField] private Button organizeBtn;

    // 현재 정렬 방향 (true = 오름차순)
    private bool _ascending = true;

    // 정렬 대상 인벤토리 (InventoryUIController 에서 설정)
    private InventoryManager _targetManager;

    // 현재 창고 필터 접근용 (InventoryUIController 에서 설정)
    private CategoryFilterUI _warehouseFilter;

    private void Start()
    {
        // 드롭다운 옵션 초기화
        if (sortDropdown != null)
        {
            sortDropdown.ClearOptions();
            sortDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "이름순",
                "카테고리순",
                "등급순",
                "수량순"
            });
            sortDropdown.onValueChanged.AddListener(OnDropdownChanged);
        }

        if (orderToggleBtn != null)
            orderToggleBtn.onClick.AddListener(OnToggleOrder);

        if (organizeBtn != null)
            organizeBtn.onClick.AddListener(OnClickOrganize);

        UpdateOrderBtnText();
    }

    // 정렬 대상 및 필터 바인딩
    public void Bind(InventoryManager manager, CategoryFilterUI filterUI)
    {
        _targetManager = manager;
        _warehouseFilter = filterUI;
    }

    private void OnDropdownChanged(int index)
    {
        ApplySort();
    }

    private void OnToggleOrder()
    {
        _ascending = !_ascending;
        UpdateOrderBtnText();
        ApplySort();
    }

    // 드롭다운 기준으로 정렬 실행
    private void ApplySort()
    {
        if (_targetManager == null || sortDropdown == null) return;

        var sortType = (InventoryManager.SortType)sortDropdown.value;
        _targetManager.SortSlots(sortType, _ascending);
    }

    // 정리 버튼: 현재 창고 필터 기준으로 병합 + 정렬
    private void OnClickOrganize()
    {
        if (_targetManager == null) return;

        var filter = _warehouseFilter != null ? _warehouseFilter.CurrentFilter : null;
        _targetManager.OrganizeFiltered(filter);
    }

    private void UpdateOrderBtnText()
    {
        if (orderBtnText != null)
            orderBtnText.text = _ascending ? "오름" : "내림";
    }
}