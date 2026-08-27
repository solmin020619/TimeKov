// InventoryDragHandler.cs
// Canvas 에 붙는 스크립트. 드래그 상태 보관 + 고스트 이미지 처리.
// 같은/다른 인벤토리 간 이동 처리.

using UnityEngine;
using UnityEngine.UI;

public class InventoryDragHandler : MonoBehaviour
{
    public static InventoryDragHandler Instance { get; private set; }

    [Header("드래그 고스트 이미지 (DragGhost 연결)")]
    [SerializeField] private Image ghostImage;

    // 현재 드래그 중인 시각 칸. ★죽을 수 있는 참조다 - 뷰 전환(가방<->창고 호버 전환)이
    // 그리드를 재구성하면 이 칸이 꺼지거나 딴 인벤의 딴 아이템을 물게 재바인딩된다.
    // 그래서 드래그 여부(IsDragging)를 이 참조에 걸지 않고, 소비자들은 아래 스냅샷을 쓴다.
    public InventorySlotUI DraggedSlot { get; private set; }

    // ★독립 플래그. 예전엔 IsDragging => DraggedSlot != null 이라, 드래그 도중 출발 칸이
    //   파괴/비활성되면 드래그가 '조용히' 꺼진 것으로 판정돼 고스트만 화면에 남았다.
    //   드래그의 수명은 시각 칸이 아니라 BeginDrag~EndDrag 가 정한다.
    private bool _dragging;
    public bool IsDragging => _dragging;

    /// <summary>ALT 분할 드래그 시 이동할 수량. 0 = 전체 스택.</summary>
    public int  DragAmount  { get; private set; }
    public bool IsSplitDrag => DragAmount > 0;

    // 드래그 시작 순간의 출발 슬롯 정체성 박제.
    // 압축 표시에서 시각 칸이 다른 실슬롯을 물게 재렌더되거나, 드래그 도중 그 슬롯 내용이
    // 바뀌어도(벨트 유입/기계 소비) 이 스냅샷으로 이동하고 드롭 직전 실슬롯과 대조 검증한다.
    private InventoryManager _srcManager;
    private int _srcSlotIndex = -1;
    private int _srcItemId = -1;

    // 스냅샷 접근자: 드롭/버리기/공장슬롯 등 드래그를 소비하는 모든 곳이 라이브 DraggedSlot.SlotData 대신
    // 이걸 써야 컴팩트 표시에서 드래그 중 재렌더로 시각 칸이 딴 실슬롯을 물어도 안전하다.
    public InventoryManager SrcManager => _srcManager;
    public int SrcSlotIndex => _srcSlotIndex;
    public int SrcItemId    => _srcItemId;

    /// <summary>박제한 출발 슬롯이 실슬롯과 아직 일치하는지. false 면 소비자는 이동/제거를 취소해야 한다.</summary>
    public bool SourceStillValid()
    {
        if (_srcManager == null) return false;
        var s = _srcManager.GetSlot(_srcSlotIndex);
        return s != null && !s.IsEmpty && s.itemId == _srcItemId;
    }

    private RectTransform _ghostRect;
    private RectTransform _canvasRect;

    private void Awake()
    {
        Instance = this;
        _ghostRect = ghostImage != null ? ghostImage.GetComponent<RectTransform>() : null;
        _canvasRect = GetComponent<RectTransform>();

        // 시작 시 고스트 숨김 + 레이캐스트 차단 해제(드래그 중 아래 슬롯/드롭존 감지 방해 안 하게)
        if (ghostImage != null)
        {
            ghostImage.raycastTarget = false;
            ghostImage.gameObject.SetActive(false);
        }
    }

    // 드래그 시작 (InventorySlotUI.OnBeginDrag 에서 호출)
    // amount = 0 : 전체 스택 / amount > 0 : ALT 분할 드래그
    public void BeginDrag(InventorySlotUI slot, int amount = 0)
    {
        if (slot == null || slot.IsEmpty) return;

        DraggedSlot = slot;
        DragAmount  = amount;
        _dragging   = true;

        var src = slot.SlotData;
        _srcManager   = slot.Owner;
        _srcSlotIndex = src != null ? src.slotIndex : -1;
        _srcItemId    = src != null ? src.itemId : -1;

        // 고스트 이미지에 아이콘 세팅
        if (ghostImage != null)
        {
            var data = ItemDatabase.GetItem(slot.SlotData.itemId);
            ghostImage.sprite = data != null ? ItemDatabase.GetIcon(data.iconKey) : null;
            ghostImage.color = Color.white;
            ghostImage.gameObject.SetActive(true);
        }
    }

