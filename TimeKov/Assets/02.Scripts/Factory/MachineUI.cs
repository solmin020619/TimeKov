using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TIMEKOV.Factory;

public class MachineUI : MonoBehaviour
{
    [Header("루트 패널")]
    public GameObject uiPanel;

    [Header("설비 이름")]
    public TextMeshProUGUI machineTitleText;

    [Header("인벤토리 슬롯 (왼쪽)")]
    public Transform inventorySlotParent;
    public GameObject inventorySlotPrefab;
    public int inventorySlotCount = 20;

    [Header("재료 슬롯 (오른쪽)")]
    public RecipeDropSlot[] recipeDropSlots;

    [Header("닫기 버튼")]
    [Tooltip("설비 UI 닫기 버튼 (없으면 무시됨)")]
    public Button closeBtn;

    [Header("회수 버튼")]
    [Tooltip("재료 회수 버튼 — RecipeSlot(InputBuffer)의 아이템만 인벤토리로 반환")]
    public Button takeInputsBtn;
    [Tooltip("모두 받기 버튼 — OutputSlot(현재 레시피 결과물)만 인벤토리로 가져옴")]
    public Button takeOutputBtn;

    [Header("레시피 선택 UI (선택 사항)")]
    [Tooltip("레시피 이전 버튼 (없으면 무시됨)")]
    public Button recipePrevBtn;
    [Tooltip("레시피 다음 버튼 (없으면 무시됨)")]
    public Button recipeNextBtn;
    [Tooltip("현재 레시피 인덱스 표시 텍스트 (예: 1/5)")]
    public TextMeshProUGUI recipeIndexText;
    [Tooltip("현재 레시피 이름 표시 텍스트 (선택 사항)")]
    public TextMeshProUGUI recipeNameText;

    [Header("진행 바 / 상태 텍스트")]
    public Slider progressBar;
    public TextMeshProUGUI statusText;

    [Header("출력 슬롯")]
    public MachineSlotWidget outputSlot;

    // [Gauge] 가공 진행 게이지 — 기존 가운데 ">>" 화살표 대체용
    // 비워두면 게이지 동작 안 함 (안전 가드, 기존 동작 유지)
    [Header("진행 게이지")]
    [Tooltip("가운데 화살표 자리의 ProcessingGauge. 비워두면 게이지 동작 안 함.")]
    [SerializeField] private ProcessingGauge processingGauge;

    [Header("설비 도면 이미지")]
    [Tooltip("중앙 설비 모델 렌더. OpenFor에서 facilityId로 자동 세팅(FacilityIconDatabase).")]
    [SerializeField] private Image facilityImage;

    [Header("가방/창고 탭")]
    [Tooltip("좌측 인벤 용량 표시 (예: 용량 11 / 35).")]
    [SerializeField] private TextMeshProUGUI bagCapacityText;
    [Tooltip("가방 보기 탭 버튼.")]
    [SerializeField] private Button bagTabBtn;
    [Tooltip("창고(Storage) 보기 탭 버튼.")]
    [SerializeField] private Button storageTabBtn;
    private bool _showStorage;

    [Header("헤더 설비 아이콘")]
    [Tooltip("헤더 좌측 설비 아이콘. OpenFor 가 facilityImage 와 같은 sprite 로 세팅.")]
    [SerializeField] private Image headerIconImage;

    [Header("진행바 노브 / 빈가방 / 가동글로우")]
    [Tooltip("푸터 진행바 fill 끝 노브. value 로 x 이동, 가공 중만 표시.")]
    [SerializeField] private RectTransform progressKnob;
    [Tooltip("가방이 비었을 때 표시하는 '비어있음' 텍스트.")]
    [SerializeField] private TextMeshProUGUI bagEmptyText;
    [Tooltip("가공 중 기계 뒤 노란 글로우. 알파 펄스(unscaled).")]
    [SerializeField] private Image machineGlow;

    [Header("현재 생산 공식 스트립(하단)")]
    [Tooltip("공식 아이콘 엔트리 부모(HLG). 런타임이 재료->결과 아이콘을 채움.")]
    [SerializeField] private Transform formulaContent;
    [Tooltip("공식 스트립 좌측 상태 라벨(생산 중 / 대기 중).")]
    [SerializeField] private TextMeshProUGUI formulaStatusText;
    [Tooltip("흐름 레일(기계->출력). 입력 레일은 슬롯 자식이라 별도 ref 불필요.")]
    [SerializeField] private Image outputRail;
    [Tooltip("입력 수직 버스(합류선). 길이/위치는 런타임이 입력 칸수로 세팅.")]
    [SerializeField] private Image inputBus;
    [Tooltip("버스->기계 가로 연결.")]
    [SerializeField] private Image busToMachine;
    [Tooltip("공정 흐름 레일 컨테이너(런타임이 포트/버스/레일 생성). 0크기 = 생산영역 중심 기준 좌표.")]
    [SerializeField] private RectTransform flowRailsRoot;
    // 실제 레일(RailPiece 프리팹)을 RenderTexture 로 렌더해 UI 에 표시(도감 초상화 방식). 연결된 포트에만.
    private RailPortraitRenderer _railPortrait;
    private Texture _railTex;

    [Header("플레이어 인벤토리")]
    public InventoryManager playerInventory;

    [Header("연료 슬롯")]
    [Tooltip("FuelDropSlot 컴포넌트가 붙은 연료 슬롯 오브젝트.")]
    public FuelDropSlot fuelDropSlot;

    [Header("드래그&드랍 설정")]
    [Tooltip("인벤토리 패널에 붙어 있는 InventoryPanelDropZone 오브젝트.\n" +
             "비워두면 inventorySlotParent 부모에서 자동으로 찾거나 추가합니다.")]
    public InventoryPanelDropZone inventoryDropZone;

    private ProcessingMachine _machine;
    private int _selectedRecipeIndex = 0;

    // statusText(연료 부족 — 연료 칸 위)와 분리된 제작 시간 표시용.
    // statusText를 복제해 원래 중앙(진행바 위) 위치에 배치한다.
    private TextMeshProUGUI _processTimeText;
    private readonly List<InventorySlotUI> _invSlots = new();
    /// <summary>outputSlot 외에 동적으로 생성된 추가 출력 슬롯들.</summary>
    private readonly List<MachineSlotWidget> _extraOutputSlots = new();

    // 현재 선택 레시피의 출력 버퍼에 받을 게 있는지(모두받기 dim #34 + 출력 펄스 #40 공통).
    private bool _hasOutput;
    private Coroutine _tabFadeCo;

    // ── 초기화 ────────────────────────────────────────────────

    private void Awake()
    {
        if (closeBtn != null)       closeBtn.onClick.AddListener(Close);
        if (recipePrevBtn != null)  recipePrevBtn.onClick.AddListener(PrevRecipe);
        if (recipeNextBtn != null)  recipeNextBtn.onClick.AddListener(NextRecipe);
        if (takeInputsBtn != null)  takeInputsBtn.onClick.AddListener(TakeAllInputs);
        if (takeOutputBtn != null)  takeOutputBtn.onClick.AddListener(TakeAll);
        if (bagTabBtn != null)      bagTabBtn.onClick.AddListener(ShowBag);
        if (storageTabBtn != null)  storageTabBtn.onClick.AddListener(ShowStorage);

        SetupDropZone();
        SetupProcessTimeText();
    }

