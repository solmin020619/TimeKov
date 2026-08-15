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
    private InventorySlotUI _highlightedUI;   // 현재 드롭 대상 강조 칸(고정가방용 - 커서 밑 칸 추적)
    private Camera _eventCam;
    private bool _eventCamResolved;
    private bool _withinDragDriving;   // 같은 인벤 안 슬롯끼리 드래그 강조를 이 그리드가 구동 중인지

    // 필터 컴팩트 표시용 매칭 슬롯 버퍼(매 갱신 재사용, GC 회피)
    private readonly List<InventorySlot> _filterBuf = new List<InventorySlot>();

    // A안: 드래그 중엔 그리드 재배치를 미뤘다가 놓는 순간 flush(컴팩트 뷰에서 든 칸 잔상 방지).
    // 정확성 자체는 B(드롭 시 스냅샷 검증)가 이미 보장하므로 이건 순수 시각 안정화.
    private bool _refreshPending;

    // -- 획득 반짝 --------------------------------------------------------
    // 방금 들어온 아이템 칸만 잠깐 하얗게 훑는다. 설비에서 완성품을 빼거나 창고에 입고될 때
    // "어느 칸으로 갔는지"를 눈으로 잡으라고.
    // 인벤이 닫혀 있는 동안 얻은 것은 큐에 쌓였다가 열릴 때 나온다(꺼져 있으면 Update 가 안 돈다).
    //
    // ★큐는 그리드마다가 아니라 '인벤 한 벌' 당 하나다.
    //   같은 가방을 보는 그리드가 둘이기 때문이다 - 결계 밖 단독 가방, 결계 안 통합패널의 가방 구역.
    //   그리드마다 큐를 들고 있으면 밖에서 한 번 반짝인 뒤에도 안 쓰인 쪽 큐가 그대로 남아,
    //   결계 안에 들어가 통합패널을 열 때 같은 아이템이 또 반짝인다(종욱 QA 08-15).
    //   창고는 그리드가 하나뿐이라 이 증상이 안 났다.
    private static readonly List<int> _bagFlashQueue = new List<int>();
    private static readonly List<int> _storageFlashQueue = new List<int>();

    private const int FlashQueueMax = 8;       // 사냥 한참 하고 열면 화면 절반이 번쩍이므로 상한
    private const float FlashStagger = 0.06f;  // 여러 칸이면 차례로 - 동시에 터지면 그냥 노이즈다

    // 도메인 리로드를 꺼두면 static 이 살아남아, 두 번째 플레이가 지난 판의 큐를 물고 시작한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetFlashQueues()
    {
        _bagFlashQueue.Clear();
        _storageFlashQueue.Clear();
    }

    // 이 그리드가 쓸 큐. 상자(Chest)는 획득 통지를 안 듣기 때문에 null.
    private List<int> FlashQueue
    {
        get
        {
            if (_manager == null) return null;
            if (_manager.ownerType == InventoryManager.InventoryOwnerType.Player)  return _bagFlashQueue;
            if (_manager.ownerType == InventoryManager.InventoryOwnerType.Storage) return _storageFlashQueue;
            return null;
        }
    }

    // 인벤토리 매니저 바인딩
    public void Bind(InventoryManager manager)
    {
        if (_manager != null)
        {
            _manager.OnInventoryChanged -= OnDataChanged;
            SetAcquireSubscription(false);
        }

        _manager = manager;

        if (_manager != null)
        {
            _manager.OnInventoryChanged += OnDataChanged;
            SetAcquireSubscription(true);
        }

        BuildSlots();
        RefreshAll();
    }

    // 가방/창고는 들어오는 통지가 서로 다르다. 하나만 듣게 해야 "가방에 넣었는데 창고 칸도 반짝"이 안 난다
    // (같은 아이템을 양쪽이 들고 있으면 그런 오반응이 난다).
    private void SetAcquireSubscription(bool on)
    {
        if (_manager == null) return;

        if (_manager.ownerType == InventoryManager.InventoryOwnerType.Player)
        {
            if (on) InventoryManager.OnItemAddedToInventory += OnItemAdded;
            else    InventoryManager.OnItemAddedToInventory -= OnItemAdded;
        }
        else if (_manager.ownerType == InventoryManager.InventoryOwnerType.Storage)
        {
            if (on) InventoryManager.OnItemAddedToStorage += OnItemAdded;
            else    InventoryManager.OnItemAddedToStorage -= OnItemAdded;
        }
    }

    private void OnItemAdded(int itemId, int count)
    {
        // 여기서 바로 칸을 찾지 않는다. 칸 바인딩(RefreshAll)이 아직 안 돌았을 수 있어서
        // 큐에만 넣고 Update 에서 흘린다.
        var q = FlashQueue;
        if (q == null) return;

        q.Remove(itemId);        // 같은 것을 연달아 얻으면 마지막 한 번만
        q.Add(itemId);
        while (q.Count > FlashQueueMax) q.RemoveAt(0);
    }

    private void FlushAcquireFlash(List<int> queue)
    {
        int shown = 0;
        for (int q = 0; q < queue.Count; q++)
        {
            int itemId = queue[q];
            for (int i = 0; i < _slotUIs.Count; i++)
            {
                var ui = _slotUIs[i];
                if (ui == null || !ui.gameObject.activeSelf) continue;
                var s = ui.SlotData;
                if (s == null || s.IsEmpty || s.itemId != itemId) continue;

                ui.PlayAcquireFlash(shown * FlashStagger);
                shown++;
                break;   // 같은 아이템이 여러 칸에 나뉘어 있어도 첫 칸만
            }
        }
        // 같은 인벤을 보는 다른 그리드도 이걸로 소비된 것으로 친다(위 큐 주석 참고).
        queue.Clear();
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

        // 필터 시: 매칭 아이템을 slotIndex 순서로 앞 칸부터 컴팩트 배치(종욱: "1번 칸부터가 직관적").
        //   매칭 칸은 실슬롯을 그대로 물어 드래그가 올바른 실칸을 집는다(B 스냅샷 전제라 재바인딩 안전).
        //   남는 칸은 빈 표시 + 레이캐스트 통과 -> 드롭이 뒤 드롭존으로 가서 입출고/취소 처리.
        bool filtered = _currentFilter != null;
        if (filtered)
        {
            _filterBuf.Clear();
            for (int i = 0; i < slots.Count; i++)
                if (!slots[i].IsEmpty && MatchesFilter(slots[i])) _filterBuf.Add(slots[i]);
        }

        for (int i = 0; i < _slotUIs.Count; i++)
        {
            if (_slotUIs[i] == null) continue;

            if (filtered)
            {
                _slotUIs[i].gameObject.SetActive(true);
                bool showItem = i < _filterBuf.Count;
                _slotUIs[i].Refresh(showItem ? _filterBuf[i] : new InventorySlot(), _manager);
                SetCellRaycast(_slotUIs[i], showItem);
                continue;
            }

            // 전체(필터 없음): 자리 고정. 모든 칸 레이캐스트 복구(필터에서 꺼졌던 칸 되살림).
            if (i >= slots.Count)
            {
                _slotUIs[i].Refresh(null, _manager);
                _slotUIs[i].gameObject.SetActive(false);
                continue;
            }
            _slotUIs[i].gameObject.SetActive(true);
            _slotUIs[i].Refresh(slots[i], _manager);
            SetCellRaycast(_slotUIs[i], true);
        }
    }

    // 창고 기본 표시 칸 수(종욱: 처음부터 빈 칸 깔아두기). 이 수를 넘게 쓰면 그때부터 칸 증식.
    private const int MinShownSlots = 100;

    // 창고 표시 모델:
    //  - 전체(필터 없음) = "칸 위치 고정인 큰 인벤"(종욱 확정). 표시 위치 = 실제 slotIndex 그대로라 재배치 없음.
    //  - 필터 = 매칭 아이템을 slotIndex 순서로 앞 칸부터 컴팩트("1번 칸부터가 직관적"). 매칭 칸은 실슬롯을
    //    그대로 물어 드래그가 올바른 실칸을 집는다(B 스냅샷 전제라 드래그 중 재렌더돼도 재바인딩 안전).
    // 표시 칸 수 = 최소 MinShownSlots, 그 너머는 실제로 쓰인 마지막 칸까지(필터 여부와 무관하게 동일 -> 그리드 크기 안 흔들림).
    // 빈 칸/패딩은 레이캐스트 통과: 드롭이 슬롯이 아니라 뒤 드롭존(AddItem)으로 가야 스택 병합 + 창고 자동 탭 전환이 산다.
    private void RefreshDynamic()
    {
        var slots = _manager.GetSlots();

        int lastUsed = -1;
        for (int i = 0; i < slots.Count; i++)
            if (!slots[i].IsEmpty) lastUsed = i;
        int shown = Mathf.Min(slots.Count, Mathf.Max(MinShownSlots, lastUsed + 1));

        while (_slotUIs.Count < shown)
            if (CreateSlotUI(_slotUIs.Count) == null) break;

        bool filtered = _currentFilter != null;
        if (filtered)
        {
            _filterBuf.Clear();
            for (int i = 0; i < slots.Count; i++)
                if (!slots[i].IsEmpty && MatchesFilter(slots[i])) _filterBuf.Add(slots[i]);
        }

        bool revealing = _revealCo != null;
        for (int i = 0; i < _slotUIs.Count; i++)
        {
            if (_slotUIs[i] == null) continue;
            if (i < shown)
            {
                _slotUIs[i].gameObject.SetActive(true);
                if (!revealing) _slotUIs[i].transform.localScale = Vector3.one;   // 등장 애니 중이 아니면 스케일 정상화

                InventorySlot disp;
                bool showItem;
                if (filtered)
                {
                    showItem = i < _filterBuf.Count;
                    disp = showItem ? _filterBuf[i] : new InventorySlot();
                }
                else
                {
                    var slot = i < slots.Count ? slots[i] : null;
                    showItem = slot != null && !slot.IsEmpty;
                    disp = slot;
                }
                _slotUIs[i].Refresh(disp, _manager);
                SetCellRaycast(_slotUIs[i], showItem);
            }
            else
            {
                _slotUIs[i].Refresh(null, _manager);
                _slotUIs[i].gameObject.SetActive(false);
            }
        }
    }

    private bool MatchesFilter(InventorySlot slot)
    {
        var data = ItemDatabase.GetItem(slot.itemId);
        return data != null && data.itemCategory == _currentFilter.Value;
    }

    // 칸의 레이캐스트 차단 토글(빈 칸/컴팩트 패딩은 통과시켜 뒤 드롭존이 받게). CanvasGroup 없으면 생성.
    private void SetCellRaycast(InventorySlotUI ui, bool blocks)
    {
        if (ui == null) return;
        var cg = ui.GetComponent<CanvasGroup>();
        if (cg == null) cg = ui.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = blocks;
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
            var hit = FindSlotUnderPointer(pointerScreen, cam);
            if (hit == null) hit = FindSlotUIByIndex(_manager.FindTargetSlotIndexForItem(itemId));
            SetDropHighlightUI(hit);
            return;
        }

        int matchIdx = _manager.FindMatchingSlotIndex(itemId);
        if (matchIdx >= 0)
        {
            SetDropTargetHighlight(matchIdx);
            return;
        }

        // 새 종류 = 실제로 들어갈 첫 빈 칸 강조(표시가 실칸 그대로라 그 칸이 항상 화면에 있다).
        SetDropTargetHighlight(_manager.FindEmptySlotIndex());
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

    // 강조 해제 (드롭존이 영역 벗어나거나 드롭/종료 시)
    public void ClearDropHighlight()
    {
        ClearDropTargetHighlight();
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

    // (옛 "새 칸 미리보기"는 압축 표시 폐기와 함께 제거 - 실칸 표시라 들어갈 빈 칸이 항상 화면에 있다)

    private void OnDataChanged()
    {
        // A안: 드래그 중이면 재배치를 미룬다(컴팩트 뷰에서 든 칸이 딴 칸으로 튀는 잔상 방지).
        // 놓는 순간 Update 가 밀린 갱신을 flush. 정확성은 B(스냅샷 검증)가 이미 보장.
        if (InventoryDragHandler.Instance != null && InventoryDragHandler.Instance.IsDragging)
        {
            _refreshPending = true;
            return;
        }
        RefreshAll();
    }

    // 같은 인벤 안에서 슬롯끼리 드래그할 때도 커서 밑 칸 강조. (창<->창 입출고는 InventoryDropZone이 담당)
    private void Update()
    {
        // A안 flush: 드래그가 끝났는데 미뤄둔 갱신이 있으면 반영.
        if (_refreshPending && (InventoryDragHandler.Instance == null || !InventoryDragHandler.Instance.IsDragging))
        {
            _refreshPending = false;
            RefreshAll();
        }

        // 획득 반짝은 칸 바인딩이 끝난 뒤라야 올바른 칸을 찾는다. 그래서 이벤트가 아니라 여기서 흘린다.
        var flashQ = FlashQueue;
        if (flashQ != null && flashQ.Count > 0) FlushAcquireFlash(flashQ);

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

        // 획득 통지는 static 이라 안 떼면 파괴된 그리드가 계속 불린다.
        SetAcquireSubscription(false);
    }
}
