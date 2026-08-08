// CategoryFilterUI.cs

using System;
using System.Collections;
using TMPro;
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

    [Header("탭 펼침 (선택 시 이름 표시 + 양옆 밀림)")]
    [SerializeField] private float collapsedWidth = 58f;   // 접힘(아이콘만)
    [SerializeField] private float tabGap = 8f;            // 탭 간격 (빌더 gap과 동일)
    [SerializeField] private float expandDuration = 0.2f;  // 펼침 애니 시간(초)

    // 탭 이름 (인덱스 = filterButtons 순서)
    private static readonly string[] TabNames = { "전체", "원재료", "1차 가공품", "2차 가공품", "전술 소모품", "핵심 강화", "특수" };
    private RectTransform[] _tabRects;
    private TextMeshProUGUI[] _tabLabels;
    private float[] _expandedWidth;
    private Coroutine _expandCo;

    // 좁은 컨테이너(공장 설비 창고 섹션)에서만 끈다. 프랑스어처럼 이름이 길면
    // 탭 7개 + 이름이 패널 폭을 넘어 양끝 아이콘이 밖으로 밀려나기 때문.
    // 그 화면은 헤더가 "창고 | 원재료" 로 선택 카테고리를 이미 보여준다.
    // 넓은 화면(인벤토리/창고 출력 포트)은 켠 채로 둔다 - 기본값 true.
    private bool _showTabNames = true;

    public event Action<ItemCategory?> OnFilterChanged;

    private ItemCategory? _current = null;

    public ItemCategory? CurrentFilter => _current;

    /// <summary>현재 선택 카테고리의 표시 이름(번역 적용). 헤더가 "창고 | 원재료" 를 만들 때 쓴다.</summary>
    public string CurrentFilterName
        => Loc.Get(_selectedIndex >= 0 && _selectedIndex < TabNames.Length ? TabNames[_selectedIndex] : TabNames[0]);

    /// <summary>탭 이름 펼침을 끈다(아이콘 전용). 폭이 좁은 화면에서 클론 직후 호출한다.</summary>
    public void SetTabNamesVisible(bool visible)
    {
        if (_showTabNames == visible) return;
        _showTabNames = visible;
        if (_tabRects != null) ApplyLayoutInstant(_selectedIndex);
    }

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
        InitExpandLayout();   // 탭 이름 캐싱 + 초기 펼침 배치
    }

    private void OnEnable()  => Loc.OnLanguageChanged += RefreshTabLabels;
    private void OnDisable() => Loc.OnLanguageChanged -= RefreshTabLabels;

    private void RefreshTabLabels()
    {
        if (_tabLabels == null) return;
        for (int i = 0; i < _tabLabels.Length; i++)
        {
            if (_tabLabels[i] == null) continue;
            string nm = (i < TabNames.Length) ? Loc.Get(TabNames[i]) : "";
            _tabLabels[i].text = nm;
            float w = _tabLabels[i].GetPreferredValues(nm).x;
            if (w < 1f) w = nm.Length * 26f;
            _expandedWidth[i] = 56f + w + 16f;
        }
        ApplyLayoutInstant(_selectedIndex);
    }

    public void SetFilterByIndex(int index)
    {
        if (index < 0 || index >= IndexToCategory.Length) return;

        _selectedIndex = index;
        _current = IndexToCategory[index];

        UpdateButtonColors();
        AnimateSelection(index);   // 선택 탭 이름 펼침 + 양옆 밀림
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

    // ── 탭 펼침(이름 표시 + 양옆 밀림) ──────────────────────────────
    private void InitExpandLayout()
    {
        if (filterButtons == null) return;
        int n = filterButtons.Length;
        _tabRects = new RectTransform[n];
        _tabLabels = new TextMeshProUGUI[n];
        _expandedWidth = new float[n];

        for (int i = 0; i < n; i++)
        {
            if (filterButtons[i] == null) { _expandedWidth[i] = collapsedWidth; continue; }
            _tabRects[i] = filterButtons[i].GetComponent<RectTransform>();

            var lt = filterButtons[i].transform.Find("CatLabel");
            _tabLabels[i] = lt != null ? lt.GetComponent<TextMeshProUGUI>() : null;

            if (_tabLabels[i] != null)
            {
                string nm = (i < TabNames.Length) ? Loc.Get(TabNames[i]) : "";
                _tabLabels[i].text = nm;
                float w = _tabLabels[i].GetPreferredValues(nm).x;
                if (w < 1f) w = nm.Length * 26f;   // 폰트 미준비 폴백(한글 넉넉히)
                _expandedWidth[i] = 56f + w + 16f; // 아이콘영역(56) + 글자 + 우측여백
                var c = _tabLabels[i].color; c.a = 0f; _tabLabels[i].color = c;
            }
            else
            {
                _expandedWidth[i] = collapsedWidth;
            }
        }
        ApplyLayoutInstant(_selectedIndex);
    }

    private void LayoutWidths(float[] widths)
    {
        if (_tabRects == null) return;
        int n = _tabRects.Length;
        float total = 0f;
        for (int i = 0; i < n; i++) total += widths[i];
        total += tabGap * (n - 1);

        float x = -total / 2f;
        for (int i = 0; i < n; i++)
        {
            float w = widths[i];
            if (_tabRects[i] != null)
            {
                var rt = _tabRects[i];
                var ap = rt.anchoredPosition; ap.x = x + w / 2f; rt.anchoredPosition = ap;
                var sd = rt.sizeDelta; sd.x = w; rt.sizeDelta = sd;
            }
            x += w + tabGap;
        }
    }

    private void ApplyLayoutInstant(int sel)
    {
        if (_tabRects == null) return;
        int n = _tabRects.Length;
        var widths = new float[n];
        for (int i = 0; i < n; i++)
            widths[i] = (_showTabNames && i == sel) ? _expandedWidth[i] : collapsedWidth;
        LayoutWidths(widths);
        for (int i = 0; i < n; i++)
            if (_tabLabels[i] != null)
            {
                var c = _tabLabels[i].color; c.a = (_showTabNames && i == sel) ? 1f : 0f; _tabLabels[i].color = c;
            }
    }

    private void AnimateSelection(int sel)
    {
        if (_tabRects == null) return;
        if (!isActiveAndEnabled || !_showTabNames) { ApplyLayoutInstant(sel); return; }
        if (_expandCo != null) { StopCoroutine(_expandCo); _expandCo = null; }
        _expandCo = StartCoroutine(ExpandRoutine(sel));
    }

    private IEnumerator ExpandRoutine(int sel)
    {
        int n = _tabRects.Length;
        float[] startW = new float[n], startA = new float[n], targW = new float[n], targA = new float[n];
        for (int i = 0; i < n; i++)
        {
            startW[i] = _tabRects[i] != null ? _tabRects[i].sizeDelta.x : collapsedWidth;
            startA[i] = _tabLabels[i] != null ? _tabLabels[i].color.a : 0f;
            targW[i] = (i == sel) ? _expandedWidth[i] : collapsedWidth;
            targA[i] = (i == sel) ? 1f : 0f;
        }

        var cur = new float[n];
        float e = 0f, dur = Mathf.Max(0.01f, expandDuration);
        while (e < dur)
        {
            e += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(e / dur);
            t = 1f - (1f - t) * (1f - t);   // ease-out
            for (int i = 0; i < n; i++)
            {
                cur[i] = Mathf.Lerp(startW[i], targW[i], t);
                if (_tabLabels[i] != null)
                {
                    var c = _tabLabels[i].color; c.a = Mathf.Lerp(startA[i], targA[i], t); _tabLabels[i].color = c;
                }
            }
            LayoutWidths(cur);
            yield return null;
        }
        ApplyLayoutInstant(sel);
        _expandCo = null;
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