    // ── 제작 시간 텍스트 분리 ───────────────────────────────────────────
    // statusText는 연료 칸 위로 옮겨져 "연료 부족" 전용이 됐으므로,
    // 제작 시간("N초")은 statusText를 복제해 원래 위치(진행바 위, 중앙)에 따로 띄운다.
    private void SetupProcessTimeText()
    {
        if (_processTimeText != null) return;       // 이미 생성됨
        if (statusText == null) return;

        var clone = Instantiate(statusText.gameObject, statusText.transform.parent);
        clone.name = "ProcessTimeText";
        _processTimeText = clone.GetComponent<TextMeshProUGUI>();

        // statusText가 원래 있던 중앙(진행바 위) 위치로 복귀.
        _processTimeText.rectTransform.anchoredPosition = new Vector2(-14.001f, 98f);
        _processTimeText.text = "";
    }

    // ── 드롭존 자동 설정 ────────────────────────────────────────

    private void SetupDropZone()
    {
        if (inventoryDropZone != null)
        {
            inventoryDropZone.SetDropCallback(TakeOutput);
            return;
        }

        if (inventorySlotParent == null) return;

        inventoryDropZone = inventorySlotParent.GetComponent<InventoryPanelDropZone>();

        if (inventoryDropZone == null && inventorySlotParent.parent != null)
            inventoryDropZone = inventorySlotParent.parent.GetComponent<InventoryPanelDropZone>();

        if (inventoryDropZone == null)
        {
            inventoryDropZone = inventorySlotParent.gameObject.AddComponent<InventoryPanelDropZone>();
        }

        inventoryDropZone.SetDropCallback(TakeOutput);
    }

    // ── 설비 UI 열기 ────────────────────────────────────────────

    public void OpenFor(ProcessingMachine machine, string title)
    {
        if (_machine != null) _machine.OnBufferChanged -= OnBufferChanged;
        _machine = machine;
        _machine.OnBufferChanged += OnBufferChanged;

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        if (inv != null)
        {
            inv.OnInventoryChanged -= RefreshInventorySlots;
            inv.OnInventoryChanged += RefreshInventorySlots;
        }

        _selectedRecipeIndex = Mathf.Max(0, machine.LockedRecipeIndex);

        if (machineTitleText != null) machineTitleText.text = title;

        // 중앙 설비 도면 = 전용 도면 폴더(클로드디자인) 우선, 없으면 퀵슬롯 모델 폴백.
        //   도면 위치: Assets/Resources/Image/UI_Icon/FacilityBlueprint/  파일명 "{facilityId}" 또는 "{facilityId}_설비명".
        //   헤더 아이콘은 작은 모델 아이콘이라 그대로 FacilityIconDatabase 사용.
        if (facilityImage != null || headerIconImage != null)
        {
            var fIcon = FacilityIconDatabase.Instance != null
                ? FacilityIconDatabase.Instance.GetIcon(machine.FacilityId) : null;
            if (facilityImage != null)
            {
                var blueprint = LoadFacilityBlueprint(machine.FacilityId);
                var spr = blueprint != null ? blueprint : fIcon;
                facilityImage.sprite = spr;
                facilityImage.enabled = spr != null;
            }
            if (headerIconImage != null) { headerIconImage.sprite = fIcon; headerIconImage.enabled = fIcon != null; }
        }

        // 모든 슬롯을 먼저 구성한 뒤 패널을 활성화 —
        // SetActive 이전에 Setup을 완료해야 RecipeDropSlot의 Start/OnEnable
        // 기본 상태("재료 넣기" + 흰 박스)가 한 프레임 깜빡이는 현상을 방지한다.
        _showStorage = false;   // 열 때 항상 가방 뷰부터
        BuildRecipeSlots();
        BuildInventorySlots();
        // 탭 페이드(#17)가 닫힘으로 중단돼 알파가 덜 찼을 수 있어 열 때 1로 리셋.
        var gridCg = inventorySlotParent != null ? inventorySlotParent.GetComponent<CanvasGroup>() : null;
        if (gridCg != null) gridCg.alpha = 1f;
        UpdateTabVisual();
        RefreshOutputSlots();

        // 연료 슬롯 초기화
        var inv2 = playerInventory != null ? playerInventory : InventoryManager.Instance;
        fuelDropSlot?.Setup(machine, inv2);

        uiPanel.SetActive(true);
        GameSfx.Play(SfxId.MachineOpen);
        FacilityWorldDisplay.SuppressWorldLabels = true;   // 월드 이름표/제작아이콘이 패널 블러 위로 뚫지 않게

        ShowFirstMachineHintIfNeeded();

        // [Gauge] 게이지 초기화 — 가공 시작 전 0%로 비우고 숨김
        if (processingGauge != null) processingGauge.StopAndHide();

        // 인벤토리에서 아이템을 집어든 순간 연료/재료 슬롯을 강조 (드랍 위치 안내)
        InventorySlotUI.OnAnySlotDragBegin -= OnInventoryDragBegin;
        InventorySlotUI.OnAnySlotDragBegin += OnInventoryDragBegin;
        InventorySlotUI.OnAnySlotDragEnd   -= OnInventoryDragEnd;
        InventorySlotUI.OnAnySlotDragEnd   += OnInventoryDragEnd;
    }

    // 중앙 도면 로드: Resources/Image/UI_Icon/FacilityBlueprint/ 에서 "{id}" 또는 "{id}_이름" 스프라이트.
    // 없으면 null(호출측이 퀵슬롯 모델로 폴백). 폴더 없으면 LoadAll 이 빈 배열 -> 안전.
    private static Sprite LoadFacilityBlueprint(int facilityId)
    {
        var all = Resources.LoadAll<Sprite>("Image/UI_Icon/FacilityBlueprint");
        if (all == null) return null;
        string id = facilityId.ToString();
        foreach (var s in all)
        {
            if (s == null) continue;
            if (s.name == id || s.name.StartsWith(id + "_")) return s;
        }
        return null;
    }

    // ── 레시피 선택 ─────────────────────────────────────────────

    public void PrevRecipe()
    {
        if (_machine == null || _machine.Recipes == null || _machine.Recipes.Count == 0) return;
        _selectedRecipeIndex = (_selectedRecipeIndex - 1 + _machine.Recipes.Count) % _machine.Recipes.Count;
        // SetLockedRecipe 호출 없음 — 화살표는 탐색용, 생산 레시피는 재료 투입 시 결정
        BuildRecipeSlots();
        RefreshOutputSlots();
    }

    public void NextRecipe()
    {
        if (_machine == null || _machine.Recipes == null || _machine.Recipes.Count == 0) return;
        _selectedRecipeIndex = (_selectedRecipeIndex + 1) % _machine.Recipes.Count;
        // SetLockedRecipe 호출 없음 — 화살표는 탐색용, 생산 레시피는 재료 투입 시 결정
        BuildRecipeSlots();
        RefreshOutputSlots();
    }

    public void SelectRecipe(int index)
    {
        if (_machine == null || _machine.Recipes == null) return;
        _selectedRecipeIndex = Mathf.Clamp(index, 0, _machine.Recipes.Count - 1);
        _machine.SetLockedRecipe(_selectedRecipeIndex);
        BuildRecipeSlots();
        RefreshOutputSlots();
    }

    // ── 닫기 ────────────────────────────────────────────────────

