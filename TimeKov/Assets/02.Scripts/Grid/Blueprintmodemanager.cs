using System.Collections.Generic;
using UnityEngine;


public class BlueprintModeManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode rotateKey = KeyCode.R;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;

    [Header("Selection Box Visual")]
    [SerializeField] private LineRenderer selectionBoxRenderer;
    [SerializeField] private Color selectionColor = new Color(0.4f, 0.85f, 1f, 0.9f);
    [SerializeField] private float selectionYOffset = 0.05f;

    [Header("Ghost Visual")]
    [Tooltip("시설 유령 프리뷰에 적용할 머티리얼. BuildManager.hologramMaterial 재사용 가능.")]
    [SerializeField] private Material ghostFacilityMaterial;

    [Tooltip("레일 유령 표시용 머티리얼(지면에 작은 사각형으로 표시).")]
    [SerializeField] private Material ghostRailMaterial;

    [SerializeField] private Color ghostValidTint = new Color(0.4f, 1f, 0.5f, 1f);
    [SerializeField] private Color ghostInvalidTint = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Root Parent for Ghosts")]
    [SerializeField] private Transform ghostParent;

    private BuildManager owner;
    private bool isActive;

    private enum State { Idle, Selecting, Pasting }
    private State state = State.Idle;

    private bool isMouseDragging;
    private Vector2Int dragStartCell;
    private Vector2Int dragCurrentCell;

    private struct FacilityEntry
    {
        public int facilityId;
        public Vector2Int size;         
        public int ownRotationY;         
        public Vector2 centerOffset;    
    }

    private struct RailEntry
    {
        public Vector2 cellCenterOffset; 
        public bool up, down, left, right;
    }

    private readonly List<FacilityEntry> facilities = new();
    private readonly List<RailEntry> rails = new();
    private Vector2 anchorCellCoord;    

    private int blueprintRotationY = 0;  
    private readonly List<GameObject> facilityGhosts = new();
    private readonly List<GameObject> railGhosts = new();


    public bool IsActive => isActive;

    public void Activate(BuildManager mgr)
    {
        owner = mgr;
        isActive = true;
        state = State.Idle;
        blueprintRotationY = 0;
        facilities.Clear();
        rails.Clear();
        HideSelectionBox();
        ClearGhosts();
    }

    public void Deactivate()
    {
        isActive = false;
        state = State.Idle;
        isMouseDragging = false;
        facilities.Clear();
        rails.Clear();
        HideSelectionBox();
        ClearGhosts();
        owner = null;
    }

    public void Tick()
    {
        if (!isActive || owner == null) return;

        if (Input.GetKeyDown(cancelKey))
        {
            owner.SetSubMode(BuildManager.BuildSubMode.Facility);
            return;
        }

        switch (state)
        {
            case State.Idle:
            case State.Selecting:
                HandleSelectionPhase();
                break;
            case State.Pasting:
                HandlePastingPhase();
                break;
        }
    }


    private void HandleSelectionPhase()
    {
        if (!TryGetCursorCell(out Vector2Int cursorCell))
        {
            if (!isMouseDragging) HideSelectionBox();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            isMouseDragging = true;
            dragStartCell = cursorCell;
            dragCurrentCell = cursorCell;
            state = State.Selecting;
            UpdateSelectionBoxVisual();
            return;
        }

        if (isMouseDragging)
        {
            dragCurrentCell = cursorCell;
            UpdateSelectionBoxVisual();

            if (Input.GetMouseButtonUp(0))
            {
                isMouseDragging = false;
                HideSelectionBox();
                FinalizeSelection();
            }
        }
    }

    private void FinalizeSelection()
    {
        Vector2Int min = new Vector2Int(
            Mathf.Min(dragStartCell.x, dragCurrentCell.x),
            Mathf.Min(dragStartCell.y, dragCurrentCell.y));
        Vector2Int max = new Vector2Int(
            Mathf.Max(dragStartCell.x, dragCurrentCell.x),
            Mathf.Max(dragStartCell.y, dragCurrentCell.y));

        CaptureBlueprint(min, max);

        if (facilities.Count == 0 && rails.Count == 0)
        {
            state = State.Idle;
            return;
        }

        state = State.Pasting;
        blueprintRotationY = 0;
        BuildGhosts();
    }

    private void CaptureBlueprint(Vector2Int cellMin, Vector2Int cellMax)
    {
        facilities.Clear();
        rails.Clear();

        int bbMinX = int.MaxValue, bbMinZ = int.MaxValue;
        int bbMaxX = int.MinValue, bbMaxZ = int.MinValue;

        PlacedBuilding[] allBuildings = FindObjectsOfType<PlacedBuilding>();
        var captured = new List<(PlacedBuilding pb, Vector2Int size)>();

        foreach (var pb in allBuildings)
        {
            if (pb == null || pb.occupiedCells == null || pb.occupiedCells.Count == 0) continue;

            bool anyInside = false;
            foreach (var c in pb.occupiedCells)
            {
                if (c.x >= cellMin.x && c.x <= cellMax.x && c.y >= cellMin.y && c.y <= cellMax.y)
                {
                    anyInside = true;
                    break;
                }
            }
            if (!anyInside) continue;

            int pMinX = int.MaxValue, pMinZ = int.MaxValue, pMaxX = int.MinValue, pMaxZ = int.MinValue;
            foreach (var c in pb.occupiedCells)
            {
                if (c.x < pMinX) pMinX = c.x;
                if (c.x > pMaxX) pMaxX = c.x;
                if (c.y < pMinZ) pMinZ = c.y;
                if (c.y > pMaxZ) pMaxZ = c.y;
            }

            Vector2Int sizeActual = new Vector2Int(pMaxX - pMinX + 1, pMaxZ - pMinZ + 1);
            captured.Add((pb, sizeActual));

            if (pMinX < bbMinX) bbMinX = pMinX;
            if (pMinZ < bbMinZ) bbMinZ = pMinZ;
            if (pMaxX + 1 > bbMaxX) bbMaxX = pMaxX + 1;
            if (pMaxZ + 1 > bbMaxZ) bbMaxZ = pMaxZ + 1;
        }

        var capturedRails = new List<RailPiece>();
        if (owner.RailManager != null)
        {
            foreach (var kv in owner.RailManager.RailMap)
            {
                Vector2Int c = kv.Key;
                if (c.x < cellMin.x || c.x > cellMax.x || c.y < cellMin.y || c.y > cellMax.y) continue;
                if (kv.Value == null) continue;

                capturedRails.Add(kv.Value);

                if (c.x < bbMinX) bbMinX = c.x;
                if (c.y < bbMinZ) bbMinZ = c.y;
                if (c.x + 1 > bbMaxX) bbMaxX = c.x + 1;
                if (c.y + 1 > bbMaxZ) bbMaxZ = c.y + 1;
            }
        }

        if (captured.Count == 0 && capturedRails.Count == 0) return;

        anchorCellCoord = new Vector2((bbMinX + bbMaxX) * 0.5f, (bbMinZ + bbMaxZ) * 0.5f);

        foreach (var (pb, size) in captured)
        {
            int yRot = NormalizeRotation(Mathf.RoundToInt(pb.transform.rotation.eulerAngles.y));
            Vector2Int unrotatedSize = (yRot == 90 || yRot == 270) ? new Vector2Int(size.y, size.x) : size;

            Vector2 center = new Vector2(pb.originCell.x + size.x * 0.5f, pb.originCell.y + size.y * 0.5f);

            facilities.Add(new FacilityEntry
            {
                facilityId = pb.facilityId,
                size = unrotatedSize,
                ownRotationY = yRot,
                centerOffset = center - anchorCellCoord
            });
        }

        foreach (var rp in capturedRails)
        {
            Vector2 cellCenter = new Vector2(rp.cell.x + 0.5f, rp.cell.y + 0.5f);
            rails.Add(new RailEntry
            {
                cellCenterOffset = cellCenter - anchorCellCoord,
                up = rp.up,
                down = rp.down,
                left = rp.left,
                right = rp.right
            });
        }
    }

    private void HandlePastingPhase()
    {
        if (Input.GetMouseButtonDown(1))
        {
            owner.SetSubMode(BuildManager.BuildSubMode.Facility);
            return;
        }

        if (Input.GetKeyDown(rotateKey))
        {
            blueprintRotationY = (blueprintRotationY + 90) % 360;
        }

        if (!TryGetCursorCell(out Vector2Int cursorCell)) return;

        bool allValid;
        UpdateGhostTransforms(cursorCell, out allValid);

        if (Input.GetMouseButtonDown(0))
        {
            if (allValid)
                CommitPlacement(cursorCell);
        }
    }

    private void UpdateGhostTransforms(Vector2Int cursorCell, out bool allValid)
    {
        allValid = true;

        Vector2 rotatedBB = GetRotatedBoundingSize();
        Vector2 newAnchor = new Vector2(cursorCell.x + 0.5f, cursorCell.y + 0.5f);

        // 설비
        for (int i = 0; i < facilities.Count; i++)
        {
            var e = facilities[i];
            GameObject ghost = facilityGhosts[i];
            if (ghost == null) continue;

            Vector2 rotatedOffset = Rotate(e.centerOffset, blueprintRotationY);
            int finalOwnRotation = NormalizeRotation(e.ownRotationY + blueprintRotationY);
            Vector2Int finalSize = (blueprintRotationY == 90 || blueprintRotationY == 270)
                ? new Vector2Int(e.size.y, e.size.x) : e.size;

            Vector2 centerCellCoord = newAnchor + rotatedOffset;
            Vector2Int startCell = new Vector2Int(
                Mathf.RoundToInt(centerCellCoord.x - finalSize.x * 0.5f),
                Mathf.RoundToInt(centerCellCoord.y - finalSize.y * 0.5f));

            Vector3 worldCenter = owner.CellCenterToWorld(
                new Vector2(startCell.x + finalSize.x * 0.5f, startCell.y + finalSize.y * 0.5f));
            Quaternion rot = Quaternion.Euler(0f, finalOwnRotation, 0f);

            ghost.transform.position = worldCenter;
            ghost.transform.rotation = rot;

            var footprint = owner.FootprintOf(startCell, finalSize);
            bool cellsOk = !owner.AreCellsOccupied(footprint);
            bool physicsOk = !owner.IsPhysicallyBlocked(worldCenter, finalSize, rot);
            bool zoneOk = owner.IsInBuildZoneNow;  
            bool thisValid = cellsOk && physicsOk && zoneOk;

            if (thisValid && CellsClashWithPreviousFacilities(i, footprint)) thisValid = false;

            if (!thisValid) allValid = false;
            TintGhost(ghost, thisValid);
        }

        for (int i = 0; i < rails.Count; i++)
        {
            var r = rails[i];
            GameObject ghost = railGhosts[i];
            if (ghost == null) continue;

            Vector2 rotatedOffset = Rotate(r.cellCenterOffset, blueprintRotationY);
            Vector2 centerCellCoord = newAnchor + rotatedOffset;
            Vector2Int targetCell = new Vector2Int(
                Mathf.FloorToInt(centerCellCoord.x),
                Mathf.FloorToInt(centerCellCoord.y));

            Vector3 worldCenter = owner.CellCenterToWorld(new Vector2(targetCell.x + 0.5f, targetCell.y + 0.5f));
            ghost.transform.position = worldCenter;

            bool railCellFree = owner.RailManager == null || !owner.RailManager.RailMap.ContainsKey(targetCell);
            bool notOnFacility = !owner.AreCellsOccupied(new List<Vector2Int> { targetCell });
            bool thisValid = railCellFree && notOnFacility && owner.IsInBuildZoneNow;

            if (!thisValid) allValid = false;
            TintGhostQuad(ghost, thisValid);
        }
    }

    private bool CellsClashWithPreviousFacilities(int currentIndex, List<Vector2Int> cells)
    {
        HashSet<Vector2Int> seen = new();
        Vector2 rotatedBB = GetRotatedBoundingSize();

        for (int i = 0; i < currentIndex; i++)
        {
            var e = facilities[i];
            Vector2 rotatedOffset = Rotate(e.centerOffset, blueprintRotationY);
            Vector2Int finalSize = (blueprintRotationY == 90 || blueprintRotationY == 270)
                ? new Vector2Int(e.size.y, e.size.x) : e.size;

        }
        return false; 
    }

    private void CommitPlacement(Vector2Int cursorCell)
    {
        Vector2 newAnchor = new Vector2(cursorCell.x + 0.5f, cursorCell.y + 0.5f);

        foreach (var e in facilities)
        {
            Vector2 rotatedOffset = Rotate(e.centerOffset, blueprintRotationY);
            int finalOwnRotation = NormalizeRotation(e.ownRotationY + blueprintRotationY);
            Vector2Int finalSize = (blueprintRotationY == 90 || blueprintRotationY == 270)
                ? new Vector2Int(e.size.y, e.size.x) : e.size;

            Vector2 centerCellCoord = newAnchor + rotatedOffset;
            Vector2Int startCell = new Vector2Int(
                Mathf.RoundToInt(centerCellCoord.x - finalSize.x * 0.5f),
                Mathf.RoundToInt(centerCellCoord.y - finalSize.y * 0.5f));

            Vector3 worldCenter = owner.CellCenterToWorld(
                new Vector2(startCell.x + finalSize.x * 0.5f, startCell.y + finalSize.y * 0.5f));
            Quaternion rot = Quaternion.Euler(0f, finalOwnRotation, 0f);

            var footprint = owner.FootprintOf(startCell, finalSize);
            owner.PlaceFacilityImmediate(e.facilityId, worldCenter, rot, footprint);
        }

        foreach (var r in rails)
        {
            Vector2 rotatedOffset = Rotate(r.cellCenterOffset, blueprintRotationY);
            Vector2 centerCellCoord = newAnchor + rotatedOffset;
            Vector2Int targetCell = new Vector2Int(
                Mathf.FloorToInt(centerCellCoord.x),
                Mathf.FloorToInt(centerCellCoord.y));

            RotateDirs(r.up, r.down, r.left, r.right, blueprintRotationY,
                out bool nu, out bool nd, out bool nl, out bool nr);

            owner.RailManager?.PlaceRailImmediate(targetCell, nu, nd, nl, nr);
        }

        owner.SetSubMode(BuildManager.BuildSubMode.Facility);
    }


    private void BuildGhosts()
    {
        ClearGhosts();

        foreach (var e in facilities)
        {
            GameObject prefab = owner.PrefabDatabase != null ? owner.PrefabDatabase.GetPrefab(e.facilityId) : null;
            if (prefab == null) { facilityGhosts.Add(null); continue; }

            GameObject ghost = Instantiate(prefab);
            ghost.name = $"BPGhost_Facility_{e.facilityId}";
            if (ghostParent != null) ghost.transform.SetParent(ghostParent, true);

            foreach (var col in ghost.GetComponentsInChildren<Collider>()) col.enabled = false;
            foreach (var rb in ghost.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
            foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>())
            {
                if (mb != this) mb.enabled = false;
            }
            ApplyGhostMaterial(ghost);

            facilityGhosts.Add(ghost);
        }

        foreach (var _ in rails)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "BPGhost_Rail";
            if (ghostParent != null) quad.transform.SetParent(ghostParent, true);
            Destroy(quad.GetComponent<Collider>());
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(owner.cellSize * 0.85f, owner.cellSize * 0.85f, 1f);

            if (ghostRailMaterial != null)
                quad.GetComponent<MeshRenderer>().sharedMaterial = ghostRailMaterial;

            railGhosts.Add(quad);
        }
    }

    private void ApplyGhostMaterial(GameObject target)
    {
        Material mat = ghostFacilityMaterial != null ? ghostFacilityMaterial : owner.hologramMaterial;
        if (mat == null) return;

        foreach (var r in target.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.materials = mats;
        }
    }

    private void TintGhost(GameObject ghost, bool valid)
    {
        if (ghost == null) return;
        Color c = valid ? ghostValidTint : ghostInvalidTint;
        foreach (var r in ghost.GetComponentsInChildren<Renderer>(true))
        {
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
                block.SetColor("_Color", c);
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor"))
                block.SetColor("_BaseColor", c);
            r.SetPropertyBlock(block);
        }
    }

    private void TintGhostQuad(GameObject quad, bool valid)
    {
        if (quad == null) return;
        Color c = valid ? ghostValidTint : ghostInvalidTint;
        var r = quad.GetComponent<Renderer>();
        if (r == null) return;
        var block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
            block.SetColor("_Color", c);
        if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor"))
            block.SetColor("_BaseColor", c);
        r.SetPropertyBlock(block);
    }

    private void ClearGhosts()
    {
        foreach (var g in facilityGhosts) if (g != null) Destroy(g);
        foreach (var g in railGhosts) if (g != null) Destroy(g);
        facilityGhosts.Clear();
        railGhosts.Clear();
    }


    private void UpdateSelectionBoxVisual()
    {
        if (selectionBoxRenderer == null) return;

        Vector2Int min = new Vector2Int(
            Mathf.Min(dragStartCell.x, dragCurrentCell.x),
            Mathf.Min(dragStartCell.y, dragCurrentCell.y));
        Vector2Int max = new Vector2Int(
            Mathf.Max(dragStartCell.x, dragCurrentCell.x),
            Mathf.Max(dragStartCell.y, dragCurrentCell.y));

        Vector3 origin = owner.GridOriginPos;
        float y = owner.fixedY + selectionYOffset;
        float cs = owner.cellSize;

        Vector3 p0 = new Vector3(origin.x + min.x * cs, y, origin.z + min.y * cs);
        Vector3 p1 = new Vector3(origin.x + (max.x + 1) * cs, y, origin.z + min.y * cs);
        Vector3 p2 = new Vector3(origin.x + (max.x + 1) * cs, y, origin.z + (max.y + 1) * cs);
        Vector3 p3 = new Vector3(origin.x + min.x * cs, y, origin.z + (max.y + 1) * cs);

        selectionBoxRenderer.useWorldSpace = true;
        selectionBoxRenderer.loop = true;
        selectionBoxRenderer.positionCount = 4;
        selectionBoxRenderer.SetPosition(0, p0);
        selectionBoxRenderer.SetPosition(1, p1);
        selectionBoxRenderer.SetPosition(2, p2);
        selectionBoxRenderer.SetPosition(3, p3);
        selectionBoxRenderer.startColor = selectionColor;
        selectionBoxRenderer.endColor = selectionColor;
        selectionBoxRenderer.enabled = true;
    }

    private void HideSelectionBox()
    {
        if (selectionBoxRenderer != null) selectionBoxRenderer.enabled = false;
    }


    private bool TryGetCursorCell(out Vector2Int cell)
    {
        cell = default;
        if (owner.mainCam == null) return false;

        Ray ray = owner.mainCam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, owner.rayDistance, owner.groundMask))
            return false;

        cell = owner.WorldToCellCoord(hit.point);
        return true;
    }

    private static int NormalizeRotation(int deg)
    {
        int r = deg % 360;
        if (r < 0) r += 360;
        return ((r + 45) / 90 * 90) % 360;
    }

    private static Vector2 Rotate(Vector2 v, int rotationY)
    {
        switch (NormalizeRotation(rotationY))
        {
            case 90: return new Vector2(v.y, -v.x);
            case 180: return new Vector2(-v.x, -v.y);
            case 270: return new Vector2(-v.y, v.x);
            default: return v;
        }
    }

    private static void RotateDirs(bool u, bool d, bool l, bool r, int rotationY,
        out bool nu, out bool nd, out bool nl, out bool nr)
    {
        switch (NormalizeRotation(rotationY))
        {
            case 90:
                nu = l; nr = u; nd = r; nl = d; break;
            case 180:
                nu = d; nd = u; nl = r; nr = l; break;
            case 270:
                nu = r; nr = d; nd = l; nl = u; break;
            default:
                nu = u; nd = d; nl = l; nr = r; break;
        }
    }

    private Vector2 GetRotatedBoundingSize()
    {
        return Vector2.zero;
    }
}