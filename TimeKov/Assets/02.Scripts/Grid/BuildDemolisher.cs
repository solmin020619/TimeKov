// =====================================================================
// BuildDemolisher.cs
// 해제(Demolish) 모드의 실행 전담 클래스.
// 모드 on/off 와 입력 토글은 BuildManager 가 소유(모드 책임)하고,
// 실제 해제 동작(레이캐스트/호버 하이라이트/건물.레일 제거/레일 오버레이)은 여기가 담당.
// 설정값(마스크/오디오/오버레이)은 BuildManager 인스펙터에 남겨두고 owner 통해 읽는다
// (RailBuildManager 가 owner.mainCam 등을 읽는 것과 동일 패턴 -> 인스펙터 재연결 불필요).
// =====================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuildDemolisher
{
    private readonly BuildManager owner;
    private readonly GridOccupancy occupancy;

    private PlacedBuilding currentHoveredBuilding;

    // 레일 해제용 hover 상태
    private RailPiece hoveredRailPiece;
    private bool hasHoveredRail = false;

    // Shift 드래그 연속 해제 상태
    private bool isDragDemolishing = false;
    private readonly HashSet<PlacedBuilding> dragDemolishedBuildings = new HashSet<PlacedBuilding>();
    private readonly HashSet<Vector2Int> dragDemolishedRailCells = new HashSet<Vector2Int>();

    private GameObject _railOverlayGO;

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

        owner.RailManager?.RemoveRailsConnectedToBuilding(building);

        occupancy.Free(building.occupiedCells);
        if (currentHoveredBuilding == building) currentHoveredBuilding = null;

        PlayDemolishSound();
        Object.Destroy(building.gameObject);

        if (owner.IsRailSubMode)
            owner.RailManager?.RefreshPortIndicators();
    }

    private void DemolishRail(Vector2Int cell)
    {
        if (owner.RailManager == null) return;
        if (!owner.RailManager.RemoveRailAt(cell)) return;

        PlayDemolishSound();
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

        ShowRailOverlay(rail);
    }

    private void ClearHoveredRail()
    {
        if (!hasHoveredRail) return;

        HideRailOverlay();
        hoveredRailPiece = null;
        hasHoveredRail = false;
    }

    private void ShowRailOverlay(RailPiece rail)
    {
        if (_railOverlayGO == null)
            _railOverlayGO = CreateRailOverlay();

        float cs = owner.RailManager != null ? owner.RailManager.CellSizeRail : owner.cellSize;
        float y = owner.RailManager != null ? owner.RailManager.FixedYRail : owner.fixedY;
        Vector3 center = rail.transform.position;
        center.y = y + 0.02f;

        _railOverlayGO.transform.position = center;
        _railOverlayGO.transform.rotation = Quaternion.identity;
        _railOverlayGO.transform.localScale = new Vector3(cs * 0.95f, Mathf.Max(0.01f, owner.railDemolishOverlayHeight), cs * 0.95f);
        _railOverlayGO.SetActive(true);
    }

    private void HideRailOverlay()
    {
        if (_railOverlayGO != null)
            _railOverlayGO.SetActive(false);
    }

    private GameObject CreateRailOverlay()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "_RailDemolishOverlay";
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var rend = go.GetComponent<MeshRenderer>();
        if (rend != null)
        {
            if (owner.railDemolishOverlayMaterial != null)
            {
                rend.sharedMaterial = owner.railDemolishOverlayMaterial;
            }
            else
            {
                Shader shader = Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    mat.color = new Color(1f, 0.2f, 0.2f, 0.5f);
                    rend.sharedMaterial = mat;
                }
            }
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }
        go.SetActive(false);
        return go;
    }

    private void PlayDemolishSound()
    {
        if (owner.audioSource == null || owner.demolishClip == null) return;
        owner.audioSource.PlayOneShot(owner.demolishClip, owner.demolishVolume);
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
