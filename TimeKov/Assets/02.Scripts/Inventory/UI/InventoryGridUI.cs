// InventoryGridUI.cs
// 슬롯 그리드. InventoryManager 데이터를 슬롯 UI에 바인딩 + 카테고리 필터.
// dynamicSlots: 창고용. 아이템 수만큼만 칸을 동적으로 표시(빈칸 없이 압축). 가방/상자는 false(고정 칸).
// PlayReveal: 첫 칸부터 촤라락 순차 등장(엔필식). Open 시 컨트롤러가 호출.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryGridUI : MonoBehaviour
{
    [Header("슬롯 설정")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotGrid;
    [Tooltip("드롭 대상 강조 흰 프레임 스프라이트(9-slice). 빌더가 자동 연결. 없으면 Outline 폴백.")]
    [SerializeField] private Sprite dropFrameSprite;

    [Header("창고용 동적 칸 (가방/상자는 끔)")]
    [Tooltip("켜면 아이템 수만큼만 칸 표시(빈칸 없이 앞으로 압축). 용량 무제한 느낌. 창고 전용.")]
    [SerializeField] private bool dynamicSlots = false;

    [Header("순차 등장 애니")]
    [Tooltip("칸 사이 등장 간격(초). 작을수록 촤라락 빠르게.")]
    [SerializeField] private float revealStep = 0.022f;
    [Tooltip("칸 하나 팝업 시간(초).")]
    [SerializeField] private float revealPop = 0.11f;

    private InventoryManager _manager;
    private List<InventorySlotUI> _slotUIs = new List<InventorySlotUI>();
    private ItemCategory? _currentFilter = null;
    private Coroutine _revealCo;
    private int _highlightedSlotIndex = -1;   // 현재 드롭 대상 강조 슬롯(동적창고용 인덱스 추적)
    private int _dynamicVisibleCount = 0;     // 동적모드에서 현재 보이는 아이템 칸 수
    private InventorySlotUI _appendPreviewSlot;   // 새 칸 미리보기(드래그 중 빈 칸)
    private InventorySlotUI _highlightedUI;   // 현재 드롭 대상 강조 칸(고정가방용 - 커서 밑 칸 추적)
    private Camera _eventCam;
    private bool _eventCamResolved;
    private bool _withinDragDriving;   // 같은 인벤 안 슬롯끼리 드래그 강조를 이 그리드가 구동 중인지

    // 인벤토리 매니저 바인딩
    public void Bind(InventoryManager manager)
    {
        if (_manager != null)
            _manager.OnInventoryChanged -= OnDataChanged;

        _manager = manager;

        if (_manager != null)
            _manager.OnInventoryChanged += OnDataChanged;

        BuildSlots();
        RefreshAll();
    }

    // 카테고리 필터 (null 이면 전체)
    public void SetFilter(ItemCategory? category)
    {
        _currentFilter = category;
        RefreshAll();

        // 카테고리 바꿀 때만 첫 칸부터 촤라락 등장. (처음 열 때는 한번에 - Open에선 호출 안 함)
        // 닫는 중(IsOpen false)엔 필터 리셋이 와도 스킵.
        var uic = InventoryUIController.Instance;
        if (uic != null && uic.IsOpen) PlayReveal();
    }

    // 슬롯 UI 생성
    private void BuildSlots()
    {
        foreach (var ui in _slotUIs)
            if (ui != null) Destroy(ui.gameObject);
        _slotUIs.Clear();

        if (_manager == null || slotPrefab == null || slotGrid == null)
        {
            Debug.LogWarning($"[InventoryGridUI:{name}] BuildSlots 중단 - " +
                             $"manager={(_manager == null ? "NULL" : _manager.ownerType.ToString())} " +
                             $"slotPrefab={slotPrefab != null} slotGrid={slotGrid != null}");
            return;
        }

        // 동적 모드는 미리 안 만들고 RefreshAll에서 필요한 만큼만 생성(무제한 창고 대비 + 빈칸 미생성)
        if (dynamicSlots) return;

        int slotCount = _manager.GetMaxSlots();
        for (int i = 0; i < slotCount; i++)
            CreateSlotUI(i);
    }

    private InventorySlotUI CreateSlotUI(int i)
    {
        var obj = Instantiate(slotPrefab, slotGrid);
        obj.name = "Slot_" + i;
        var slotUI = obj.GetComponent<InventorySlotUI>();
        if (slotUI == null)
        {
            Debug.LogError("[InventoryGridUI] slotPrefab에 InventorySlotUI 없음: " + obj.name);
            return null;
        }
        slotUI.InitDropFrame(dropFrameSprite);   // 흰 강조 프레임 주입(스프라이트 없으면 무시)
        _slotUIs.Add(slotUI);
        return slotUI;
    }

    // 데이터 변경 시 전체 슬롯 갱신
    public void RefreshAll()
    {
        if (_manager == null) return;

        if (dynamicSlots) { RefreshDynamic(); return; }

        var slots = _manager.GetSlots();

        for (int i = 0; i < _slotUIs.Count; i++)
        {
            if (_slotUIs[i] == null) continue;

            if (i >= slots.Count)
            {
                _slotUIs[i].Refresh(null, _manager);
                _slotUIs[i].gameObject.SetActive(false);
                continue;
            }

            var slot = slots[i];
            _slotUIs[i].gameObject.SetActive(true);

            // 카테고리 필터
            if (!slot.IsEmpty && _currentFilter != null)
            {
                var data = ItemDatabase.GetItem(slot.itemId);
                bool match = data != null && data.itemCategory == _currentFilter.Value;

                if (!match)
                {
                    _slotUIs[i].Refresh(new InventorySlot(), _manager);
                    continue;
                }
            }

            _slotUIs[i].Refresh(slot, _manager);
        }
    }

    // 창고: 비어있지 않고 필터에 맞는 슬롯만 앞으로 압축 표시(빈칸 없음). 칸이 모자라면 생성.
    private void RefreshDynamic()
    {
        HideAppendPreview();   // 데이터 변경 시 미리보기 정리(드롭존이 다음 프레임 재표시)
        var slots = _manager.GetSlots();
        var visible = new List<InventorySlot>();
        foreach (var slot in slots)
        {
            if (slot.IsEmpty) continue;
            if (_currentFilter != null)
            {
                var data = ItemDatabase.GetItem(slot.itemId);
                if (data == null || data.itemCategory != _currentFilter.Value) continue;
            }
            visible.Add(slot);
        }
        _dynamicVisibleCount = visible.Count;

        // 창고가 비면 빈 칸 1개는 보여준다(완전 빈 패널 = 버그처럼 보임 방지). placeholder = 첫 빈 슬롯(유효 slotIndex).
        InventorySlot placeholder = null;
        if (visible.Count == 0)
            foreach (var s in slots) { if (s.IsEmpty) { placeholder = s; break; } }
        int shown = (placeholder != null) ? 1 : visible.Count;

        while (_slotUIs.Count < shown)
            if (CreateSlotUI(_slotUIs.Count) == null) break;

        bool revealing = _revealCo != null;
        for (int i = 0; i < _slotUIs.Count; i++)
        {
            if (_slotUIs[i] == null) continue;
            if (i < visible.Count)
            {
                _slotUIs[i].gameObject.SetActive(true);
                if (!revealing) _slotUIs[i].transform.localScale = Vector3.one;   // 등장 애니 중이 아니면 스케일 정상화
                _slotUIs[i].Refresh(visible[i], _manager);
            }
            else if (i < shown)
            {
                _slotUIs[i].gameObject.SetActive(true);
                if (!revealing) _slotUIs[i].transform.localScale = Vector3.one;
                _slotUIs[i].Refresh(placeholder, _manager);   // 빈 칸 placeholder(첫 빈 슬롯)
            }
            else
            {
                _slotUIs[i].Refresh(null, _manager);
                _slotUIs[i].gameObject.SetActive(false);
            }
        }
    }

    // 첫 칸부터 순차 등장(스케일 팝). 단일 코루틴.
    public void PlayReveal()
    {
        if (!isActiveAndEnabled) return;
        if (_revealCo != null) StopCoroutine(_revealCo);
        _revealCo = StartCoroutine(RevealRoutine());
    }

    private IEnumerator RevealRoutine()
    {
        var active = new List<Transform>();
        foreach (var ui in _slotUIs)
            if (ui != null && ui.gameObject.activeSelf) active.Add(ui.transform);

        int n = active.Count;
        if (n == 0) { _revealCo = null; yield break; }

        foreach (var t in active) if (t != null) t.localScale = Vector3.zero;

        float pop = Mathf.Max(0.01f, revealPop);
        float step = Mathf.Max(0f, revealStep);
        float total = step * n + pop;
        float e = 0f;
        while (e < total)
        {
            e += Time.unscaledDeltaTime;
            for (int i = 0; i < n; i++)
            {
                if (active[i] == null) continue;
                float local = e - i * step;
                float k = Mathf.Clamp01(local / pop);
                float s = 1f - (1f - k) * (1f - k);   // ease-out
                active[i].localScale = Vector3.one * s;
            }
            yield return null;
        }
        foreach (var t in active) if (t != null) t.localScale = Vector3.one;
        _revealCo = null;
    }

    // 드롭존이 매 프레임 호출: 이 itemId/커서 위치에 따라 실제로 들어갈 칸을 강조.
    //  - 고정가방: 커서 밑 칸 강조 = 슬롯단위 드롭이라 드롭하면 바로 그 칸으로 들어간다(강조==실제 드롭위치).
    //              칸 밖 빈틈/영역 빈공간이면 영역 드롭(AddItem)이 갈 칸 = 매칭 스택 우선, 없으면 첫 빈칸.
    //  - 동적창고: 매칭(겹칠) 칸 있으면 그 칸, 없으면 새로 생길 칸을 끝에 미리보기.
    public void HighlightDropTarget(int itemId, Vector2 pointerScreen, Camera cam)
    {
        if (_manager == null) { ClearDropHighlight(); return; }

        if (!dynamicSlots)
        {
            // 고정가방: 커서가 올라간 칸을 그대로 강조.
            // 칸 밖 빈틈이면 영역 드롭(AddItem)이 갈 칸 = 매칭 스택 우선, 없으면 첫 빈칸.
            HideAppendPreview();
            var hit = FindSlotUnderPointer(pointerScreen, cam);
            if (hit == null) hit = FindSlotUIByIndex(_manager.FindTargetSlotIndexForItem(itemId));
            SetDropHighlightUI(hit);
            return;
        }

        int matchIdx = _manager.FindMatchingSlotIndex(itemId);
        if (matchIdx >= 0)
        {
            HideAppendPreview();
            SetDropTargetHighlight(matchIdx);
            return;
        }

        // 새 종류 = 새로 생길 칸을 미리 보여줌
        SetDropTargetHighlight(-1);   // 매칭 강조 해제
        // 아이템 있을 때만 끝에 미리보기 칸 추가. 빈 창고는 이미 placeholder 빈 칸이 있으니 그걸 강조.
        if (_dynamicVisibleCount > 0)
            ShowAppendPreview();
        else
        {
            HideAppendPreview();
            SetDropTargetHighlight(_manager.FindEmptySlotIndex());
        }
    }

    // 화면좌표 밑에 있는 활성 슬롯 UI (없으면 null)
    private InventorySlotUI FindSlotUnderPointer(Vector2 screenPos, Camera cam)
    {
        foreach (var ui in _slotUIs)
        {
            if (ui == null || !ui.gameObject.activeSelf) continue;
            var rt = ui.transform as RectTransform;
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam))
                return ui;
        }
        return null;
    }

    // slotIndex 에 해당하는 활성 슬롯 UI (없으면 null)
    private InventorySlotUI FindSlotUIByIndex(int slotIndex)
    {
        if (slotIndex < 0) return null;
        foreach (var ui in _slotUIs)
        {
            if (ui == null || !ui.gameObject.activeSelf || ui.SlotData == null) continue;
            if (ui.SlotData.slotIndex == slotIndex) return ui;
        }
        return null;
    }

    // 고정가방: 커서 밑 한 칸만 테두리 강조(다른 칸 안 건드림). 같은 칸이면 무시.
    private void SetDropHighlightUI(InventorySlotUI ui)
    {
        if (_highlightedUI == ui) return;
        if (_highlightedUI != null) _highlightedUI.SetDragHighlight(false);
        _highlightedUI = ui;
        if (_highlightedUI != null) _highlightedUI.SetDragHighlight(true);
    }

    // 강조 + 미리보기 모두 해제 (드롭존이 영역 벗어나거나 드롭/종료 시)
    public void ClearDropHighlight()
    {
        ClearDropTargetHighlight();
        HideAppendPreview();
    }

    // 특정 슬롯 강조 (활성 + SlotData 있는 칸만, 빈 칸도 가능). storageSlotIndex<0 이면 해제만.
    public void SetDropTargetHighlight(int storageSlotIndex)
    {
        if (_highlightedSlotIndex == storageSlotIndex) return;
        ClearDropTargetHighlight();
        if (storageSlotIndex < 0) return;
        foreach (var ui in _slotUIs)
        {
            if (ui == null || !ui.gameObject.activeSelf || ui.SlotData == null) continue;
            if (ui.SlotData.slotIndex == storageSlotIndex)
            {
                ui.SetDragHighlight(true);
                _highlightedSlotIndex = storageSlotIndex;
                break;
            }
        }
    }

    public void ClearDropTargetHighlight()
    {
        foreach (var ui in _slotUIs)
            if (ui != null) ui.SetDragHighlight(false);
        _highlightedSlotIndex = -1;
        _highlightedUI = null;
    }

    // 동적창고: 마지막 아이템 다음 칸에 빈 미리보기 칸 표시(드롭하면 여기 생긴다는 안내)
    private void ShowAppendPreview()
    {
        if (_appendPreviewSlot != null) return;   // 이미 표시 중
        int idx = _dynamicVisibleCount;
        while (_slotUIs.Count <= idx)
            if (CreateSlotUI(_slotUIs.Count) == null) return;
        var p = _slotUIs[idx];
        if (p == null) return;
        p.gameObject.SetActive(true);
        p.transform.localScale = Vector3.one;
        p.Refresh(new InventorySlot(), _manager);   // 빈 칸
        // 미리보기는 가짜 칸이라 드롭을 가로채면 안 됨 -> 레이캐스트 통과시켜 드롭존이 입고 처리
        var cg = p.GetComponent<CanvasGroup>(); if (cg == null) cg = p.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        p.SetDragHighlight(true);
        _appendPreviewSlot = p;
    }

    private void HideAppendPreview()
    {
        if (_appendPreviewSlot == null) return;
        var cg = _appendPreviewSlot.GetComponent<CanvasGroup>(); if (cg != null) cg.blocksRaycasts = true;   // 실제 칸으로 재사용 시 다시 상호작용
        _appendPreviewSlot.SetDragHighlight(false);
        _appendPreviewSlot.gameObject.SetActive(false);
        _appendPreviewSlot = null;
    }

    private void OnDataChanged() => RefreshAll();

    // 같은 인벤 안에서 슬롯끼리 드래그할 때도 커서 밑 칸 강조. (창<->창 입출고는 InventoryDropZone이 담당)
    private void Update()
    {
        var dh = InventoryDragHandler.Instance;
        bool within = dh != null && dh.IsDragging && dh.DraggedSlot != null
                      && !dh.DraggedSlot.IsEmpty && _manager != null && dh.DraggedSlot.Owner == _manager;
        if (!within)
        {
            if (_withinDragDriving) { ClearDropHighlight(); _withinDragDriving = false; }
            return;
        }

        _withinDragDriving = true;
        var hit = FindSlotUnderPointer(Input.mousePosition, ResolveEventCam());
        if (hit == dh.DraggedSlot) hit = null;   // 자기 자신엔 강조 안 함(드롭=취소)
        SetDropHighlightUI(hit);   // null이면 해제
    }

    private Camera ResolveEventCam()
    {
        if (_eventCamResolved) return _eventCam;
        var canvas = GetComponentInParent<Canvas>();
        _eventCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        _eventCamResolved = true;
        return _eventCam;
    }

    private void OnDisable()
    {
        if (_revealCo != null) { StopCoroutine(_revealCo); _revealCo = null; }
        ClearDropHighlight();
        _withinDragDriving = false;
    }

    private void OnDestroy()
    {
        if (_manager != null)
            _manager.OnInventoryChanged -= OnDataChanged;
    }
}
