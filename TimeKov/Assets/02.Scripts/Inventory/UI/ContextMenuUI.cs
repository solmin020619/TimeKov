// ContextMenuUI.cs
// 우클릭 컨텍스트 메뉴. 호버 툴팁(이름/등급만)과 달리, 우클릭하면
// 아이템 정보 + 사용 효과 + 행동 버튼(사용 / 퀵슬롯 등록 / 분할 / 버리기)을 한 패널에 보여준다.
// UI는 런타임 생성(ItemTooltipUI/PlayerHudUI 와 같은 방식). 디자인 스프라이트는 추후 교체.
// 공개 API(Open/Close/TryCloseOnOutsideClick/IsOpen)는 그대로라 InventoryUIController 수정 불필요.

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ContextMenuUI : MonoBehaviour
{
    [Header("스킨(비우면 절차 생성 사용)")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite buttonSprite;

    // 차가운 간유리 톤 (가방 UI와 통일)
    private static readonly Color PanelColor    = new Color32(26, 30, 38, 240);
    private static readonly Color EffectBoxColor = new Color32(16, 19, 25, 205);
    private static readonly Color BtnColor      = new Color32(52, 60, 74, 255);
    private static readonly Color BtnUseColor   = new Color32(45, 96, 140, 255);
    private static readonly Color BtnQuickColor = new Color32(58, 92, 66, 255);
    private static readonly Color BtnTrashColor = new Color32(96, 50, 52, 255);
    private static readonly Color TextColor     = new Color32(225, 232, 240, 255);
    private static readonly Color DimTextColor  = new Color32(150, 160, 172, 255);

    private const float MenuWidth = 230f;

    // 카테고리 한글 (ItemCategory 순서)
    private static readonly string[] CategoryNames =
        { "원재료", "1차 가공품", "2차 가공품", "전술 소모품", "핵심 강화", "특수" };

    private InventorySlotUI _currentSlot;
    private RectTransform _rect;
    private RectTransform _canvasRect;
    private Canvas _canvas;

    private bool _built;
    private Image _iconImg;
    private TextMeshProUGUI _nameText;
    private TextMeshProUGUI _categoryText;
    private GameObject _effectBox;
    private TextMeshProUGUI _effectText;
    private GameObject _consumableRow;     // 사용/퀵슬롯 등록 행 (소모품만 표시)
    private Button _splitBtn;
    private Button _quickBtn;              // 퀵슬롯 등록 (회복 앰플만 노출)
    private Sprite _sEffect, _btnNormal, _btnHover, _btnPressed;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        ResolveCanvas();
        EnsureBuilt();
        gameObject.SetActive(false);
    }

    private void ResolveCanvas()
    {
        _rect = GetComponent<RectTransform>();
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            _canvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            _canvasRect = _canvas.GetComponent<RectTransform>();
        }
    }

    // ── 런타임 UI 구성 (1회) ─────────────────────────────────────────────
    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;

        if (_rect == null) _rect = GetComponent<RectTransform>();

        // 구버전 프리팹 자식(예전 버튼들) 제거 후 새로 구성
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0f, 1f);   // 좌상단 기준(커서 아래로 펼침)

        // 디자인 스프라이트 로드(Resources/ContextQuickslot). 없으면 절차 생성으로 폴백.
        var sPanel  = panelSprite  != null ? panelSprite  : Load("ctx_panel");
        _sEffect    = Load("ctx_effect_box");
        _btnNormal  = buttonSprite != null ? buttonSprite : Load("ctx_btn_normal");
        _btnHover   = Load("ctx_btn_hover");
        _btnPressed = Load("ctx_btn_pressed");

        var bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.sprite = sPanel != null ? sPanel : UISpriteFactory.RoundedRect(48, 14);
        bg.type = Image.Type.Sliced;
        bg.color = sPanel != null ? Color.white : PanelColor;
        bg.raycastTarget = true;

        var vlg = GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _rect.sizeDelta = new Vector2(MenuWidth, _rect.sizeDelta.y);

        BuildHeader();
        BuildEffect();
        BuildButtons();
    }

    private void BuildHeader()
    {
        var row = NewRow("Header", 42f);
        var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 8f;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = true;
        hl.childAlignment = TextAnchor.MiddleLeft;

        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(row, false);
        _iconImg = iconGo.AddComponent<Image>();
        _iconImg.preserveAspect = true;
        _iconImg.raycastTarget = false;
        var iconLe = iconGo.AddComponent<LayoutElement>();
        iconLe.preferredWidth = 40f;
        iconLe.preferredHeight = 40f;
        iconLe.minWidth = 40f;

        var col = new GameObject("Texts", typeof(RectTransform));
        col.transform.SetParent(row, false);
        var cvl = col.AddComponent<VerticalLayoutGroup>();
        cvl.spacing = 1f;
        cvl.childControlWidth = true;
        cvl.childControlHeight = true;
        cvl.childForceExpandWidth = true;
        cvl.childForceExpandHeight = false;
        cvl.childAlignment = TextAnchor.MiddleLeft;
        var colLe = col.AddComponent<LayoutElement>();
        colLe.flexibleWidth = 1f;

        _nameText = NewText(col.transform, "Name", 19f, FontStyles.Bold, TextColor);
        _categoryText = NewText(col.transform, "Category", 13f, FontStyles.Normal, DimTextColor);
    }

    private void BuildEffect()
    {
        _effectBox = new GameObject("EffectBox", typeof(RectTransform));
        _effectBox.transform.SetParent(transform, false);

        var img = _effectBox.AddComponent<Image>();
        img.sprite = _sEffect != null ? _sEffect : UISpriteFactory.RoundedRect(32, 10);
        img.type = Image.Type.Sliced;
        img.color = _sEffect != null ? Color.white : EffectBoxColor;
        img.raycastTarget = false;

        var vl = _effectBox.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(9, 9, 7, 7);
        vl.spacing = 2f;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        var label = NewText(_effectBox.transform, "EffLabel", 12f, FontStyles.Bold, new Color32(120, 170, 210, 255));
        label.text = "사용 효과";
        _effectText = NewText(_effectBox.transform, "EffText", 14f, FontStyles.Normal, TextColor);
    }

    private void BuildButtons()
    {
        var row1 = NewButtonRow("BtnRow1");
        _consumableRow = row1.gameObject;
        BuildButton(row1, "사용", BtnUseColor, OnClickUse);
        _quickBtn = BuildButton(row1, "퀵슬롯 등록", BtnQuickColor, OnClickRegisterQuick);

        var row2 = NewButtonRow("BtnRow2");
        _splitBtn = BuildButton(row2, "분할", BtnColor, OnClickSplit);
        BuildButton(row2, "버리기", BtnTrashColor, OnClickTrash);
    }

    // ── 열기 ─────────────────────────────────────────────────────────────
    public void Open(InventorySlotUI slot, Vector2 screenPos)
    {
        if (slot == null || slot.IsEmpty) return;
        EnsureBuilt();
        _currentSlot = slot;

        var data = ItemDatabase.GetItem(slot.SlotData.itemId);
        bool isConsumable = data != null && data.itemCategory == ItemCategory.TacticalConsumable;

        // 헤더(아이콘 + 이름 + 등급/카테고리)
        if (_iconImg != null)
        {
            var icon = data != null ? ItemDatabase.GetIcon(data.iconKey) : null;
            _iconImg.sprite = icon;
            _iconImg.enabled = icon != null;
        }
        if (_nameText != null)
        {
            if (data != null)
            {
                int g = (int)data.itemGrade;
                _nameText.text = data.itemName;
                _nameText.color = GradeVisual.IsCommon(g) ? TextColor : GradeVisual.GetColor(g);
            }
            else { _nameText.text = "알 수 없는 아이템"; _nameText.color = TextColor; }
        }
        if (_categoryText != null)
        {
            if (data != null)
            {
                int g = (int)data.itemGrade;
                _categoryText.text =
                    $"<color=#{GradeVisual.GetColorHex(g)}>{GradeVisual.GetName(g)}</color>  {CategoryName(data.itemCategory)}";
            }
            else _categoryText.text = "";
        }

        // 사용 효과(소모품만)
        string eff = isConsumable ? ConsumableEffectApplier.Describe(slot.SlotData.itemId.ToString()) : "";
        bool showEffect = !string.IsNullOrEmpty(eff);
        if (_effectBox != null) _effectBox.SetActive(showEffect);
        if (showEffect && _effectText != null) _effectText.text = eff;

        // 버튼: 사용/퀵슬롯 등록 행은 소모품만(회복/스탯 앰플 다 등록 가능), 분할은 2개 이상일 때만
        if (_consumableRow != null) _consumableRow.SetActive(isConsumable);
        if (_quickBtn != null) _quickBtn.gameObject.SetActive(isConsumable);
        if (_splitBtn != null) _splitBtn.interactable = slot.SlotData.amount > 1;

        gameObject.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
        SetPosition(screenPos);
    }

    public void Close()
    {
        _currentSlot = null;
        if (gameObject != null) gameObject.SetActive(false);
    }

    // ── 버튼 동작 ────────────────────────────────────────────────────────
    private void OnClickUse()
    {
        if (_currentSlot == null) return;

        var owner = _currentSlot.Owner;
        // itemId를 미리 캡처 - TryConsumeItem이 slot.Clear()를 호출하면 itemId가 -1로 바뀜
        int itemId = _currentSlot.SlotData.itemId;
        var player = FindAnyObjectByType<Player>();

        // 만피 회복 앰플은 소비하지 않고 토스트만(아이템 보존)
        if (!ConsumableEffectApplier.CanApply(itemId.ToString(), player))
        {
            ToastManager.Warning("시간이 이미 가득 찼습니다.");
            Close();
            return;
        }

        bool consumed = owner != null && owner.TryConsumeItem(itemId, 1);
        if (!consumed) { Close(); return; }

        bool applied = ConsumableEffectApplier.Apply(itemId.ToString(), player);
        if (!applied) owner.AddItem(itemId, 1);   // 실패 시 복구

        Close();
    }

    private void OnClickRegisterQuick()
    {
        if (_currentSlot == null) return;
        int itemId = _currentSlot.SlotData.itemId;

        var player = FindAnyObjectByType<Player>();
        if (player == null || player.QuickSlot == null)
        {
            ToastManager.Warning("퀵슬롯을 사용할 수 없습니다.");
            Close();
            return;
        }

        player.QuickSlot.Register(itemId);
        Close();
    }

    private void OnClickSplit()
    {
        if (_currentSlot == null) return;
        InventoryUIController.Instance?.OpenSplitPopup(_currentSlot);
        Close();
    }

    private void OnClickTrash()
    {
        if (_currentSlot == null) return;
        InventoryUIController.Instance?.OpenTrashConfirm(_currentSlot);
        Close();
    }

    // 외부 클릭 감지(InventoryUIController.Update 에서 호출)
    public void TryCloseOnOutsideClick()
    {
        if (!IsOpen) return;
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? _canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, cam))
                Close();
        }
    }

    // ── 위치(좌상단 기준, 화면 안으로 클램프) ────────────────────────────
    private void SetPosition(Vector2 screenPos)
    {
        if (_rect == null || _canvasRect == null) return;

        Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? _canvas.worldCamera : null;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, cam, out localPos);

        float w = _rect.rect.width;
        float h = _rect.rect.height;
        float halfW = _canvasRect.rect.width * 0.5f;
        float halfH = _canvasRect.rect.height * 0.5f;

        localPos.x += 2f;   // 커서 우하단으로 살짝
        localPos.y -= 2f;

        localPos.x = Mathf.Clamp(localPos.x, -halfW, halfW - w);
        localPos.y = Mathf.Clamp(localPos.y, -halfH + h, halfH);

        _rect.anchoredPosition = localPos;
    }

    // ── 생성 헬퍼 ────────────────────────────────────────────────────────
    private RectTransform NewRow(string name, float minHeight)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = minHeight;
        return (RectTransform)go.transform;
    }

    private RectTransform NewButtonRow(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var hl = go.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 6f;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = true;
        hl.childForceExpandHeight = true;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 34f;
        return (RectTransform)go.transform;
    }

    private Button BuildButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        bool hasSkin = _btnNormal != null;
        img.sprite = hasSkin ? _btnNormal : UISpriteFactory.RoundedRect(32, 9);
        img.type = Image.Type.Sliced;
        // 디자인 버튼은 받은 그대로(중립). 디자인 없을 때만 행동별 색으로 폴백.
        img.color = hasSkin ? Color.white : color;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        if (hasSkin && _btnHover != null && _btnPressed != null)
        {
            btn.transition = Selectable.Transition.SpriteSwap;
            var ss = btn.spriteState;
            ss.highlightedSprite = _btnHover;
            ss.pressedSprite = _btnPressed;
            ss.selectedSprite = _btnNormal;
            btn.spriteState = ss;
        }
        btn.onClick.AddListener(onClick);

        var t = NewText(go.transform, "Label", 14f, FontStyles.Bold, TextColor);
        t.text = label;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        return btn;
    }

    private TextMeshProUGUI NewText(Transform parent, string name, float size, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.richText = true;
        t.raycastTarget = false;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    private static string CategoryName(ItemCategory c)
    {
        int i = Mathf.Clamp((int)c, 0, CategoryNames.Length - 1);
        return CategoryNames[i];
    }

    private static Sprite Load(string n) => Resources.Load<Sprite>("ContextQuickslot/" + n);
}