    // 드래그 중 고스트 위치 갱신 (InventorySlotUI.OnDrag 에서 호출)
    public void UpdateDragPosition(Vector2 screenPos)
    {
        if (!IsDragging || _ghostRect == null || _canvasRect == null) return;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, screenPos, null, out localPos);

        _ghostRect.anchoredPosition = localPos;
    }

    // 드래그 종료 (성공·실패 무관하게 항상 호출)
    public void EndDrag()
    {
        _dragging   = false;
        DraggedSlot = null;
        DragAmount  = 0;
        _srcManager   = null;
        _srcSlotIndex = -1;
        _srcItemId    = -1;
        if (ghostImage != null)
            ghostImage.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_dragging) return;

        // 고스트는 출발 칸의 OnDrag 에 기대지 않고 여기서 직접 따라간다.
        // ★뷰 전환 재구성으로 출발 칸이 꺼지면 UGUI 가 그 칸의 OnDrag 를 더는 안 불러서
        //   고스트가 제자리에 박제된다 - 드래그 상태의 주인이 스스로 움직여야 견고하다.
        UpdateDragPosition(Input.mousePosition);

        // 안전망: 버튼을 뗐는데 드래그가 안 끝났으면(출발 칸이 꺼져 OnEndDrag 가 안 온 경우 등) 정리.
        // 뗀 그 프레임은 건너뛴다 - EventSystem(실행순서 -1000)이 같은 프레임에 드롭을 먼저 처리하므로,
        // 다음 프레임까지 남아 있는 드래그만 유령이다.
        if (!Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0))
            EndDrag();
    }

    // 드랍 수신 처리 (InventorySlotUI.OnDrop 에서 호출)
    public void HandleDrop(InventorySlotUI targetSlot)
    {
        if (!IsDragging || targetSlot == null) { EndDrag(); return; }

        // ★'같은 시각 칸' 취소 검사는 두지 않는다. 뷰 전환(호버 전환) 재바인딩 후에는 출발했던
        //   그 시각 칸이 딴 인벤의 실칸을 보여줄 수 있어, 거기에 놓는 건 정당한 이동이다.
        //   진짜 자기 자신(같은 인벤 + 같은 실칸 번호) 취소는 아래 스냅샷 검사가 잡는다.

        // 도착 슬롯은 드롭 순간의 시각 칸이 물고 있는 실슬롯을 그대로 사용(누른 칸이 곧 대상).
        var toSlot    = targetSlot.SlotData;
        var toManager = targetSlot.Owner;
        if (_srcManager == null || toManager == null || toSlot == null) { EndDrag(); return; }

        // 출발 슬롯 검증: 드래그 시작 때 박제한 정체성과 실슬롯이 아직 같은지 확인.
        // 드래그 도중 벨트 유입/기계 소비로 그 슬롯이 비었거나 다른 아이템으로 바뀌었으면 취소해
        // 엉뚱한 아이템이 이동하는 걸 막는다(압축 표시 재바인딩 + 밑장 바뀜 동시 차단).
        if (!SourceStillValid()) { EndDrag(); return; }

        // 다른 시각 칸이 같은 실슬롯을 물고 있던 경우(자기 자신으로의 이동)는 무시
        if (_srcManager == toManager && _srcSlotIndex == toSlot.slotIndex) { EndDrag(); return; }

        if (IsSplitDrag)
        {
            // ALT 분할 드래그: 지정 수량만 이동
            _srcManager.MoveAmountToSlot(_srcSlotIndex, DragAmount, toManager, toSlot.slotIndex);
        }
        else if (_srcManager == toManager)
        {
            _srcManager.SwapSlots(_srcSlotIndex, toSlot.slotIndex);
        }
        else
        {
            _srcManager.MoveSlotTo(_srcSlotIndex, toManager, toSlot.slotIndex);
        }

        EndDrag();
    }
}