    public void Close()
    {
        GameSfx.Play(SfxId.MachineClose);
        FacilityWorldDisplay.SuppressWorldLabels = false;   // 패널 닫으면 월드 표시 복구
        if (_machine != null) _machine.OnBufferChanged -= OnBufferChanged;

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        if (inv != null) inv.OnInventoryChanged -= RefreshInventorySlots;

        InventorySlotUI.OnAnySlotDragBegin -= OnInventoryDragBegin;
        InventorySlotUI.OnAnySlotDragEnd   -= OnInventoryDragEnd;
        ClearDropHighlights();

        var hintMgr = TimeKov.UI.HintArrowManager.I;
        if (hintMgr != null)
        {
            hintMgr.Hide("machine_output");
            hintMgr.Hide("recipe_slot_hint");
            hintMgr.Hide("first_machine_hint");
        }

        // [Gauge] 게이지 정리 — 패널 닫을 때 0%로 비우고 숨김
        if (processingGauge != null) processingGauge.StopAndHide();

        // 연료 슬롯 정리
        fuelDropSlot?.Cleanup();

        _machine = null;

        var unfold = uiPanel.GetComponent<UIUnfoldEffect>();
        if (unfold != null && uiPanel.activeInHierarchy)
            unfold.Close();
        else
            uiPanel.SetActive(false);

        GameUIController.Instance?.CloseFactoryUI();
    }

    public void AddItemFromInventory(int itemId, int amount)
    {
        if (_machine == null) return;
        if (!_machine.CanReceive(itemId)) return;
        _machine.Receive(itemId, amount);
    }

    // ── 인벤토리 슬롯 ───────────────────────────────────────────

    private void BuildInventorySlots()
    {
        var inv = ActiveInv();
        int slotCount = inv != null ? inv.GetMaxSlots() : inventorySlotCount;

        if (_invSlots.Count != slotCount)
        {
            foreach (var s in _invSlots)
                if (s != null) Destroy(s.gameObject);
            _invSlots.Clear();

            for (int i = 0; i < slotCount; i++)
            {
                var go = Instantiate(inventorySlotPrefab, inventorySlotParent);
                var slot = go.GetComponent<InventorySlotUI>();
                if (slot == null)
                    Debug.LogError("[MachineUI] inventorySlotPrefab에 InventorySlotUI 컴포넌트가 없습니다!");
                _invSlots.Add(slot);
            }
        }

        RefreshInventorySlots();
    }

    public void RefreshInventorySlots()
    {
        var inv = ActiveInv();
        int used = inv != null ? inv.GetUsedSlotCount() : 0;
        if (bagCapacityText != null && inv != null)
            bagCapacityText.text = $"용량 {used} / {inv.GetMaxSlots()}";
        if (bagEmptyText != null) bagEmptyText.gameObject.SetActive(used == 0);
        if (inv == null)
        {
            foreach (var slot in _invSlots)
                slot?.Refresh(null, null);
            return;
        }

        var slots = inv.GetSlots();
        for (int i = 0; i < _invSlots.Count; i++)
        {
            if (_invSlots[i] == null) continue;
            InventorySlot slotData = i < slots.Count ? slots[i] : null;
            _invSlots[i].Refresh(slotData, inv);
        }
    }

    // ── 가방/창고 탭 ─────────────────────────────────────────────
    // 기본 _showStorage=false -> 가방(player) 뷰. 창고 탭 누르면 StorageInstance(50칸) 뷰.
    // 드래그 출처는 InventorySlotUI.Refresh(slotData, inv) 가 Owner=inv 로 잡아주므로
    // 재료/연료 슬롯으로 드래그하면 해당 인벤(가방 or 창고)에서 차감된다.

    private InventoryManager ActiveInv()
        => _showStorage ? InventoryManager.StorageInstance
                        : (playerInventory != null ? playerInventory : InventoryManager.Instance);

    public void ShowBag()     => SetView(false);
    public void ShowStorage() => SetView(true);

    private void SetView(bool storage)
    {
        if (_showStorage == storage) return;
        _showStorage = storage;
        GameSfx.Play(SfxId.MachineTabClick);
        BuildInventorySlots();   // 칸 수(가방35/창고50) 다를 수 있어 재구성 + 갱신
        UpdateTabVisual();
        PlayTabFade();
    }

    // 탭 전환 시 그리드 짧은 페이드(#17). timeScale=0 안전(unscaledDeltaTime).
    private void PlayTabFade()
    {
        if (inventorySlotParent == null) return;
        var cg = inventorySlotParent.GetComponent<CanvasGroup>();
        if (cg == null) cg = inventorySlotParent.gameObject.AddComponent<CanvasGroup>();
        if (_tabFadeCo != null) StopCoroutine(_tabFadeCo);
        _tabFadeCo = StartCoroutine(TabFadeRoutine(cg));
    }

