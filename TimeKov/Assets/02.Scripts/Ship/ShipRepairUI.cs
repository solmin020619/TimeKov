using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 폐우주선 수리 패널 컨트롤러 (풀스크린 대형, 공장풍 프로스티드 + 우주선 홀로그램 링게이지).
// 패널 골격은 에디터 빌더(ShipRepairUIBuilder)가 만들고 ref 로 연결한다.
// 레벨 사다리(pip)와 부품 목록은 레벨 수가 가변이라 런타임에 이 컨트롤러가 생성한다.
// 데이터/조작은 ShipRepairManager 로만 (CurrentLevel/CanRepairNext/TryRepairNext/IsPartCollected/OnChanged).
public class ShipRepairUI : MonoBehaviour
{
    public static ShipRepairUI Instance { get; private set; }

    /// <summary>F로 닫은 프레임 — 터미널이 같은 입력으로 재오픈하는 깜빡임 방지.</summary>
    public static int LastCloseFrame { get; private set; } = -10;

    /// <summary>패널 호스트를 에디터에서 안 보이게 꺼둬도(비활성) 런타임에 찾아 활성화하고 Instance 를 보장한다.
    /// 씬에 패널 자체가 없으면(빌더 미실행) null.</summary>
    public static ShipRepairUI EnsureInstance()
    {
        if (Instance != null) return Instance;
        var found = FindObjectsByType<ShipRepairUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (found.Length == 0) return null;
        var ui = found[0];
        if (!ui.gameObject.activeSelf) ui.gameObject.SetActive(true);   // SetActive(true) -> Awake 동기 실행 -> Instance 세팅
        return Instance != null ? Instance : ui;
    }

    [Header("패널 (빌더가 연결)")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;

    [Header("헤더 / 사다리")]
    [SerializeField] private TextMeshProUGUI levelText;      // "수리 단계 Lv.N / M"
    [SerializeField] private RectTransform pipContainer;     // 레벨 pip 부모 (HorizontalLayoutGroup)

    [Header("홀로그램 (복원도)")]
    [SerializeField] private Image ringGauge;                // Filled Radial360, 복원도 비율
    [SerializeField] private TextMeshProUGUI restorePercentText;

    [Header("다음 수리 / 스탯")]
    [SerializeField] private TextMeshProUGUI nextHeaderText; // "다음 수리  Lv.N -> Lv.N+1"
    [Tooltip("[0]건축 범위 [1]설비 연료 [2]공장 가동속도 의 값 텍스트")]
    [SerializeField] private TextMeshProUGUI[] statValueTexts;

    [Header("부품")]
    [SerializeField] private TextMeshProUGUI partsCountText; // "회수 N / M"
    [SerializeField] private RectTransform partsContent;     // 부품 행 부모 (VerticalLayoutGroup)

    [Header("수리 버튼")]
    [SerializeField] private Button repairButton;
    [SerializeField] private TextMeshProUGUI repairButtonText;

    private static readonly Color Holo      = new Color(0.42f, 0.83f, 1.00f, 1f);
    private static readonly Color HoloSoft  = new Color(0.42f, 0.83f, 1.00f, 0.16f);
    private static readonly Color TextMain  = new Color(0.82f, 0.89f, 0.96f, 1f);
    private static readonly Color TextDim   = new Color(0.44f, 0.51f, 0.60f, 1f);
    private static readonly Color DoneCol   = new Color(0.36f, 0.84f, 0.56f, 1f);
    private static readonly Color PipEmpty  = new Color(0.30f, 0.36f, 0.44f, 1f);
    private static readonly Color BtnReady  = new Color(0.20f, 0.66f, 0.95f, 1f);
    private static readonly Color BtnLocked = new Color(0.16f, 0.20f, 0.28f, 0.95f);

    private readonly List<Image>            _pips     = new();
    private readonly List<ShipPartRow>      _partRows = new();
    private bool _dynamicBuilt;
    private int  _openedFrame = -1;

    // ── 라이프사이클 ──────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        closeButton?.onClick.AddListener(Close);
        repairButton?.onClick.AddListener(OnClickRepair);
        panelRoot?.SetActive(false);
    }

    private void OnEnable()  => ShipRepairManager.OnChanged += Refresh;
    private void OnDisable() => ShipRepairManager.OnChanged -= Refresh;

