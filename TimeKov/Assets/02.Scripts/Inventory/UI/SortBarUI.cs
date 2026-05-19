// SortBarUI.cs
// WarehousePanel/SortBar 에 붙이는 스크립트
// 정렬 기준 드롭다운과 오름차순/내림차순 버튼 처리

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SortBarUI : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private TMP_Dropdown sortDropdown;     // 정렬 기준 드롭다운
    [SerializeField] private Button orderToggleBtn;   // 오름차순/내림차순 토글 버튼
    [SerializeField] private TextMeshProUGUI orderBtnText;  // 토글 버튼 텍스트

    // 정렬 대상 인벤토리 (InventoryUIController 에서 설정)
    private InventoryManager _targetManager;

    // 현재 정렬 방향 (true = 오름차순)
    private bool _ascending = true;

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

        // 토글 버튼 이벤트 등록
        if (orderToggleBtn != null)
            orderToggleBtn.onClick.AddListener(OnToggleOrder);

        // 초기 버튼 텍스트 설정
        UpdateOrderBtnText();
    }

    // 정렬 대상 인벤토리 설정 (InventoryUIController 에서 호출)
    public void Bind(InventoryManager manager)
    {
        _targetManager = manager;
    }

    // 드롭다운 변경 핸들러
    private void OnDropdownChanged(int index)
    {
        ApplySort();
    }

    // 오름차순/내림차순 토글
    private void OnToggleOrder()
    {
        _ascending = !_ascending;
        UpdateOrderBtnText();
        ApplySort();
    }

    // 현재 설정으로 정렬 실행
    private void ApplySort()
    {
        if (_targetManager == null || sortDropdown == null) return;

        var sortType = (InventoryManager.SortType)sortDropdown.value;
        _targetManager.SortSlots(sortType, _ascending);
    }

    // 토글 버튼 텍스트 갱신
    private void UpdateOrderBtnText()
    {
        if (orderBtnText != null)
            orderBtnText.text = _ascending ? "오름" : "내림";
    }
}