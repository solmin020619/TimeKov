// =====================================================================
// BuildDemolisher.cs
// 해제(Demolish) 모드의 실행 전담 클래스.
// 모드 on/off 와 입력 토글은 BuildManager 가 소유(모드 책임)하고,
// 실제 해제 동작(레이캐스트/호버 하이라이트/건물.레일 제거)은 여기가 담당.
// 설정값(마스크/오디오)은 BuildManager 인스펙터에 남겨두고 owner 통해 읽는다
// (RailBuildManager 가 owner.mainCam 등을 읽는 것과 동일 패턴 -> 인스펙터 재연결 불필요).
// =====================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TIMEKOV.Factory;

public class BuildDemolisher
{
    private readonly BuildManager owner;
    private readonly GridOccupancy occupancy;

    private PlacedBuilding currentHoveredBuilding;

    // 레일 해제용 hover 상태
    private RailPiece hoveredRailPiece;
    private BeltSegment hoveredBeltSegment;
    private bool hasHoveredRail = false;

    // Shift 드래그 연속 해제 상태
    private bool isDragDemolishing = false;
    private readonly HashSet<PlacedBuilding> dragDemolishedBuildings = new HashSet<PlacedBuilding>();
    private readonly HashSet<Vector2Int> dragDemolishedRailCells = new HashSet<Vector2Int>();

    public BuildDemolisher(BuildManager owner, GridOccupancy occupancy)
    {
        this.owner = owner;
        this.occupancy = occupancy;
    }

    // BuildManager.Update 에서 해제 모드일 때 매 프레임 호출
    public void Tick()
    {
        if (owner.mainCam == null)
            return;

        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetMouseButtonDown(0) && shiftHeld && !IsPointerOverUI())
        {
            isDragDemolishing = true;
            dragDemolishedBuildings.Clear();
            dragDemolishedRailCells.Clear();
        }
        if (Input.GetMouseButtonUp(0))
        {
            isDragDemolishing = false;
            dragDemolishedBuildings.Clear();
            dragDemolishedRailCells.Clear();
        }