    private System.Collections.IEnumerator TabFadeRoutine(CanvasGroup cg)
    {
        cg.alpha = 0.25f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.12f;
            cg.alpha = Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(t));
            yield return null;
        }
        cg.alpha = 1f;
        _tabFadeCo = null;
    }

    private void UpdateTabVisual()
    {
        SetTabActive(bagTabBtn, !_showStorage);
        SetTabActive(storageTabBtn, _showStorage);
    }

    // 선택 탭은 또렷(알파 0.9), 비선택은 흐리게(0.2). 색 RGB는 빌더값 유지, 알파만 토글.
    private static void SetTabActive(Button b, bool on)
    {
        if (b == null || b.image == null) return;
        var c = b.image.color; c.a = on ? 0.9f : 0.2f; b.image.color = c;
        var underline = b.transform.Find("Underline");
        if (underline != null) underline.gameObject.SetActive(on);
    }

    // ── 재료 슬롯 ───────────────────────────────────────────────

    private void BuildRecipeSlots()
    {
        if (_machine == null || recipeDropSlots == null) return;

        var recipes = _machine.Recipes;
        if (recipes == null || recipes.Count == 0) return;

        _selectedRecipeIndex = Mathf.Clamp(_selectedRecipeIndex, 0, recipes.Count - 1);
        var selectedRecipe = recipes[_selectedRecipeIndex];
        var inputs = selectedRecipe.inputs;

        RefreshRecipeSelectionUI(recipes.Count);

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        // 슬롯 = 레시피 기준(재료칸 = 재료 수, 결과칸 = 1). 포트 구조는 BuildFlowRails 가 따로 그린다.
        for (int i = 0; i < recipeDropSlots.Length; i++)
        {
            if (recipeDropSlots[i] == null) continue;

            if (i < inputs.Length)
            {
                recipeDropSlots[i].gameObject.SetActive(true);
                // _selectedRecipeIndex 전달 — 재료 드랍 시 해당 레시피로 생산 고정
                recipeDropSlots[i].Setup(inputs[i].itemId, inputs[i].amount, _machine, inv, _selectedRecipeIndex);
            }
            else
            {
                recipeDropSlots[i].gameObject.SetActive(false);
            }
        }

        // 공정 흐름 레일(포트 -> 세로 버스 -> 슬롯) 재생성.
        BuildFlowRails();

        BuildFormula();
        ShowRecipeHintIfQuestActive();
    }

    // 설비 입력/출력 포트 수 = 실제 BuildPort 개수(3x3=3, 5x5=5). 시트 slotCount 아님(다를 수 있음).
    private int InputPortCount() => CountPorts(PortType.Input);

    private int CountPorts(PortType type)
    {
        if (_machine == null) return 1;
        int n = 0;
        var ports = _machine.GetComponentsInChildren<BuildPort>();
        foreach (var p in ports) if (p != null && p.portType == type) n++;
        return Mathf.Max(n, 1);
    }

    private int OutputPortCount() => CountPorts(PortType.Output);

    // ── 공정 흐름 레일: 설비 포트(N) -> 세로 버스 -> 재료/결과칸. 입력=회색/흰, 출력=파랑. ──
    //   버스/가로레일은 얇게(한 트레이스), 포트 단자만 굵게 강조. 포트별 실제 벨트연결 반영(연결=밝게 + 가동 시 흰 펄스).
    // FR_SlotEdgeX = 슬롯 모서리(±225), FR_PortX = 포트(±360). 그 사이를 버스가 나눔.
    //   버스를 포트쪽으로 붙여 슬롯쪽 가로선을 길게(±310) -> 포트쪽 50 : 슬롯쪽 85. (레퍼런스처럼 슬롯쪽이 더 김)
    private const float FR_PortX = 360f, FR_BusX = 310f, FR_SlotEdgeX = 225f;
    // FR_PortPitch = 포트 세로 간격. 포트단자를 키우면(FR_PortTickH) 출력(포트 최다 3개)에서 안 붙게 간격도 넉넉히.
    private const float FR_PortPitch = 80f, FR_SlotPitch = 152f;
    // ★레퍼런스(엔필)처럼 = 버스/가로레일은 다 얇은 회로선 하나로 이어지게, 포트 연결 세로선만 굵게 강조.
    //   이음새 끊김 해결 = 가로선을 세로선 속으로 한 두께 겹치게(MakeHRail) + 같은 색 통일 + 버스 끝 늘리기.
    private const float FR_RailThin = 4f;      // 버스/가로레일 = 얇은 회로선(가늘게, 하나로 이어지게)
    private const float FR_PortTickW = 8f;      // 포트 단자 = 유일하게 강조(굵은 세로선). 얇은 레일보다 확실히 굵게.
    private const float FR_PortTickH = 64f;     // 포트 단자 길이(연결지점 세로 강조) - 레일(벨트 RT)이 여기 맞춰 들어가므로 넉넉히
    private const float FR_PortWrapGap = 28f;   // 포트가 슬롯을 감쌀 때 슬롯 최외곽에서 더 벌리는 여유(포트단자가 슬롯과 안 겹치게)
    private const float FR_SlotDotOut = 8f;     // 슬롯쪽 레일 끝(=연결점)을 슬롯 모서리에서 버스쪽으로 뺀 거리(박스에 안 가리게)
    // 실제 게임 레일(RenderTexture) = 정사각 텍스처의 가운데 가로 스트립(band) + 나머지 투명.
    //   벨트를 포트단자(FR_PortTickH)에 자동으로 맞춘다: band 높이 = 포트단자 높이가 되게 표시크기를 역산.
    //   렌더러 프레이밍(fov28/fill0.82/3타일) 상 band 높이 = 0.273*표시크기, band 좌우여백 = 0.09*표시크기.
    private const float FR_BeltSizePerTick = 3.66f; // 표시크기 = 포트단자높이 / 0.273. 벨트가 세로선보다 크거나 작으면 이 값만 조정.
    private const float FR_BeltInnerFrac   = 0.41f; // 밀어낼 거리 = 표시크기 * (0.5-0.09). band 안쪽 끝이 포트에 딱(공백 남으면 이 값만 조정).
    private const float FR_BeltSize = FR_PortTickH * FR_BeltSizePerTick;  // ★포트단자 조정하면 벨트가 자동으로 이 크기에 맞음
    private const float FR_BeltOut  = FR_BeltSize * FR_BeltInnerFrac;
    private const float FR_PulseSpeed = 0.8f;  // 가동 시 흰 펄스 흐름 속도
    private const float FR_BeltItemSpeed = 0.32f; // 벨트 위 아이템 아이콘 흐름 속도(펄스보다 느리게)
    private static readonly Color FR_BusGray   = new Color(0.55f, 0.58f, 0.63f, 0.9f);  // 입력 버스(회색)
    private static readonly Color FR_RailWhite = new Color(0.82f, 0.93f, 1.0f, 1f);     // 입력 가로레일(밝은 시안화이트)
    private static readonly Color FR_Blue      = new Color(0.28f, 0.80f, 1.0f, 1f);     // 출력 = 밝은 시안(실제 벨트색, 버스/레일/단자)
    private static Sprite _frCircle;
    // 가동 중 레일을 따라 흐르는 흰 펄스. 각 펄스는 a->b 를 반복 이동.
    private struct RailPulse { public RectTransform dot; public Image img; public Vector2 a, b; public float phase; }
    private readonly System.Collections.Generic.List<RailPulse> _railPulses = new();
    // 연결된 포트별 벨트 위 아이템 아이콘. 실제 벨트 occupant 를 실시간 폴링해 유입/배출 아이템을 표시(아이템 올 때만).
    private class BeltFlow { public RectTransform rt; public Image img; public Vector2 a, b; public BuildPort port; public int shownId = -1; }
    private readonly System.Collections.Generic.List<BeltFlow> _beltFlows = new();
    private readonly System.Collections.Generic.List<TextMeshProUGUI> _centerChevrons = new();

    private void BuildFlowRails()
    {
        if (flowRailsRoot == null || _machine == null) return;
        for (int i = flowRailsRoot.childCount - 1; i >= 0; i--)
            Destroy(flowRailsRoot.GetChild(i).gameObject);
        _railPulses.Clear();
        _beltFlows.Clear();
        _centerChevrons.Clear();

        var recipes = _machine.Recipes;
        if (recipes == null || recipes.Count == 0) return;
        int ri = Mathf.Clamp(_selectedRecipeIndex, 0, recipes.Count - 1);
        var recipe = recipes[ri];
        int inSlots  = recipe != null && recipe.inputs  != null ? recipe.inputs.Length : 0;
        int outSlots = recipe != null && recipe.outputs != null && recipe.outputs.Length > 0 ? 1 : 0;

        // 한 색 회로 하나로: 입력=흰(시안화이트) 전부, 출력=파랑 전부. 포트단자만 굵기로 강조.
        BuildRailSide(true,  InputPortCount(),  inSlots,  FR_RailWhite);
        BuildRailSide(false, OutputPortCount(), outSlots, FR_Blue);

        // 중앙 흐름 화살표 2개(재료->결과). 회색 기본 + 가동 시 흰 펄스(UpdateFlowRails 가 좌->우로 칠함).
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject("FlowChevron", typeof(RectTransform));
            go.transform.SetParent(flowRailsRoot, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(30f, 38f); rt.anchoredPosition = new Vector2(-9f + i * 17f, 0f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = ">"; tmp.fontSize = 30; tmp.fontStyle = FontStyles.Bold;
            tmp.color = FR_BusGray; tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
            _centerChevrons.Add(tmp);
        }
    }

    // 실제 레일 프리팹 -> RenderTexture (1회 렌더 후 캐시, 카메라가 매프레임 흐름 갱신).
    private Texture EnsureRailTexture()
    {
        if (_railTex != null) return _railTex;
        var rbm = FindFirstObjectByType<RailBuildManager>();
        if (rbm == null || rbm.StraightRailPrefab == null) return null;
        if (_railPortrait == null)
        {
            var go = new GameObject("RailPortrait");
            go.transform.SetParent(transform, false);
            _railPortrait = go.AddComponent<RailPortraitRenderer>();
        }
        _railTex = _railPortrait.Render(rbm.StraightRailPrefab, 256, 256);
        return _railTex;
    }

    // 연결된 포트 바깥쪽에 실제 게임 레일(RenderTexture)을 얹는다.
    //   정사각 텍스처 = 가운데 가로 레일 + 나머지 투명 -> 정사각 RawImage 로 얹으면 왜곡/크롭 없이 가로 레일만 보인다.
    private void MakeRailStrip(Vector2 pos)
    {
        var tex = EnsureRailTexture();
        if (tex == null) return;
        var go = new GameObject("PortRailReal", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(flowRailsRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(FR_BeltSize, FR_BeltSize);
        rt.anchoredPosition = pos;
        var raw = go.GetComponent<RawImage>();
        raw.texture = tex; raw.raycastTarget = false;
    }

    // 포트별 벨트 아이템 아이콘 생성(처음엔 숨김). UpdateFlowRails 가 매 프레임 실제 벨트 occupant 를 읽어 표시/숨김+아이콘 갱신+흐름.
    private void MakeBeltFlow(BuildPort port, Vector2 a, Vector2 b)
    {
        var go = new GameObject("BeltItem", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(flowRailsRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        float d = FR_PortTickH * 0.9f;
        rt.sizeDelta = new Vector2(d, d);
        var img = go.GetComponent<Image>();
        img.preserveAspect = true; img.raycastTarget = false; img.enabled = false;   // 실제 아이템 올 때만 켬
        _beltFlows.Add(new BeltFlow { rt = rt, img = img, a = a, b = b, port = port });
    }

    // 한쪽(입력/출력) 레일 생성. isInput=true 면 왼쪽(-), false 면 오른쪽(+).
    // 버스/가로레일/포트단자 전부 같은 색(하나의 회로) + 조각을 서로 겹쳐 이음새 제거. 포트 단자만 굵게 강조.
    private void BuildRailSide(bool isInput, int nPorts, int slotCount, Color baseColor)
    {
        if (nPorts <= 0) return;
        float sign = isInput ? -1f : 1f;
        float portX = sign * FR_PortX, busX = sign * FR_BusX, slotEdge = sign * FR_SlotEdgeX;
        var ports = PortsOfType(isInput ? PortType.Input : PortType.Output);   // 틱 순서 = 이 배열 순서(실제 포트)

        float slotTop = (slotCount - 1) * 0.5f * FR_SlotPitch;
        float portNatural = (nPorts - 1) * 0.5f * FR_PortPitch;
        // 포트가 슬롯을 위아래로 감싸게(바깥) -> 슬롯은 안쪽에서 엇갈림, 버스 끝은 최외곽 포트에 딱(맨살 스텁 없음).
        //   슬롯이 있으면 포트 span 을 슬롯+gap 이상으로 벌린다. 슬롯이 좁거나 없으면(출력=슬롯1개 중앙) 포트는 자연 간격.
        float portHalf  = slotTop > 0f ? Mathf.Max(portNatural, slotTop + FR_PortWrapGap) : portNatural;
        float busHalf   = Mathf.Max(portHalf, 1f);
        float portTop   = nPorts > 1 ? portHalf : 0f;                      // 포트 1개면 중앙
        float portPitch = nPorts > 1 ? (portHalf * 2f) / (nPorts - 1) : 0f;
        MakeQuad("Bus", new Vector2(busX, 0f), new Vector2(FR_RailThin, busHalf * 2f), baseColor);

        // 포트 단자(굵게 강조) + 포트->버스 가로레일. 연결된 포트 = 밝게 + 실제 레일 + 흰 펄스.
        for (int j = 0; j < nPorts; j++)
        {
            float py = portTop - j * portPitch;
            BuildPort port = j < ports.Length ? ports[j] : null;
            bool c = port != null && BeltSegment.IsPortConnected(port);
            Color tickCol = c ? Color.Lerp(baseColor, Color.white, 0.55f) : baseColor;
            Color railCol = c ? Color.Lerp(baseColor, Color.white, 0.30f) : baseColor;
            // 연결된 포트 = 실제 게임 레일(RenderTexture)을 "먼저"(맨 뒤에) 깐다 -> 포트단자/가로선이 그 위를 덮어 겹침 색 얼룩(블렌딩) 방지.
            //   입력=왼쪽 바깥, 출력=오른쪽 바깥. RT 는 오른쪽 흐름(railYaw270) = 입력 유입 / 출력 배출 둘 다 맞음.
            if (c)
            {
                MakeRailStrip(new Vector2(portX + sign * FR_BeltOut, py));
                // 벨트 위 실제 유입/배출 아이템: 그 레일에 실제로 아이템이 올라와 있을 때만 그 아이콘을 표시(실시간, 폴링).
                float innerX = portX;                          // 포트(안쪽 끝)
                float outerX = portX + sign * 2f * FR_BeltOut; // 벨트 바깥 끝
                Vector2 ia = isInput ? new Vector2(outerX, py) : new Vector2(innerX, py);   // 입력=바깥에서 시작(유입) / 출력=포트에서
                Vector2 ib = isInput ? new Vector2(innerX, py) : new Vector2(outerX, py);   // ...끝(입력=포트로 / 출력=바깥으로 배출)
                MakeBeltFlow(port, ia, ib);
            }
            MakeHRail("PortRail", portX, busX, py, railCol);                                                 // 포트<->버스 가로선(벨트 위)
            MakeQuad("Port", new Vector2(portX, py), new Vector2(FR_PortTickW, FR_PortTickH), tickCol);      // 유일한 강조(굵은 세로, 벨트 위에 덮여 얼룩 가림)
            if (c)
            {
                // 가동 시 흰 펄스 2개(간격 0.5)가 포트<->버스를 좌->우로 흐름. 입력=포트->버스, 출력=버스->포트.
                Vector2 pa = new Vector2(isInput ? portX : busX, py);
                Vector2 pb = new Vector2(isInput ? busX : portX, py);
                for (int s = 0; s < 2; s++)
                {
                    var img = MakePulseDot();
                    _railPulses.Add(new RailPulse { dot = img.rectTransform, img = img, a = pa, b = pb, phase = s * 0.5f });
                }
            }
        }

        // 버스->슬롯 가로레일 + 버스 탭 노드 + 재료칸쪽 연결점.
        for (int k = 0; k < slotCount; k++)
        {
            float sy = slotTop - k * FR_SlotPitch;
            // 레일 끝(=연결점)을 슬롯 박스 밖(버스쪽)으로 빼서 박스에 안 가리게. 점은 항상 레일 끝에.
            float slotEnd = slotEdge + sign * FR_SlotDotOut;
            MakeHRail("SlotRail", slotEnd, busX, sy, baseColor);
            MakeDot(new Vector2(slotEnd, sy), 9f, baseColor);
        }
    }

    // 두 X 지점(바깥끝, 버스중심)을 정확히 잇는다. 늘리지 않음.
    //   버스 쪽 끝이 버스 중심(xB=busX)에서 딱 끝나 = 버스 절반과 겹쳐 틈 없이 붙되, 반대쪽으로 삐져나오는 스텁이 안 생긴다.
    //   바깥끝(포트단자/슬롯점)은 그 위에 덮이는 단자/동그라미가 가려줘서 별도 겹침 불필요.
    private void MakeHRail(string name, float xOuter, float xBus, float y, Color color)
    {
        float lo = Mathf.Min(xOuter, xBus);
        float hi = Mathf.Max(xOuter, xBus);
        MakeQuad(name, new Vector2((lo + hi) * 0.5f, y), new Vector2(hi - lo, FR_RailThin), color);
    }

    private void MakeQuad(string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(flowRailsRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
    }

    private void MakeDot(Vector2 pos, float d, Color color)
    {
        var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(flowRailsRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(d, d); rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>(); img.sprite = CircleSprite(); img.color = color; img.raycastTarget = false;
    }

    // 런타임 생성 원형 스프라이트(빌트인 Knob 리소스가 런타임에 안 잡혀서 직접 생성). 흰색 + 1px 소프트 엣지, Image.color 로 틴트.
    private static Sprite CircleSprite()
    {
        if (_frCircle != null) return _frCircle;
        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[S * S];
        float r = S * 0.5f - 1f, c = S * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(Mathf.Clamp01(r - dist + 0.5f) * 255f);
                px[y * S + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px); tex.Apply();
        _frCircle = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        return _frCircle;
    }

    // 가동 중 레일을 따라 흐르는 흰 펄스 도트.
    private Image MakePulseDot()
    {
        var go = new GameObject("Pulse", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(flowRailsRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(10f, 10f);
        var img = go.GetComponent<Image>();
        img.sprite = CircleSprite();
        img.color = new Color(1f, 1f, 1f, 0.95f);   // 흰 펄스
        img.raycastTarget = false;
        return img;
    }

    // 설비의 해당 타입 포트들(단자 그리는 순서 = 이 배열 순서). 연결 판정/유입아이템은 각 포트로 BeltSegment 에 질의.
    private BuildPort[] PortsOfType(PortType type)
    {
        var list = new System.Collections.Generic.List<BuildPort>();
        if (_machine != null)
            foreach (var p in _machine.GetComponentsInChildren<BuildPort>())
                if (p != null && p.portType == type) list.Add(p);
        return list.ToArray();
    }

    // ── 현재 생산 공식 스트립 ─────────────────────────────────────
    // 하단 작은 패널에 [재료 아이콘 > 결과 아이콘] 을 채운다(엔필식 요약).
    // 중앙 슬롯은 상호작용용, 이건 "지금 뭘 만드는지" 요약 표시.

    private void BuildFormula()
    {
        if (formulaContent == null || _machine == null) return;

        for (int i = formulaContent.childCount - 1; i >= 0; i--)
            Destroy(formulaContent.GetChild(i).gameObject);

        var recipes = _machine.Recipes;
        if (recipes == null || recipes.Count == 0) return;
        int ri = Mathf.Clamp(_selectedRecipeIndex, 0, recipes.Count - 1);
        var recipe = recipes[ri];
        if (recipe == null) return;

        if (recipe.inputs != null)
            foreach (var inp in recipe.inputs)
                MakeFormulaEntry(inp.itemId, inp.amount, false);

        MakeFormulaArrow();

        if (recipe.outputs != null)
            foreach (var outp in recipe.outputs)
                MakeFormulaEntry(outp.itemId, outp.amount, true);
    }

    private void MakeFormulaEntry(int itemId, int amount, bool isOutput)
    {
        var go = new GameObject(isOutput ? "FxOut" : "FxIn", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(formulaContent, false);
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = 40; le.preferredHeight = 40;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        var irt = iconGo.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
        var img = iconGo.GetComponent<Image>(); img.preserveAspect = true; img.raycastTarget = false;
        var itemData = GameDataUtility.GetItem(itemId);
        img.sprite = itemData != null ? ItemDatabase.GetIcon(itemData.iconKey) : null;
        img.enabled = img.sprite != null;

        var amtGo = new GameObject("Amt", typeof(RectTransform));
        amtGo.transform.SetParent(go.transform, false);
        var amt = amtGo.AddComponent<TextMeshProUGUI>();
        amt.text = "x" + amount; amt.fontSize = 13; amt.color = Color.white;
        amt.alignment = TextAlignmentOptions.BottomRight; amt.fontStyle = FontStyles.Bold;
        amt.raycastTarget = false; amt.textWrappingMode = TextWrappingModes.NoWrap;
        var art = amt.rectTransform;
        art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one; art.offsetMin = Vector2.zero; art.offsetMax = new Vector2(2, 0);
    }

    private void MakeFormulaArrow()
    {
        var go = new GameObject("Arrow", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(formulaContent, false);
        var le = go.GetComponent<LayoutElement>(); le.preferredWidth = 22; le.preferredHeight = 40;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = ">"; tmp.fontSize = 22; tmp.color = new Color(0.90f, 0.76f, 0.29f, 1f);
        tmp.alignment = TextAlignmentOptions.Center; tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false; tmp.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void RefreshRecipeSelectionUI(int totalCount)
    {
        bool multiRecipe = totalCount > 1;
        if (recipePrevBtn != null) recipePrevBtn.gameObject.SetActive(multiRecipe);
        if (recipeNextBtn != null) recipeNextBtn.gameObject.SetActive(multiRecipe);

        if (recipeIndexText != null)
            recipeIndexText.text = multiRecipe ? $"{_selectedRecipeIndex + 1} / {totalCount}" : "";

        if (recipeNameText != null && _machine != null && _machine.Recipes != null
            && _selectedRecipeIndex < _machine.Recipes.Count)
        {
            var recipe = _machine.Recipes[_selectedRecipeIndex];
            recipeNameText.text = !string.IsNullOrEmpty(recipe.recipeName) ? recipe.recipeName : "";
        }
    }

    // ── 출력 슬롯 ───────────────────────────────────────────────

    private void RefreshOutputSlots()
    {
        if (outputSlot == null || _machine == null) return;

        // 출력 버퍼 총량 -> 모두받기 dim(#34) + 출력 펄스(#40) 공통 판정.
        int totalOut = 0;
        var rcs = _machine.Recipes;
        if (rcs != null && rcs.Count > 0)
        {
            int ri = Mathf.Clamp(_selectedRecipeIndex, 0, rcs.Count - 1);
            var outs = rcs[ri]?.outputs;
            if (outs != null)
                foreach (var o in outs) totalOut += _machine.OutputBuffer.GetAmount(o.itemId);
        }
        _hasOutput = totalOut > 0;
        if (takeOutputBtn != null) takeOutputBtn.interactable = _hasOutput;

        // 이전에 동적으로 생성된 추가 슬롯 제거
        foreach (var s in _extraOutputSlots)
            if (s != null) Destroy(s.gameObject);
        _extraOutputSlots.Clear();

        // 현재 선택된 레시피의 outputs 가져오기
        var recipes = _machine.Recipes;
        if (recipes == null || recipes.Count == 0)
        {
            outputSlot.gameObject.SetActive(false);
            return;
        }

        int recipeIdx = Mathf.Clamp(_selectedRecipeIndex, 0, recipes.Count - 1);
        var recipe    = recipes[recipeIdx];
        var outputs   = recipe?.outputs;

        if (outputs == null || outputs.Length == 0)
        {
            outputSlot.gameObject.SetActive(false);
            inventoryDropZone?.SetDropCallback(null);
            TimeKov.UI.HintArrowManager.I?.Hide("machine_output");
            return;
        }

        Transform slotParent = outputSlot.transform.parent;
        int slotIndex = 0; // 실제로 표시된 슬롯 수

        foreach (var output in outputs)
        {
            int buffered = _machine.OutputBuffer.GetAmount(output.itemId);
            if (buffered <= 0) continue; // 버퍼에 없으면 슬롯 표시 안 함

            MachineSlotWidget slot;
            if (slotIndex == 0)
            {
                slot = outputSlot;
                slot.gameObject.SetActive(true);
            }
            else
            {
                var go = Instantiate(outputSlot.gameObject, slotParent);
                slot = go.GetComponent<MachineSlotWidget>();
                _extraOutputSlots.Add(slot);
            }

            int id = output.itemId, amt = buffered;
            slot.Setup(id, amt);
            slot.SetDoubleClickAction(() => TakeOutput(id, amt));

            // 첫 슬롯에만 힌트 화살표
            if (slotIndex == 0)
            {
                inventoryDropZone?.SetDropCallback(TakeOutput);

                var hintCanvas = slot.GetComponentInParent<Canvas>();
                var hintRect   = slot.GetComponent<RectTransform>();
                if (hintCanvas != null && hintRect != null)
                    TimeKov.UI.HintArrowManager.I?.ShowOnUI("machine_output", hintRect, hintCanvas, 0f);
            }

            slotIndex++;
        }

        if (slotIndex == 0) // 현재 레시피 결과물이 버퍼에 없음 → 결과물을 흐리게 미리보기
        {
            outputSlot.gameObject.SetActive(true);
            outputSlot.SetupPreview(outputs[0].itemId);
            outputSlot.SetDoubleClickAction(null);
            inventoryDropZone?.SetDropCallback(null);
            TimeKov.UI.HintArrowManager.I?.Hide("machine_output");
        }
    }

    private void TakeOutput(int itemId, int amount)
    {
        if (_machine == null) return;
        if (!_machine.TryTakeOutput(itemId, amount)) return;

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;
        int leftover = inv != null ? inv.AddItem(itemId, amount) : amount;
        inv?.ForceRefreshUI();

        // 가방에 못 들어간 분량은 창고로 (창고는 거의 무한)
        if (leftover > 0)
        {
            var storage = InventoryManager.StorageInstance;
            if (storage != null) storage.AddItem(itemId, leftover);
            ToastManager.Info("인벤토리가 가득 차 창고로 이동했습니다");
        }

        GameEvents.RaiseItemAcquired(itemId, amount);

        RefreshOutputSlots();
        RefreshInventorySlots();
    }

    // ── 버퍼 변경 콜백 ──────────────────────────────────────────

    private void OnBufferChanged()
    {
        RefreshOutputSlots();
        RefreshInventorySlots();
        foreach (var slot in recipeDropSlots)
            slot?.PublicRefresh();

        ShowRecipeHintIfQuestActive();
    }

    // ── 인벤 드래그 시작/종료에 맞춘 드랍 대상 강조 ──────────────

    /// <summary>인벤토리에서 아이템을 집어들면 그 아이템을 받는 슬롯만 강조한다.</summary>
    private void OnInventoryDragBegin(InventorySlotUI slot)
    {
        if (slot == null || slot.IsEmpty) return;
        int id = slot.SlotData.itemId;

        if (fuelDropSlot != null && id == fuelDropSlot.AcceptedItemId)
            fuelDropSlot.SetDragHighlight(true);

        if (recipeDropSlots != null)
        {
            foreach (var r in recipeDropSlots)
                if (r != null && r.gameObject.activeSelf && r.RequiredItemId == id)
                    r.SetDragHighlight(true);
        }
    }

    private void OnInventoryDragEnd(InventorySlotUI slot)
    {
        ClearDropHighlights();
    }

    private void ClearDropHighlights()
    {
        if (fuelDropSlot != null) fuelDropSlot.SetDragHighlight(false);
        if (recipeDropSlots != null)
        {
            foreach (var r in recipeDropSlots)
                if (r != null) r.SetDragHighlight(false);
        }
    }

    // ── 진행 바 ─────────────────────────────────────────────────

    // 흐름 펄스: 가동 시에만 연결된 레일을 따라 흰 펄스가 좌->우로 흐름. 중앙 화살표도 가동 시 흰 펄스.
    private void UpdateFlowRails(bool operating)
    {
        float t = Time.unscaledTime * FR_PulseSpeed;
        for (int i = 0; i < _railPulses.Count; i++)
        {
            var p = _railPulses[i];
            if (p.dot == null) continue;
            if (p.img != null) p.img.enabled = operating;   // 미가동 = 펄스 숨김
            if (!operating) continue;
            float frac = Mathf.Repeat(t + p.phase, 1f);
            p.dot.anchoredPosition = Vector2.Lerp(p.a, p.b, frac);
        }

        // 벨트 위 아이템: 실제 벨트가 지금 싣고 있는 아이템만 표시(occupant 폴링). 없으면 숨김. 입력=유입/출력=배출 방향으로 흐름 + 양끝 페이드.
        float bt = Time.unscaledTime * FR_BeltItemSpeed;
        for (int i = 0; i < _beltFlows.Count; i++)
        {
            var bf = _beltFlows[i];
            if (bf.rt == null || bf.img == null) continue;
            int id = bf.port != null ? BeltSegment.IncomingItemId(bf.port) : -1;
            if (id < 0) { if (bf.img.enabled) bf.img.enabled = false; bf.shownId = -1; continue; }
            if (id != bf.shownId)   // 실린 아이템이 바뀌면 아이콘 갱신
            {
                var itemData = GameDataUtility.GetItem(id);
                bf.img.sprite = itemData != null ? ItemDatabase.GetIcon(itemData.iconKey) : null;
                bf.shownId = id;
            }
            if (bf.img.sprite == null) { bf.img.enabled = false; continue; }
            bf.img.enabled = true;
            float frac = Mathf.Repeat(bt, 1f);
            bf.rt.anchoredPosition = Vector2.Lerp(bf.a, bf.b, frac);
            var col = bf.img.color; col.a = Mathf.Sin(frac * Mathf.PI); bf.img.color = col;
        }
        // 중앙 화살표 2개: 가동 시 흰 펄스가 좌->우로 지나감(위상차), 미가동 = 회색.
        for (int i = 0; i < _centerChevrons.Count; i++)
        {
            var ch = _centerChevrons[i];
            if (ch == null) continue;
            float w = operating ? Mathf.Clamp01(Mathf.Sin(Time.unscaledTime * 3.2f - i * 0.7f) * 0.5f + 0.5f) : 0f;
            ch.color = Color.Lerp(FR_BusGray, Color.white, w);
        }
    }

    private void Update()
    {
        if (_machine == null || !uiPanel.activeSelf) return;

        // 현재 화면에 표시된 레시피가 실제 생산 중인 레시피와 일치할 때만 진행 바 표시
        var recipes = _machine.Recipes;
        bool isSelectedRecipeActive =
            _machine.ActiveRecipe != null
            && recipes != null
            && _selectedRecipeIndex >= 0
            && _selectedRecipeIndex < recipes.Count
            && recipes[_selectedRecipeIndex] == _machine.ActiveRecipe;

        if (progressBar != null)
            progressBar.value = isSelectedRecipeActive ? _machine.Progress : 0f;

        // 진행바 노브 = fill 끝점으로 이동(가공 중만 표시).
        if (progressKnob != null)
        {
            bool knobShow = isSelectedRecipeActive && _machine.IsProcessing;
            if (progressKnob.gameObject.activeSelf != knobShow) progressKnob.gameObject.SetActive(knobShow);
            if (knobShow && progressBar != null)
            {
                const float fillInset = 2f;   // Fill Area 2px 인셋 보정(fill 시작/끝과 노브 정렬)
                float w = ((RectTransform)progressBar.transform).rect.width;
                var kp = progressKnob.anchoredPosition;
                progressKnob.anchoredPosition = new Vector2(fillInset + progressBar.value * (w - fillInset * 2f), kp.y);
            }
        }

        // 출력 펄스(#40): 받을 결과물 있으면 출력 슬롯 살짝 맥동(수령 유도). unscaled.
        if (outputSlot != null)
        {
            float s = _hasOutput ? 1f + 0.05f * Mathf.PingPong(Time.unscaledTime * 2f, 1f) : 1f;
            outputSlot.transform.localScale = new Vector3(s, s, 1f);
        }

        // 가동 글로우 = 가공 중 노란빛 알파 펄스(unscaled = timeScale 0 안전).
        if (machineGlow != null)
        {
            if (isSelectedRecipeActive && _machine.IsProcessing)
            {
                float a = 0.10f + 0.10f * Mathf.PingPong(Time.unscaledTime * 1.6f, 1f);
                var gc = machineGlow.color; gc.a = a; machineGlow.color = gc;
            }
            else if (machineGlow.color.a != 0f)
            {
                var gc = machineGlow.color; gc.a = 0f; machineGlow.color = gc;
            }
        }

        // [Gauge] 현재 표시된 레시피가 실제 생산 중일 때만 게이지 진행도 동기, 아니면 숨김
        // progressBar 와 같은 조건 — UI 일관성 유지
        // [Sync Fix] StartProcessing() 호출 금지 — 자체 타이머(3.5초 사이클)가 켜져서
        // _machine.Progress(실제 가공 시간) 와 충돌하면 게이지가 "지맘대로" 갔다가 다시 가는 현상 발생.
        // GO만 활성화하고 SetProgress 로만 외부 동기 모드 운영.
        if (processingGauge != null)
        {
            if (isSelectedRecipeActive && _machine.IsProcessing)
            {
                if (!processingGauge.gameObject.activeSelf)
                    processingGauge.gameObject.SetActive(true);
                processingGauge.SetProgress(_machine.Progress);
            }
            else
            {
                if (processingGauge.gameObject.activeSelf)
                    processingGauge.StopAndHide();
            }
        }

        // 공식 스트립 상태 라벨(#31): 생산 중 / 연료 부족 / 대기 중.
        if (formulaStatusText != null)
        {
            if (isSelectedRecipeActive && _machine.IsProcessing)
            { formulaStatusText.text = "생산 중"; formulaStatusText.color = new Color(0.90f, 0.76f, 0.29f, 1f); }
            else if (_machine.Status == MachineStatus.NoFuel)
            { formulaStatusText.text = "연료 부족"; formulaStatusText.color = new Color(0.88f, 0.45f, 0.40f, 1f); }
            else
            { formulaStatusText.text = "대기 중"; formulaStatusText.color = new Color(0.72f, 0.77f, 0.82f, 1f); }
        }

        // 흐름 레일: 가동 시에만 흰 펄스 흐름(연결된 포트 + 중앙 화살표).
        UpdateFlowRails(isSelectedRecipeActive && _machine.IsProcessing);

        if (statusText == null) return;

        if (_machine.Status == MachineStatus.NoFuel)
        {
            // 연료 슬롯에 "연료 넣기" 프롬프트가 떠 있으면(연료 드래그/호버) 같은 자리에 겹치므로 그땐 경고를 숨긴다.
            // (옛 경고기호는 Static 한글 폰트에 없어 깨진 네모로 떠서 텍스트만 남김 - 강조는 statusText 색으로.)
            bool inserting = fuelDropSlot != null && fuelDropSlot.IsInsertPromptVisible;
            statusText.text = inserting ? "" : "연료 부족";              // 연료 칸 위
            if (_processTimeText != null) _processTimeText.text = "";
        }
        else if (isSelectedRecipeActive)
        {
            statusText.text = "";                                         // 연료 부족 칸은 비움
            float remaining = _machine.processingTime * (1f - _machine.Progress);
            if (_processTimeText != null)
                _processTimeText.text = $"{remaining:F0}초";              // 제작 시간은 중앙(진행바 위)
        }
        else
        {
            statusText.text = "";
            if (_processTimeText != null) _processTimeText.text = "";
        }
    }

    // ── 재료 회수 버튼 (InputBuffer → 인벤토리) ─────────────────

    /// <summary>현재 선택된 레시피의 재료(InputBuffer)만 인벤토리로 반환한다.</summary>
    public void TakeAllInputs()
    {
        if (_machine == null) return;

        var recipes = _machine.Recipes;
        if (recipes == null || recipes.Count == 0) return;

        int recipeIdx = Mathf.Clamp(_selectedRecipeIndex, 0, recipes.Count - 1);
        var inputs    = recipes[recipeIdx]?.inputs;
        if (inputs == null) return;

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;

        foreach (var input in inputs)
        {
            int buffered = _machine.InputBuffer.GetAmount(input.itemId);
            if (buffered <= 0) continue;
            _machine.InputBuffer.Consume(input.itemId, buffered);
            inv?.AddItem(input.itemId, buffered);
        }

        _machine.PublicNotifyBufferChanged();
        RefreshInventorySlots();
        foreach (var slot in recipeDropSlots)
            slot?.PublicRefresh();
    }

    // ── 모두 받기 버튼 (OutputBuffer → 인벤토리) ────────────────

    /// <summary>현재 선택된 레시피의 OutputBuffer 결과물을 전부 인벤토리로 가져온다.</summary>
    public void TakeAll()
    {
        if (_machine == null) return;
        GameSfx.Play(SfxId.MachineTakeOutput);

        var inv = playerInventory != null ? playerInventory : InventoryManager.Instance;

        var recipes = _machine.Recipes;
        if (recipes != null && recipes.Count > 0)
        {
            int recipeIdx = Mathf.Clamp(_selectedRecipeIndex, 0, recipes.Count - 1);
            var outputs   = recipes[recipeIdx]?.outputs;
            if (outputs != null)
            {
                bool movedToStorage = false;
                var storage = InventoryManager.StorageInstance;
                foreach (var output in outputs)
                {
                    int buffered = _machine.OutputBuffer.GetAmount(output.itemId);
                    if (buffered > 0 && _machine.TryTakeOutput(output.itemId, buffered))
                    {
                        int leftover = inv != null ? inv.AddItem(output.itemId, buffered) : buffered;
                        if (leftover > 0 && storage != null) { storage.AddItem(output.itemId, leftover); movedToStorage = true; }
                        GameEvents.RaiseItemAcquired(output.itemId, buffered);
                    }
                }
                if (movedToStorage) ToastManager.Info("인벤토리가 가득 차 창고로 이동했습니다");
            }
        }

        _machine.PublicNotifyBufferChanged();
        _machine.ResetStatusIfIdle();
        RefreshInventorySlots();
        RefreshOutputSlots();
    }

    // ── HintArrow 가이드 ─────────────────────────────────────────

    const string FirstMachineHintKey = "HintArrow_FirstMachineOpen";

    void ShowFirstMachineHintIfNeeded()
    {
        if (PlayerPrefs.GetInt(FirstMachineHintKey, 0) == 1) return;
        if (_invSlots == null || _invSlots.Count == 0) return;

        var firstSlot = _invSlots[0];
        if (firstSlot == null) return;

        var canvas = firstSlot.GetComponentInParent<Canvas>();
        var rect = firstSlot.GetComponent<RectTransform>();
        var mgr = TimeKov.UI.HintArrowManager.I;
        if (mgr == null || canvas == null || rect == null) return;

        mgr.ShowOnUI("first_machine_hint", rect, canvas, 5f);
        PlayerPrefs.SetInt(FirstMachineHintKey, 1);
        PlayerPrefs.Save();
    }

    void ShowRecipeHintIfQuestActive()
    {
        var mgr = TimeKov.UI.HintArrowManager.I;
        if (mgr == null) return;

        if (QuestManager.Instance == null || _machine == null || recipeDropSlots == null)
        {
            mgr.Hide("recipe_slot_hint");
            return;
        }

        int targetItemId = 0;
        foreach (var rt in QuestManager.Instance.Runtimes)
        {
            if (rt?.activeObjectives == null) continue;
            foreach (var obj in rt.activeObjectives)
            {
                if (obj is FacilityInputObjective inp && !inp.IsCompleted)
                {
                    if (inp.facilityId == 0 || inp.facilityId == _machine.FacilityId)
                    {
                        int have = _machine.InputBuffer.GetAmount(inp.inputItemId);
                        if (have >= inp.requiredCount) continue;
                        targetItemId = inp.inputItemId;
                        break;
                    }
                }
            }
            if (targetItemId != 0) break;
        }

        if (targetItemId == 0)
        {
            mgr.Hide("recipe_slot_hint");
            return;
        }

        foreach (var slot in recipeDropSlots)
        {
            if (slot == null || !slot.gameObject.activeSelf) continue;
            if (slot.RequiredItemId != targetItemId) continue;

            var canvas = slot.GetComponentInParent<Canvas>();
            var rect = slot.GetComponent<RectTransform>();
            if (canvas != null && rect != null)
            {
                mgr.ShowOnUI("recipe_slot_hint", rect, canvas, 0f);
                return;
            }
        }

        mgr.Hide("recipe_slot_hint");
    }
}
