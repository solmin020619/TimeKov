// =====================================================================
// BaseUpgradeUI.cs
// 기지(핵심 구역) 업그레이드 패널 컨트롤러.
// 패널/행/재료슬롯 템플릿은 에디터 빌더(BaseUpgradeUIBuilder)가 인벤토리와 동일한
// 디자인(프로스티드 블러 + 라운드 + 슬라이드)으로 생성해 ref 로 연결한다.
//
// 흐름: 행 클릭 = 선택 → 하단 재료 슬롯(아이콘+현재/필요, 부족하면 흐리게) 갱신
//       → 제출 버튼으로 구매. 재료 슬롯 호버 시 인벤/창고와 같은 아이템 툴팁.
// =====================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[SingleInstance]
public class BaseUpgradeUI : MonoBehaviour
{
    public static BaseUpgradeUI Instance { get; private set; }

    /// <summary>F로 닫은 프레임 — 터미널이 같은 입력으로 재오픈하는 깜빡임 방지.</summary>
    public static int LastCloseFrame { get; private set; } = -10;

    [Header("패널 (빌더가 연결)")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private UISlideEffect slide;
    [SerializeField] private Transform content;          // 행 컨테이너
    [SerializeField] private GameObject rowTemplate;     // 비활성 행 템플릿
    [SerializeField] private GameObject groupTemplate;   // 비활성 그룹헤더 템플릿
    [SerializeField] private Button closeButton;

    [Header("하단 재료/제출 (빌더가 연결)")]
    [SerializeField] private BaseUpgradeMaterialSlot[] materialSlots;
    [SerializeField] private Button submitButton;
    [SerializeField] private TextMeshProUGUI submitButtonText;

    [Header("카메라 (사선 뷰 ↔ 게임플레이)")]
    [Tooltip("열 때 켤 사선(핵심 구역) 카메라. 비우면 카메라 전환 없음.")]
    [SerializeField] private Camera zoneCamera;
    [Tooltip("열 때 끌 게임플레이 카메라. 비우면 Camera.main 자동 사용.")]
    [SerializeField] private Camera gameplayCamera;

    private static readonly Color NameCol       = new Color(0.90f, 0.93f, 0.97f, 1f);
    private static readonly Color LockedCol     = new Color(0.48f, 0.54f, 0.62f, 1f);
    private static readonly Color OwnedCol      = new Color(0.45f, 0.85f, 0.55f, 1f);
    private static readonly Color RowNormalBg   = new Color(0.84f, 0.88f, 0.93f, 0.10f);
    private static readonly Color RowSelectedBg = new Color(0.30f, 0.55f, 0.85f, 0.28f);

    private static readonly Color BtnReady  = new Color(0.20f, 0.66f, 0.95f, 1f);
    private static readonly Color BtnLocked = new Color(0.16f, 0.20f, 0.28f, 0.95f);

    private class RowUI
    {
        public BaseUpgradeManager.UpgradeDef def;
        public Button button;
        public Image bg;
        public TextMeshProUGUI nameText;
        public Image markIcon;
    }
    private readonly List<RowUI> _rows = new();
    private BaseUpgradeManager.UpgradeDef _selected;
    private int _openedFrame = -1;

    // ── 라이프사이클 ──────────────────────────────────────────────────

    private void Awake()
    {
        if (UIDuplicateGuard.Report(Instance, this)) { Destroy(gameObject); return; }
        Instance = this;

        if (slide == null && panelRoot != null) slide = panelRoot.GetComponent<UISlideEffect>();
        closeButton?.onClick.AddListener(Close);
        submitButton?.onClick.AddListener(OnClickSubmit);

        rowTemplate?.SetActive(false);
        groupTemplate?.SetActive(false);
        panelRoot?.SetActive(false);
    }

    private void OnEnable()  => BaseUpgradeManager.OnChanged += Refresh;
    private void OnDisable() => BaseUpgradeManager.OnChanged -= Refresh;

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

        if (zoneCamera != null)
        {
            if (gameplayCamera == null) gameplayCamera = Camera.main;
            if (gameplayCamera != null) gameplayCamera.gameObject.SetActive(false);
            zoneCamera.gameObject.SetActive(true);
        }

        panelRoot.SetActive(true);
        slide?.Open();
        _openedFrame = Time.frameCount;

        RebuildRows();

        UISoundManager.Instance?.PlayPanelOpen();
    }

    public void Close()
    {
        if (panelRoot == null) return;

        LastCloseFrame = Time.frameCount;
        ItemTooltipUI.Instance?.Hide();

        if (zoneCamera != null)
        {
            zoneCamera.gameObject.SetActive(false);
            if (gameplayCamera != null) gameplayCamera.gameObject.SetActive(true);
        }

        if (slide != null && panelRoot.activeInHierarchy) slide.Close();
        else panelRoot.SetActive(false);

        UISoundManager.Instance?.PlayPanelClose();
        GameUIController.Instance?.CloseBaseUpgradeUI();
    }

    // ── 행 구성 ───────────────────────────────────────────────────────

