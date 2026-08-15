using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventorySlotUI : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private Image bgImage;
    [SerializeField] private Image rarityBorder;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private GameObject newBadge;
    [SerializeField] private GameObject countChip;   // 우상단 수량 칩 배경 (수량 2 이상일 때만 표시)
    [SerializeField] private GameObject iconBacking; // 아이콘 뒤 어두운 backing (아이템 있을 때만 표시)
    [SerializeField] private Image gradeAurora;      // 등급 오로라 (하단서 위로 번지는 그라데이션, 런타임 등급색 틴트)
    [SerializeField] private Image consumableBadge;  // 소모품 표시 배지 (소모품 카테고리일 때만 좌상단 표시, item_marker)

    [SerializeField] private Color normalColor = new Color(0.18f, 0.22f, 0.30f, 1f);
    [SerializeField] private Color emptyBorderColor = new Color(0.3f, 0.3f, 0.3f, 0f);
    [SerializeField] private Color dragColor = new Color(1f, 1f, 1f, 0.4f);
    // 호버 시 슬롯에 "불 들어오는" 강조색 (선택색보다 밝게)
    [SerializeField] private Color hoverColor = new Color(0.45f, 0.62f, 0.95f, 0.5f);
    [SerializeField] private Color normalBorderColor = new Color(0.627f, 0.745f, 0.855f, 0.34f);  // 평소 테두리(크롬)
    [SerializeField] private Color hoverBorderColor  = new Color(0.37f, 0.77f, 1f, 0.9f);          // 호버 테두리(시안)

    [Tooltip("드롭 대상으로 강조될 때 슬롯이 커지는 배율 (살짝 커지는 팝). 1=안 커짐.")]
    [SerializeField] private float dropPopScale = 1.08f;

    // 등급 바/오로라 지오메트리(빌더값 대신 코드 통제 = 굵은 바닥선 + 그 위에서 시작하는 은은 오로라, 선/오로라 딱 구분).
    private const float GradeBarH     = 6f;   // 바닥 등급선 두께(솔리드 크리스프 직선)
    private const float GradeBarInsetX = 0f;  // 선 좌우 인셋 0 = 슬롯 폭에 딱 맞춤(선이 슬롯과 어긋나 보이던 것 통일)
    private const float GradeAuroraH  = 30f;  // 오로라 높이(선 위에서 시작해 위로, 별개 효과)
    private const float GradeAuroraA  = 0.6f; // 오로라 강도(0.4는 안 보여서 업). 설비 슬롯들과 같은 값 유지할 것

    // 등급 색은 공용 GradeVisual 로 이동(중앙화). 일반(0)은 인벤에서 숨김.

    private InventorySlot _slot;
    private InventoryManager _owner;
    private bool _isHovered;
    private bool _dropHighlight;   // 드래그 입고 대상으로 강조 (호버와 별개)
    private UnityEngine.UI.Outline _outline;   // 호버 시 테두리 시안 전환용 (루트 Outline)
    private Image _dropFrame;   // 드롭 대상 강조 흰 프레임(런타임 생성, 9-slice). 없으면 Outline 폴백.
    private bool _wasDragSource;   // 드래그 출발 슬롯 회색처리 상태 추적

    public static event Action<InventorySlotUI> OnAnySlotClicked;
    public static event Action<InventorySlotUI> OnAnySlotDoubleClicked;
    public static event Action<InventorySlotUI> OnAnySlotRightClicked;
    public static event Action<InventorySlotUI> OnAnySlotHoverEnter;
    public static event Action<InventorySlotUI> OnAnySlotHoverExit;
    public static event Action<InventorySlotUI> OnAnySlotDragBegin;  // 드래그 시작
    public static event Action<InventorySlotUI> OnAnySlotDragEnd;    // 드래그 종료
    public static event Action<InventorySlotUI> OnAnySlotDropped;    // 드랍 수신

    public InventorySlot SlotData => _slot;
    public InventoryManager Owner => _owner;
    public bool IsEmpty => _slot == null || _slot.IsEmpty;

    private const float DropPopSpeed = 1.0f;   // 팝 스케일 변화 속도(초당)

    // -- 획득 반짝 --------------------------------------------------------
    // 방금 들어온 칸을 잠깐 하얗게 훑는다. 어느 칸으로 들어갔는지 눈으로 잡으라고.
    // 루트 스케일이 아니라 별도 오버레이를 쓴다 - 스케일은 드롭 팝/촤라락 등장이 이미 쓰고 있어서
    // 같이 쓰면 서로 밟는다.
    private const float FlashRise = 0.07f;   // 켜지는 시간(짧게 = "반짝")
    private const float FlashFall = 0.38f;   // 꺼지는 시간(길게 = 잔광)
    private const float FlashPeak = 0.30f;   // 가장 밝을 때 알파. 아이콘이 안 보일 만큼 덮으면 안 된다
    private Image _acquireFlash;
    private Coroutine _acquireFlashCo;

    private void OnDisable()
    {
        // 비활성화 시 Unity가 OnPointerExit를 호출하지 않으므로 호버 상태를 직접 리셋한다.
        // (안 하면 패널을 닫았다 다시 열 때 마우스가 없던 칸이 강조된 채로 남는다)
        _isHovered = false;
        _dropHighlight = false;
        _wasDragSource = false;
        transform.localScale = Vector3.one;   // 팝 스케일 잔재 제거
        if (_dropFrame != null) { SetFrameAlpha(0f); }   // 프레임 잔재 제거

        // 반짝이 도는 중에 패널이 닫히면 흰 오버레이가 켜진 채로 굳는다.
        if (_acquireFlashCo != null) { StopCoroutine(_acquireFlashCo); _acquireFlashCo = null; }
        if (_acquireFlash != null) _acquireFlash.gameObject.SetActive(false);

        // 이 칸을 드래그 중인데 패널이 닫히면(ESC 등) OnEndDrag 가 영영 안 와서
        // 고스트 아이콘이 화면에 영구 잔존한다 - 여기서 드래그를 강제 종료.
        var dh = InventoryDragHandler.Instance;
        if (dh != null && dh.DraggedSlot == this) dh.EndDrag();
    }

    private void Update()
    {
        DriveDropFrame();
        DriveDragSourceDim();

        // 드롭 대상일 때 살짝 커지는 팝. _dropHighlight on -> dropPopScale, off -> 1.
        // 주의: 카테고리 전환 촤라락(InventoryGridUI.RevealRoutine)도 루트 스케일을 쓰므로,
        //       강조가 아닌데 스케일 < 1 이면 리빌 등장 중이니 건드리지 않는다.
        float target = _dropHighlight ? dropPopScale : 1f;
        float s = transform.localScale.x;
        if (Mathf.Approximately(s, target)) return;
        if (!_dropHighlight && s < 0.999f) return;
        float ns = Mathf.MoveTowards(s, target, DropPopSpeed * Time.unscaledDeltaTime);
        transform.localScale = new Vector3(ns, ns, ns);
    }

    // 드롭 대상 흰 프레임 페이드 + 호흡(알파 0.75<->1). 프레임 없으면(미연결) 무시 -> Outline 폴백.
    private void DriveDropFrame()
    {
        if (_dropFrame == null) return;
        float a = _dropFrame.color.a;
        if (_dropHighlight)
        {
            float pulse = 0.875f + 0.125f * Mathf.Sin(Time.unscaledTime * 5f);   // 0.75..1.0
            SetFrameAlpha(Mathf.MoveTowards(a, pulse, 5f * Time.unscaledDeltaTime));
        }
        else if (a > 0f)
        {
            SetFrameAlpha(Mathf.MoveTowards(a, 0f, 6f * Time.unscaledDeltaTime));
        }
    }

    // 드래그 출발 슬롯이면 흐리게(회색) 처리 -> 어느 칸을 들었는지 시각 피드백. 상태 바뀔 때만 갱신.
    private void DriveDragSourceDim()
    {
        var dh = InventoryDragHandler.Instance;
        bool isSource = dh != null && dh.IsDragging && dh.DraggedSlot == this;
        if (isSource == _wasDragSource) return;
        _wasDragSource = isSource;
        if (isSource)
        {
            if (itemIcon != null) itemIcon.color = new Color(1f, 1f, 1f, 0.22f);
            if (countText != null) countText.gameObject.SetActive(false);
            if (countChip != null) countChip.SetActive(false);
        }
        else
        {
            Refresh(_slot, _owner);   // 복원(현재 데이터 기준 재바인딩)
        }
    }

    private void SetFrameAlpha(float a)
    {
        if (_dropFrame == null) return;
        var c = _dropFrame.color; c.a = a; _dropFrame.color = c;
        bool on = a > 0.001f;
        if (_dropFrame.gameObject.activeSelf != on) _dropFrame.gameObject.SetActive(on);
    }

    // 그리드(InventoryGridUI)가 슬롯 생성 시 1회 호출. 흰 9-slice 프레임 child 생성(슬롯보다 살짝 바깥).
    public void InitDropFrame(Sprite frameSprite)
    {
        if (frameSprite == null || _dropFrame != null) return;
        var go = new GameObject("DropFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-5f, -5f); rt.offsetMax = new Vector2(5f, 5f);   // 슬롯보다 살짝 바깥
        var img = go.GetComponent<Image>();
        img.sprite = frameSprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 2f;   // @2x 에셋 -> 코너 논리 22px (코너 = border/multiplier)
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);   // 흰색, 시작 투명 (시안 액센트는 텍스처에 베이크됨)
        go.transform.SetAsLastSibling();
        go.SetActive(false);
        _dropFrame = img;
    }

    /// <summary>이 칸에 방금 아이템이 들어왔다는 반짝. delay 는 여러 칸을 차례로 터뜨릴 때 쓴다.</summary>
    public void PlayAcquireFlash(float delay = 0f)
    {
        if (!isActiveAndEnabled) return;
        EnsureAcquireFlash();
        if (_acquireFlash == null) return;
        if (_acquireFlashCo != null) StopCoroutine(_acquireFlashCo);
        _acquireFlashCo = StartCoroutine(AcquireFlashRoutine(delay));
    }

    // 흰 오버레이는 처음 반짝일 때만 만든다(모든 칸에 미리 만들면 안 쓰는 칸까지 드로우콜).
    private void EnsureAcquireFlash()
    {
        if (_acquireFlash != null) return;
        var go = new GameObject("AcquireFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        // 칸 배경과 같은 모양으로 덮는다(둥근 칸이면 둥글게). 배경이 없으면 그냥 사각.
        if (bgImage != null)
        {
            img.sprite = bgImage.sprite;
            img.type = bgImage.type;
            img.pixelsPerUnitMultiplier = bgImage.pixelsPerUnitMultiplier;
        }
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);
        go.transform.SetAsLastSibling();
        go.SetActive(false);
        _acquireFlash = img;
    }

    private System.Collections.IEnumerator AcquireFlashRoutine(float delay)
    {
        float t = 0f;
        while (t < delay) { t += Time.unscaledDeltaTime; yield return null; }

        _acquireFlash.gameObject.SetActive(true);

        t = 0f;
        while (t < FlashRise)
        {
            t += Time.unscaledDeltaTime;
            SetAcquireFlashAlpha(FlashPeak * Mathf.Clamp01(t / FlashRise));
            yield return null;
        }

        t = 0f;
        while (t < FlashFall)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / FlashFall);
            SetAcquireFlashAlpha(FlashPeak * (1f - k) * (1f - k));   // 끝으로 갈수록 천천히 사라짐
            yield return null;
        }

        SetAcquireFlashAlpha(0f);
        _acquireFlash.gameObject.SetActive(false);
        _acquireFlashCo = null;
    }

    private void SetAcquireFlashAlpha(float a)
    {
        if (_acquireFlash == null) return;
        var c = _acquireFlash.color; c.a = a; _acquireFlash.color = c;
    }

    // 드래그 입고 대상으로 강조 켜기/끄기 (창고 드롭존이 호출)
    public void SetDragHighlight(bool on)
    {
        if (_dropHighlight == on) return;
        _dropHighlight = on;
        UpdateBgVisual();
    }

    public void Refresh(InventorySlot slot, InventoryManager owner)
    {
        _slot = slot;
        _owner = owner;

        if (slot == null || slot.IsEmpty)
        {
            SetEmpty();
            UpdateBgVisual();
            return;
        }

        // 호버/선택 상태를 반영해 배경색 적용 (리프레시 중에도 강조 유지)
        UpdateBgVisual();

        if (iconBacking != null) iconBacking.SetActive(true);   // 아이템 있을 때만 backing

        var data = ItemDatabase.GetItem(slot.itemId);

        if (itemIcon != null)
        {
            itemIcon.enabled = true;
            Sprite icon = data != null ? ItemDatabase.GetIcon(data.iconKey) : null;
            itemIcon.sprite = icon;
            itemIcon.color = icon != null ? Color.white : new Color(1, 1, 1, 0.3f);
        }

        // 등급/연료 색 계산 (연료템은 등급과 별개의 독자 색)
        int gradeIndex = data != null ? (int)data.itemGrade : 0;
        bool isFuel = data != null && FuelConfig.Instance != null && FuelConfig.Instance.fuelItemId == slot.itemId;
        Color slotColor = isFuel ? GradeVisual.FuelColor : GradeVisual.GetColor(gradeIndex);

        // 칸 하단 등급선 = 딱 솔리드 크리스프 직선(오로라 느낌 X). grade_bar.png(알파 페이드) 스프라이트 제거해 플랫 색 사각으로.
        if (rarityBorder != null)
        {
            rarityBorder.color = slotColor;
            rarityBorder.sprite = null;
            rarityBorder.type = Image.Type.Simple;
            var brt = rarityBorder.rectTransform;
            brt.anchoredPosition = new Vector2(0f, brt.anchoredPosition.y);
            brt.sizeDelta = new Vector2(-GradeBarInsetX * 2f, GradeBarH);   // 좌우 인셋 = 슬롯 밖으로 안 튀게
        }

        // 오로라 효과는 그 선 "위로" 별개로 번짐(선과 완전 분리). 은은하게.
        if (gradeAurora != null)
        {
            gradeAurora.color = new Color(slotColor.r, slotColor.g, slotColor.b, slotColor.a * GradeAuroraA);
            var art = gradeAurora.rectTransform;
            art.anchoredPosition = new Vector2(0f, GradeBarH);
            art.sizeDelta = new Vector2(-GradeBarInsetX * 2f, GradeAuroraH);   // 선과 같은 폭
        }

        if (countText != null)
        {
            countText.gameObject.SetActive(slot.amount > 1);
            countText.text = slot.amount.ToString();
        }
        if (countChip != null) countChip.SetActive(slot.amount > 1);

        if (newBadge != null)
            newBadge.SetActive(slot.isNew);

        // 소모품 마커 - 소모품이고 NEW가 아닐 때만. NEW와 같은 좌상단 자리라, NEW 먼저 뜨고 사라지면 마커가 그 자리 차지(안 겹침).
        if (consumableBadge != null)
            consumableBadge.enabled = !slot.isNew && data != null && data.itemCategory == ItemCategory.TacticalConsumable;
    }

    private void SetEmpty()
    {
        if (itemIcon != null) itemIcon.enabled = false;
        if (rarityBorder != null) rarityBorder.color = emptyBorderColor;
        if (gradeAurora != null) gradeAurora.color = new Color(1, 1, 1, 0f);
        if (countText != null) countText.gameObject.SetActive(false);
        if (countChip != null) countChip.SetActive(false);
        if (iconBacking != null) iconBacking.SetActive(false);
        if (newBadge != null) newBadge.SetActive(false);
        if (consumableBadge != null) consumableBadge.enabled = false;
    }

    // 호버일 때만 배경 강조 (클릭 선택 강조는 제거함 - 호버 전용)
    private void UpdateBgVisual()
    {
        // 배경(fill)은 마우스 호버일 때만. 드래그 드롭대상(_dropHighlight)은 슬롯 fill 안 건드리고 테두리만.
        if (bgImage != null)
            bgImage.color = _isHovered ? hoverColor : normalColor;

        // 드롭 대상은 흰 프레임(_dropFrame)이 담당하므로 Outline은 호버에만. 프레임 미연결 시(폴백) 드롭도 Outline.
        bool border = _isHovered || (_dropHighlight && _dropFrame == null);
        if (_outline == null) _outline = GetComponent<UnityEngine.UI.Outline>();
        if (_outline != null)
            _outline.effectColor = border ? hoverBorderColor : normalBorderColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsEmpty) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (eventData.clickCount >= 2)
            {
                OnAnySlotDoubleClicked?.Invoke(this);
            }
            else
            {
                OnAnySlotClicked?.Invoke(this);
                if (_slot != null && _slot.isNew)
                {
                    _owner?.ClearNewFlag(_slot.slotIndex);
                    if (newBadge != null) newBadge.SetActive(false);
                    // NEW 사라지면 같은 자리에 소모품 마커 (소모품이면)
                    if (consumableBadge != null)
                    {
                        var d = ItemDatabase.GetItem(_slot.itemId);
                        consumableBadge.enabled = d != null && d.itemCategory == ItemCategory.TacticalConsumable;
                    }
                }
            }
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            OnAnySlotRightClicked?.Invoke(this);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (IsEmpty) return;   // 빈 칸은 호버 강조/툴팁 없음 (빈 칸 선택돼 보이던 버그)
        _isHovered = true;
        UpdateBgVisual();
        OnAnySlotHoverEnter?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        UpdateBgVisual();
        OnAnySlotHoverExit?.Invoke(this);
    }

    // 드래그 시작
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsEmpty) return;

        // ALT + 좌클릭 드래그 → 절반 수량만 분할해서 들기
        // (1개짜리는 ALT 눌러도 그냥 1개 드래그)
        bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (altHeld && eventData.button == PointerEventData.InputButton.Left
                    && _slot != null && _slot.amount >= 2)
        {
            int half = _slot.amount / 2;
            OnAnySlotDragBegin?.Invoke(this);
            InventoryDragHandler.Instance?.BeginDrag(this, half);
            return;
        }

        OnAnySlotDragBegin?.Invoke(this);
        InventoryDragHandler.Instance?.BeginDrag(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        InventoryDragHandler.Instance?.UpdateDragPosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        var dh = InventoryDragHandler.Instance;
        // 슬롯에 안 떨어지고(드래그 유지) 패널 밖에 놓으면 = 바닥에 버리기
        if (dh != null && dh.IsDragging && dh.DraggedSlot == this)
        {
            var ctrl = InventoryUIController.Instance;
            if (ctrl != null && !ctrl.IsPointInsideOpenPanel(eventData.position))
                ctrl.DiscardSlotByDrag(this);
        }
        dh?.EndDrag();
        OnAnySlotDragEnd?.Invoke(this);
    }

    // 다른 슬롯에서 드랍 받기
    public void OnDrop(PointerEventData eventData)
    {
        // output 슬롯 드래그 → 부모 계층의 InventoryPanelDropZone에 위임
        if (TIMEKOV.Factory.MachineSlotWidget.IsOutputDragging)
        {
            GetComponentInParent<InventoryPanelDropZone>()?.AcceptOutputDrop();
            return;
        }

        // recipe(input) 슬롯 드래그 → 부모 계층의 InventoryPanelDropZone에 위임
        if (RecipeDropSlot.IsRecipeDragging)
        {
            GetComponentInParent<InventoryPanelDropZone>()?.AcceptRecipeDrop();
            return;
        }

        // 연료 슬롯 드래그 → 부모 계층의 InventoryPanelDropZone에 위임
        if (FuelDropSlot.IsFuelDragging)
        {
            GetComponentInParent<InventoryPanelDropZone>()?.AcceptFuelDrop();
            return;
        }

        OnAnySlotDropped?.Invoke(this);
        InventoryDragHandler.Instance?.HandleDrop(this);
    }
}