    private void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;
        if (Time.frameCount != _openedFrame && Input.GetKeyDown(KeyCode.F))
            Close();
    }

    // ── 열기 / 닫기 ───────────────────────────────────────────────────

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    public void Open()
    {
        if (panelRoot == null) return;

        panelRoot.SetActive(true);
        _openedFrame = Time.frameCount;

        BuildDynamic();
        Refresh();

        UISoundManager.Instance?.PlayPanelOpen();
    }

    /// <summary>패널만 숨긴다(상태 통보 없음) — GameUIController.CloseAll 이 호출.</summary>
    public void HidePanel()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Close()
    {
        if (panelRoot == null) return;

        LastCloseFrame = Time.frameCount;
        HidePanel();
        UISoundManager.Instance?.PlayPanelClose();
        GameUIController.Instance?.CloseShipRepairUI();
    }

    // ── 가변 요소 생성 (레벨 pip / 부품 행) ────────────────────────────

    private void BuildDynamic()
    {
        if (_dynamicBuilt) return;
        var mgr = ShipRepairManager.Instance;
        if (mgr == null) return;

        int max = mgr.MaxLevel;

        // 레벨 pip (Lv.1 ~ Lv.max)
        if (pipContainer != null)
        {
            for (int i = 0; i < max; i++)
                _pips.Add(MakePip(pipContainer));
        }

        // 부품 행 (레벨 2 ~ max 도달용)
        if (partsContent != null)
        {
            for (int level = 2; level <= max; level++)
                _partRows.Add(MakePartRow(partsContent, level));
        }

        _dynamicBuilt = true;
    }

    private Image MakePip(RectTransform parent)
    {
        var go = new GameObject("Pip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 30f; le.preferredHeight = 30f;
        var img = go.GetComponent<Image>();
        img.sprite = UISpriteFactory.Circle(48);
        img.color = PipEmpty;
        img.raycastTarget = false;
        return img;
    }

    private ShipPartRow MakePartRow(RectTransform parent, int level)
    {
        var go = new GameObject($"Part_{level}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 52f;
        var bg = go.GetComponent<Image>();
        bg.sprite = UISpriteFactory.RoundedRect(40, 10);
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.16f, 0.22f, 0.30f, 0.5f);
        bg.raycastTarget = false;

        var dotGo = new GameObject("Dot", typeof(RectTransform), typeof(Image));
        dotGo.transform.SetParent(go.transform, false);
        var drt = (RectTransform)dotGo.transform;
        drt.anchorMin = drt.anchorMax = new Vector2(0, 0.5f); drt.pivot = new Vector2(0, 0.5f);
        drt.sizeDelta = new Vector2(16, 16); drt.anchoredPosition = new Vector2(14, 0);
        var dot = dotGo.GetComponent<Image>();
        dot.sprite = UISpriteFactory.Circle(32);
        dot.raycastTarget = false;

        var nameGo = new GameObject("Name", typeof(RectTransform));
        nameGo.transform.SetParent(go.transform, false);
        var nrt = (RectTransform)nameGo.transform;
        nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 1);
        nrt.offsetMin = new Vector2(40, 0); nrt.offsetMax = new Vector2(-96, 0);
        var nameT = nameGo.AddComponent<TextMeshProUGUI>();
        nameT.fontSize = 15f; nameT.alignment = TextAlignmentOptions.Left; nameT.raycastTarget = false;

        var stGo = new GameObject("Status", typeof(RectTransform));
        stGo.transform.SetParent(go.transform, false);
        var strt = (RectTransform)stGo.transform;
        strt.anchorMin = new Vector2(1, 0); strt.anchorMax = new Vector2(1, 1); strt.pivot = new Vector2(1, 0.5f);
        strt.sizeDelta = new Vector2(90, 0); strt.anchoredPosition = new Vector2(-12, 0);
        var stT = stGo.AddComponent<TextMeshProUGUI>();
        stT.fontSize = 12f; stT.alignment = TextAlignmentOptions.Right; stT.raycastTarget = false;

        return new ShipPartRow { level = level, dot = dot, nameText = nameT, statusText = stT };
    }

    // ── 갱신 ──────────────────────────────────────────────────────────

    private void Refresh()
    {
        var mgr = ShipRepairManager.Instance;
        if (mgr == null) return;

        int cur = mgr.CurrentLevel;
        int max = mgr.MaxLevel;

        if (levelText != null)
            levelText.text = $"수리 단계  Lv.{cur} / {max}";

        // pip 색
        for (int i = 0; i < _pips.Count; i++)
        {
            int lv = i + 1;
            var img = _pips[i];
            if (img == null) continue;
            if (lv < cur)       { img.color = Holo; }
            else if (lv == cur) { img.color = Holo; }   // 현재
            else                { img.color = PipEmpty; }
        }

        // 복원도 링 (Lv.1=0% ~ Lv.max=100%)
        float prog = max > 1 ? (cur - 1f) / (max - 1f) : 1f;
        if (ringGauge != null) ringGauge.fillAmount = Mathf.Clamp01(prog);
        if (restorePercentText != null) restorePercentText.text = $"{Mathf.RoundToInt(prog * 100f)}%";

        bool maxed = mgr.IsFullyRepaired;

        // 다음 수리 헤더 + 스탯 미리보기
        var curDef  = mgr.GetLevel(cur);
        var nextDef = maxed ? null : mgr.GetLevel(cur + 1);

        if (nextHeaderText != null)
            nextHeaderText.text = maxed ? "수리 완료" : $"다음 수리   Lv.{cur} -> Lv.{cur + 1}";

        SetStat(0, "건축 범위",   curDef, nextDef, StatKind.Zone);
        SetStat(1, "설비 연료",   curDef, nextDef, StatKind.Fuel);
        SetStat(2, "공장 가동속도", curDef, nextDef, StatKind.Speed);

        // 부품 목록
        int gathered = 0;
        foreach (var row in _partRows)
        {
            if (row == null) continue;
            var def = mgr.GetLevel(row.level);
            if (row.nameText != null)
                row.nameText.text = def != null && !string.IsNullOrEmpty(def.requiredPartName) ? def.requiredPartName : $"부품 {row.level}";

            bool used      = mgr.IsPartUsed(row.level);
            bool collected = mgr.IsPartCollected(row.level);

            if (used)
            {
                gathered++;
                if (row.dot != null) row.dot.color = DoneCol;
                if (row.statusText != null) { row.statusText.text = "사용됨"; row.statusText.color = DoneCol; }
                if (row.nameText != null) row.nameText.color = TextDim;
            }
            else if (collected)
            {
                gathered++;
                if (row.dot != null) row.dot.color = Holo;
                if (row.statusText != null) { row.statusText.text = "보유"; row.statusText.color = Holo; }
                if (row.nameText != null) row.nameText.color = TextMain;
            }
            else
            {
                if (row.dot != null) row.dot.color = PipEmpty;
                if (row.statusText != null) { row.statusText.text = "미회수"; row.statusText.color = TextDim; }
                if (row.nameText != null) row.nameText.color = TextDim;
            }
        }
        if (partsCountText != null)
            partsCountText.text = $"회수  {gathered} / {Mathf.Max(0, max - 1)}";

        // 수리 버튼
        bool canRepair = mgr.CanRepairNext();
        if (repairButton != null)
        {
            repairButton.interactable = canRepair;
            if (repairButton.image != null) repairButton.image.color = canRepair ? BtnReady : BtnLocked;
        }
        if (repairButtonText != null)
            repairButtonText.text = maxed ? "수리 완료" : (canRepair ? "수리 실행" : "부품 부족");
    }

    private enum StatKind { Zone, Fuel, Speed }

    private void SetStat(int idx, string _, ShipRepairManager.LevelDef cur, ShipRepairManager.LevelDef next, StatKind kind)
    {
        if (statValueTexts == null || idx < 0 || idx >= statValueTexts.Length) return;
        var t = statValueTexts[idx];
        if (t == null) return;

        string curS = FormatStat(cur, kind);
        if (next == null)   // 최종 레벨 = 다음 없음
        {
            t.text = curS;
            t.color = TextDim;
            return;
        }

        string nextS = FormatStat(next, kind);
        bool changed = curS != nextS;
        if (changed)
        {
            t.text  = $"{curS}  ->  {nextS}";
            t.color = Holo;
        }
        else
        {
            t.text  = curS;
            t.color = TextDim;
        }
    }

    private string FormatStat(ShipRepairManager.LevelDef def, StatKind kind)
    {
        if (def == null) return "-";
        switch (kind)
        {
            case StatKind.Zone:  return $"단계 {def.zoneStage}";
            case StatKind.Fuel:  return $"{Mathf.RoundToInt(def.fuelSeconds)}초";
            case StatKind.Speed: return $"제작 {Mathf.RoundToInt(def.factorySpeed * 100f)}%";
        }
        return "-";
    }

    // ── 입력 ──────────────────────────────────────────────────────────

    private void OnClickRepair()
    {
        var mgr = ShipRepairManager.Instance;
        if (mgr == null) return;

        UISoundManager.Instance?.PlayButtonClick();
        mgr.TryRepairNext();   // 성공 시 OnChanged -> Refresh 자동
        Refresh();
    }

    private class ShipPartRow
    {
        public int level;
        public Image dot;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI statusText;
    }
}