    private void RebuildRows()
    {
        var mgr = BaseUpgradeManager.Instance;
        if (mgr == null || content == null || rowTemplate == null) return;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
        _rows.Clear();

        string lastGroup = null;
        for (int i = 0; i < mgr.Upgrades.Count; i++)
        {
            var def = mgr.Upgrades[i];
            if (def == null) continue;

            if (def.groupLabel != lastGroup)
            {
                MakeGroupHeader(def.groupLabel);
                lastGroup = def.groupLabel;
            }
            _rows.Add(MakeRow(def));
        }

        // 기본 선택 = 첫 구매가능 항목, 없으면 첫 항목
        _selected = null;
        foreach (var r in _rows)
            if (mgr.GetState(r.def) == BaseUpgradeManager.ItemState.Available) { _selected = r.def; break; }
        if (_selected == null && _rows.Count > 0) _selected = _rows[0].def;

        Refresh();
    }

    private void MakeGroupHeader(string label)
    {
        if (groupTemplate == null) return;
        var go = Instantiate(groupTemplate, content);
        go.SetActive(true);
        var t = go.transform.Find("Label")?.GetComponent<TextMeshProUGUI>()
             ?? go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (t != null) t.text = label;
    }

    private RowUI MakeRow(BaseUpgradeManager.UpgradeDef def)
    {
        var go = Instantiate(rowTemplate, content);
        go.SetActive(true);

        var row = new RowUI
        {
            def      = def,
            button   = go.GetComponent<Button>(),
            bg       = go.GetComponent<Image>(),
            nameText = go.transform.Find("Name")?.GetComponent<TextMeshProUGUI>(),
            markIcon = go.transform.Find("Mark")?.GetComponent<Image>(),
        };
        if (row.button != null) row.button.onClick.AddListener(() => OnClickRow(row));
        return row;
    }

    // ── 갱신 ──────────────────────────────────────────────────────────

    private void Refresh()
    {
        var mgr = BaseUpgradeManager.Instance;
        if (mgr == null) return;

        foreach (var row in _rows)
        {
            if (row == null || row.def == null) continue;
            var state = mgr.GetState(row.def);

            if (row.nameText != null)
            {
                row.nameText.text  = row.def.displayName;
                row.nameText.color = state == BaseUpgradeManager.ItemState.Locked ? LockedCol : NameCol;
            }
            if (row.bg != null)
                row.bg.color = (row.def == _selected) ? RowSelectedBg : RowNormalBg;

            if (row.markIcon != null)
            {
                if (state == BaseUpgradeManager.ItemState.Owned)
                {
                    row.markIcon.gameObject.SetActive(true);
                    row.markIcon.sprite = UISpriteFactory.Circle(64);
                    row.markIcon.color  = OwnedCol;
                }
                else if (state == BaseUpgradeManager.ItemState.Locked)
                {
                    row.markIcon.gameObject.SetActive(true);
                    row.markIcon.sprite = UISpriteFactory.Lock(64);
                    row.markIcon.color  = LockedCol;
                }
                else row.markIcon.gameObject.SetActive(false);
            }
        }

        UpdateBottom();
    }

    private void UpdateBottom()
    {
        var mgr = BaseUpgradeManager.Instance;
        if (mgr == null) return;

        var mats = _selected != null ? _selected.materials : null;
        if (materialSlots != null)
        {
            for (int i = 0; i < materialSlots.Length; i++)
            {
                if (materialSlots[i] == null) continue;
                if (mats != null && i < mats.Count && mats[i] != null && mats[i].itemId > 0)
                {
                    int need = mats[i].amount;
                    int have = mgr.GetTotalCount(mats[i].itemId);
                    materialSlots[i].Set(mats[i].itemId, have, need);
                }
                else materialSlots[i].Clear();
            }
        }

        // 제출 버튼 상태
        bool canBuy = _selected != null && mgr.CanPurchase(_selected);
        if (submitButton != null)
        {
            submitButton.interactable = canBuy;
            if (submitButton.image != null) submitButton.image.color = canBuy ? BtnReady : BtnLocked;
        }
        if (submitButtonText != null)
        {
            string label = Loc.Get("제출");
            if (_selected == null) label = Loc.Get("항목 선택");
            else
            {
                var st = mgr.GetState(_selected);
                if (st == BaseUpgradeManager.ItemState.Owned)       label = Loc.Get("보유 중");
                else if (st == BaseUpgradeManager.ItemState.Locked) label = Loc.Get("잠김");
                else if (!mgr.HasKit(_selected))                    label = Loc.Get("재료 부족");
                else                                                label = Loc.Get("제출");
            }
            submitButtonText.text = label;
        }
    }

    // ── 입력 ──────────────────────────────────────────────────────────

    private void OnClickRow(RowUI row)
    {
        if (row == null) return;
        UISoundManager.Instance?.PlayButtonClick();
        _selected = row.def;
        Refresh();
    }

    private void OnClickSubmit()
    {
        var mgr = BaseUpgradeManager.Instance;
        if (mgr == null || _selected == null) return;

        UISoundManager.Instance?.PlayButtonClick();
        mgr.TryPurchase(_selected);   // 성공 시 OnChanged → Refresh 자동 (실패 시 토스트)
        Refresh();
    }
}