        Ray ray = owner.mainCam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit bHit, owner.rayDistance, owner.placedBuildingMask))
        {
            PlacedBuilding building = bHit.collider.GetComponentInParent<PlacedBuilding>();

            if (building != null)
            {
                SetHoveredBuilding(building);
                ClearHoveredRail();

                bool clickDown = Input.GetMouseButtonDown(0) && !IsPointerOverUI();
                bool dragHit = isDragDemolishing && !dragDemolishedBuildings.Contains(building);

                if (clickDown || dragHit)
                {
                    DemolishBuilding(building);
                    if (isDragDemolishing) dragDemolishedBuildings.Add(building);
                }
                return;
            }
        }

        if (owner.RailManager != null && owner.railMask.value != 0 &&
            Physics.Raycast(ray, out RaycastHit rHit, owner.rayDistance, owner.railMask))
        {
            RailPiece rail = rHit.collider.GetComponentInParent<RailPiece>();
            if (rail != null)
            {
                ClearHoveredBuilding();
                SetHoveredRail(rail);

                bool clickDown = Input.GetMouseButtonDown(0) && !IsPointerOverUI();
                bool dragHit = isDragDemolishing && !dragDemolishedRailCells.Contains(rail.cell);

                if (clickDown || dragHit)
                {
                    Vector2Int target = rail.cell;
                    if (isDragDemolishing) dragDemolishedRailCells.Add(target);
                    ClearHoveredRail();
                    DemolishRail(target);
                }
                return;
            }
        }

        ClearHoveredBuilding();
        ClearHoveredRail();
    }

    // 해제 모드 종료/서브모드 전환 시 BuildManager 가 호출 (호버.드래그 상태 정리)
    public void Cancel()
    {
        ClearHoveredBuilding();
        ClearHoveredRail();
        isDragDemolishing = false;
        dragDemolishedBuildings.Clear();
        dragDemolishedRailCells.Clear();
    }

    private void DemolishBuilding(PlacedBuilding building)
    {
        if (building == null) return;

        // 설비 내부 아이템을 창고로 이동 (파괴 전에 처리)
        ReturnItemsToStorage(building.gameObject);

        owner.RailManager?.RemoveRailsConnectedToBuilding(building);

        occupancy.Free(building.occupiedCells);
        if (currentHoveredBuilding == building) currentHoveredBuilding = null;

        PlayDemolishSound();
        Object.Destroy(building.gameObject);

        // 철거된 설비를 가리키던 벨트 연결 정리 후 재감지
        BeltSegment.ReconnectAll();

        if (owner.IsRailSubMode)
            owner.RailManager?.RefreshPortIndicators();
    }

    /// <summary>
    /// 설비의 InputBuffer·OutputBuffer 아이템을 창고(StorageInstance)로 이동.
    /// 창고가 없거나 설비가 MachineBase를 가지지 않으면 조용히 스킵.
    /// </summary>
    private void ReturnItemsToStorage(GameObject facilityObj)
    {
        var storage = InventoryManager.StorageInstance;
        if (storage == null) return;

        var machine = facilityObj.GetComponent<MachineBase>();
        if (machine == null) return;

        // Dictionary를 직접 순회하면 컬렉션 변경 오류가 날 수 있으므로 복사본으로 처리
        var inputItems  = new Dictionary<int, int>(machine.InputBuffer.Stock);
        var outputItems = new Dictionary<int, int>(machine.OutputBuffer.Stock);

        foreach (var kv in inputItems)
            if (kv.Value > 0) storage.AddItem(kv.Key, kv.Value);

        foreach (var kv in outputItems)
            if (kv.Value > 0) storage.AddItem(kv.Key, kv.Value);

        // 연료 아이템 회수 (FuelTimeRemaining → 아이템 개수로 역산)
        var cfg = FuelConfig.Instance;
        if (cfg != null && machine.HasFuel)
        {
            int fuelCount = machine.TakeFuel();
            if (fuelCount > 0)
                storage.AddItem(cfg.fuelItemId, fuelCount);
        }

        // 토스트는 StorageInflowNotice(창고 자동 유입 공용 알림)가 담당 - 철거/레일 회수/자동입고 문구 통일.
    }

    private void DemolishRail(Vector2Int cell)
    {
        if (owner.RailManager == null) return;
        if (!owner.RailManager.RemoveRailAt(cell)) return;

        PlayDemolishSound();

        // 잘려나간 체인 양쪽에 이전 출발/도착 정보가 남지 않도록 재감지
        BeltSegment.ReconnectAll();
    }

    private void SetHoveredBuilding(PlacedBuilding building)
    {
        if (currentHoveredBuilding == building)
            return;

        ClearHoveredBuilding();

        currentHoveredBuilding = building;
        currentHoveredBuilding.SetDemolishHighlight(true);
    }

    private void ClearHoveredBuilding()
    {
        if (currentHoveredBuilding != null)
        {
            currentHoveredBuilding.SetDemolishHighlight(false);
            currentHoveredBuilding = null;
        }
    }

    private void SetHoveredRail(RailPiece rail)
    {
        if (rail == null) return;
        if (hasHoveredRail && hoveredRailPiece == rail) return;

        ClearHoveredRail();

        hoveredRailPiece = rail;
        hasHoveredRail = true;

        // 네모 칸 오버레이 대신 벨트 자체 텍스처를 강한 빨강으로 칠해 철거 대상을 표시.
        hoveredBeltSegment = rail.GetComponentInChildren<BeltSegment>(true);
        hoveredBeltSegment?.SetDemolishHighlight(true);
    }

    private void ClearHoveredRail()
    {
        if (!hasHoveredRail) return;

        hoveredBeltSegment?.SetDemolishHighlight(false);
        hoveredBeltSegment = null;
        hoveredRailPiece = null;
        hasHoveredRail = false;
    }

    private void PlayDemolishSound() => GameSfx.Play(SfxId.Demolish);   // 볼륨은 GameSfxConfig 에서 관리

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
