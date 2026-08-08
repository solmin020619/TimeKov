// CategoryFilterUI.cs
//
// 카테고리 필터 탭 줄 (아이콘 전용).
//   선택 탭에 이름을 펼쳐 보여주던 방식은 제거했다. 번역어(프랑스어 등)가 길면
//   탭 7개 + 이름이 패널 폭을 구조적으로 넘어서 양끝 아이콘이 밖으로 밀려났고,
//   압축/말줄임으로 접어 넣어도 서너 글자만 남아 정보가치가 없었다.
//   참고한 레퍼런스도 같은 결론(탭에서 텍스트 제거)이었다.
//   선택된 카테고리 '이름'은 패널 헤더가 담당한다("창고 | 원재료" 형식
//   - MachineUI.UpdateStorageHeaderLabel / InventoryUIController.RefreshPanelTitles).

using System;
using UnityEngine;
using UnityEngine.UI;

public class CategoryFilterUI : MonoBehaviour
{
    // 0: AllFilterBtn
    // 1: RawMaterialBtn     (RawMaterial)
    // 2: ProcessedTier1Btn  (ProcessedTier1)
    // 3: ProcessedTier2Btn  (ProcessedTier2)
    // 4: TacticalBtn        (TacticalConsumable)
    // 5: CoreBtn            (CoreUpgrade)
    // 6: SpecialBtn         (Special)
    [SerializeField] private Button[] filterButtons;

    [SerializeField] private Color selectedColor = new Color(0.30f, 0.60f, 0.90f, 1f);
    [SerializeField] private Color normalColor = new Color(0.20f, 0.25f, 0.35f, 1f);
    [SerializeField] private Color selectedBorderColor = new Color(0.37f, 0.77f, 1f, 1f);    // 선택 탭 테두리 (시안)
    [SerializeField] private Color normalBorderColor = new Color(0.59f, 0.70f, 0.80f, 0.26f); // 비선택 테두리 (크롬)
    [SerializeField] private Color selectedIconColor = new Color(0.68f, 0.89f, 1f, 1f);       // 선택 아이콘 (밝은 시안)
    [SerializeField] private Color normalIconColor = new Color(0.76f, 0.83f, 0.90f, 1f);      // 비선택 아이콘

    [Header("탭 배치")]
    [SerializeField] private float collapsedWidth = 58f;   // 탭 폭 (아이콘만)
    [SerializeField] private float tabGap = 8f;            // 탭 간격

    // 탭 이름 (인덱스 = filterButtons 순서). 탭엔 안 그리지만 헤더 표시용으로 유지.
    private static readonly string[] TabNames = { "전체", "원재료", "1차 가공품", "2차 가공품", "전술 소모품", "핵심 강화", "특수" };

    public event Action<ItemCategory?> OnFilterChanged;

    private ItemCategory? _current = null;

    public ItemCategory? CurrentFilter => _current;

    /// <summary>현재 선택 카테고리의 표시 이름(번역 적용). 패널 헤더가 "창고 | 이름" 을 만들 때 쓴다.</summary>
    public string CurrentFilterName
        => Loc.Get(_selectedIndex >= 0 && _selectedIndex < TabNames.Length ? TabNames[_selectedIndex] : TabNames[0]);

    private int _selectedIndex = 0;

    private static readonly ItemCategory?[] IndexToCategory = new ItemCategory?[]
    {
        null,
        ItemCategory.RawMaterial,
        ItemCategory.ProcessedTier1,
        ItemCategory.ProcessedTier2,
        ItemCategory.TacticalConsumable,
        ItemCategory.CoreUpgrade,
        ItemCategory.Special
    };

    private void Start()
    {
        if (filterButtons == null || filterButtons.Length == 0) return;

        for (int i = 0; i < filterButtons.Length; i++)
        {
            if (filterButtons[i] == null) continue;
            int capturedIndex = i;
            filterButtons[i].onClick.AddListener(() => SetFilterByIndex(capturedIndex));
        }

        UpdateButtonColors();
        InitLayout();
    }

    public void SetFilterByIndex(int index)
    {
        if (index < 0 || index >= IndexToCategory.Length) return;

        _selectedIndex = index;
        _current = IndexToCategory[index];

        UpdateButtonColors();
        OnFilterChanged?.Invoke(_current);
    }

    // 외부에서 특정 카테고리 탭으로 전환 (입고 시 옮긴 아이템 카테고리로 자동 점프).
    // null = 전체 탭. 매칭 탭이 없으면 아무 것도 안 함.
    public void SelectByCategory(ItemCategory? cat)
    {
        if (cat == null) { SetFilterByIndex(0); return; }
        for (int i = 0; i < IndexToCategory.Length; i++)
        {
            if (IndexToCategory[i] == cat) { SetFilterByIndex(i); return; }
        }
    }

    // 탭을 등폭으로 중앙 정렬하고, 예전 펼침용 이름 라벨(CatLabel)은 꺼 둔다.
    //   라벨 오브젝트는 씬/프리팹에 남아 있으므로 안 끄면 이전 상태에 따라 글자가 남을 수 있다.
    private void InitLayout()
    {
        int n = filterButtons.Length;
        float total = collapsedWidth * n + tabGap * (n - 1);
        float x = -total / 2f;
        for (int i = 0; i < n; i++)
        {
            if (filterButtons[i] == null) { x += collapsedWidth + tabGap; continue; }

            var label = filterButtons[i].transform.Find("CatLabel");
            if (label != null) label.gameObject.SetActive(false);

            var rt = filterButtons[i].GetComponent<RectTransform>();
            if (rt != null)
            {
                var ap = rt.anchoredPosition; ap.x = x + collapsedWidth / 2f; rt.anchoredPosition = ap;
                var sd = rt.sizeDelta; sd.x = collapsedWidth; rt.sizeDelta = sd;
            }
            x += collapsedWidth + tabGap;
        }
    }

    private void UpdateButtonColors()
    {
        if (filterButtons == null) return;

        for (int i = 0; i < filterButtons.Length; i++)
        {
            if (filterButtons[i] == null) continue;
            bool sel = (i == _selectedIndex);

            var img = filterButtons[i].GetComponent<Image>();
            if (img != null) img.color = sel ? selectedColor : normalColor;

            // 테두리(Outline) 색 전환
            var ol = filterButtons[i].GetComponent<UnityEngine.UI.Outline>();
            if (ol != null) ol.effectColor = sel ? selectedBorderColor : normalBorderColor;

            // 아이콘 색 전환 (자식 CatIcon)
            var iconTr = filterButtons[i].transform.Find("CatIcon");
            if (iconTr != null)
            {
                var iconImg = iconTr.GetComponent<Image>();
                if (iconImg != null) iconImg.color = sel ? selectedIconColor : normalIconColor;
            }
        }
    }

    public void ResetToAll()
    {
        SetFilterByIndex(0);
    }
}
