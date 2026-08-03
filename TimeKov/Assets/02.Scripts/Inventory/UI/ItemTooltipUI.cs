// ItemTooltipUI.cs
// 툴팁 컴포넌트를 관리하는 스크립트
// 슬롯 호버 시 아이템 정보 표시 (인벤토리/창고 슬롯 + 설비 UI 슬롯 공용)

using UnityEngine;
using TMPro;

public class ItemTooltipUI : MonoBehaviour
{
    /// <summary>씬에 하나 존재하는 툴팁. 설비 UI 등 외부에서 itemId로 직접 호출할 때 사용.</summary>
    public static ItemTooltipUI Instance { get; private set; }

    [Header("텍스트 필드")]
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI categoryText;

    [Header("위치 오프셋")]
    [SerializeField] private Vector2 offset = new Vector2(15f, -15f);

    private RectTransform _rect;
    private RectTransform _canvasRect;
    private Canvas _canvas;
    private bool _isShowing;
    private int _currentItemId = -1;
    private bool _currentChestMode;

    // 카테고리 이름 한글 테이블 (ItemCategory 열거형 순서와 일치)
    private static readonly string[] CategoryNames = new string[]
    {
        "원재료",       // RawMaterial
        "1차 가공품",   // ProcessedTier1
        "2차 가공품",   // ProcessedTier2
        "전술 소모품",  // TacticalConsumable
        "핵심 강화",    // CoreUpgrade
        "특수"          // Special
    };

    private void Awake()
    {
        Instance = this;
        _rect = GetComponent<RectTransform>();

        // 항상 최상단에 그려지게 - 자체 Canvas overrideSorting (인벤 패널 뒤로 숨는 문제 해결)
        var ownCanvas = GetComponent<Canvas>();
        if (ownCanvas == null) ownCanvas = gameObject.AddComponent<Canvas>();
        ownCanvas.overrideSorting = true;
        ownCanvas.sortingOrder = 32000;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) AttachToCanvas(canvas);

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!_isShowing) return;

        // UI가 모두 닫혔는데 툴팁이 남아있으면 자동 숨김
        // (SetActive(false)로 UI가 꺼질 때 OnPointerExit가 발동되지 않는 Unity 특성 대응)
        var uic = GameUIController.Instance;
        if (uic != null && !uic.IsUIBlocking())
        {
            Hide();
            return;
        }

        UpdatePosition(Input.mousePosition);
    }

    // ── 표시 (인벤토리/창고 슬롯) ──────────────────────────────────────────
    public void Show(InventorySlotUI slot)
    {
        if (slot == null || slot.IsEmpty) return;

        // UI가 닫혀 있으면 표시하지 않음
        var uic = GameUIController.Instance;
        if (uic != null && !uic.IsUIBlocking()) return;

        // 파밍 상자 슬롯이면 회수 안내 위주(이름만). 가방/창고는 평소대로.
        bool chestMode = slot.Owner != null && slot.Owner == InventoryManager.ChestInstance;
        ShowItem(slot.SlotData.itemId, slot.GetComponentInParent<Canvas>(), chestMode);
    }

    // ── 표시 (설비 UI 등 외부 — itemId 직접 지정) ──────────────────────────
    public void Show(int itemId, Canvas hostCanvas)
    {
        if (itemId <= 0) return;

        var uic = GameUIController.Instance;
        if (uic != null && !uic.IsUIBlocking()) return;

        ShowItem(itemId, hostCanvas, false);
    }

    // ── 공통 표시 처리 ─────────────────────────────────────────────────────
    private void ShowItem(int itemId, Canvas hostCanvas, bool chestMode = false)
    {
        // 같은 아이템에 이미 표시 중이면 위치만 갱신 (깜빡임 방지)
        if (_isShowing && itemId == _currentItemId && chestMode == _currentChestMode)
        {
            UpdatePosition(Input.mousePosition);
            return;
        }
        _currentItemId = itemId;
        _currentChestMode = chestMode;

        var data = ItemDatabase.GetItem(itemId);

        // 아이템 이름 = 등급 색 (일반은 흰색). 등급이 한눈에 느껴지게.
        if (itemNameText != null)
        {
            if (data != null)
            {
                int g = (int)data.itemGrade;
                itemNameText.text = data.GetLocalizedName();
                itemNameText.color = GradeVisual.IsCommon(g) ? Color.white : GradeVisual.GetColor(g);
            }
            else
            {
                itemNameText.text = Loc.Get("알 수 없는 아이템");
                itemNameText.color = Color.white;
            }
        }

        // 카테고리 줄
        if (categoryText != null)
        {
            if (chestMode)
            {
                // 파밍 상자 = 회수 안내 위주(상세는 가방에 넣으면 거기서 호버 시)
                categoryText.richText = true;
                categoryText.text = $"<color=#9FB0C0>{Loc.Get("우클릭 / 더블클릭으로 회수")}</color>";
            }
            else if (data != null)
            {
                // 평소 = 등급이름(등급색) + 카테고리 (예: 고급  원재료)
                int g = (int)data.itemGrade;
                int catIndex = Mathf.Clamp((int)data.itemCategory, 0, CategoryNames.Length - 1);
                categoryText.richText = true;
                categoryText.text = $"<color=#{GradeVisual.GetColorHex(g)}>{GradeVisual.GetName(g)}</color>  {Loc.Get(CategoryNames[catIndex])}";
            }
            else
            {
                categoryText.text = "";
            }
        }

        // 호출한 슬롯이 속한 캔버스로 옮겨 그 위에 렌더 (설비 UI 위에도 정상 표시)
        if (hostCanvas != null) AttachToCanvas(hostCanvas);

        _isShowing = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        UpdatePosition(Input.mousePosition);
    }

    // 툴팁 숨김
    public void Hide()
    {
        _isShowing = false;
        _currentItemId = -1;
        _currentChestMode = false;
        gameObject.SetActive(false);
    }

    // 툴팁을 지정 캔버스의 자식으로 옮기고 기준 RectTransform 갱신
    private void AttachToCanvas(Canvas canvas)
    {
        if (canvas == null) return;
        var root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
        if (_canvas == root) return;

        _canvas = root;
        _canvasRect = root.GetComponent<RectTransform>();
        transform.SetParent(root.transform, false);
    }

    // 위치 갱신 (마우스 클램프)
    private void UpdatePosition(Vector2 screenPos)
    {
        if (_rect == null || _canvasRect == null) return;

        // 캔버스 렌더 모드에 맞는 카메라 선택 (Overlay면 null)
        Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? _canvas.worldCamera : null;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, screenPos, cam, out localPos);

        localPos += offset;

        float halfW = _rect.sizeDelta.x * 0.5f;
        float halfH = _rect.sizeDelta.y * 0.5f;
        float maxX = _canvasRect.rect.width * 0.5f - halfW;
        float maxY = _canvasRect.rect.height * 0.5f - halfH;

        localPos.x = Mathf.Clamp(localPos.x, -maxX, maxX);
        localPos.y = Mathf.Clamp(localPos.y, -maxY, maxY);

        _rect.anchoredPosition = localPos;
    }
}
