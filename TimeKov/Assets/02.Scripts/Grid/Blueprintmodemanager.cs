using System.Collections.Generic;
using UnityEngine;

public class BlueprintModeManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode rotateKey = KeyCode.R;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;

    [Header("Selection Box Visual")]
    [Tooltip("드래그 박스 테두리 색상")]
    [SerializeField] private Color selectionOutlineColor = new Color(1f, 1f, 1f, 0.9f);
    [Tooltip("드래그 박스 내부 채움 색상 (알파 낮게)")]
    [SerializeField] private Color selectionFillColor = new Color(0.4f, 0.85f, 1f, 0.15f);
    [Tooltip("테두리 두께 (픽셀)")]
    [SerializeField] private float selectionBorderPixels = 1f;

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
        public Vector2Int startCellOffset;
    }

    private struct RailEntry
    {
        public Vector2Int cellOffset;
        public bool up, down, left, right;
    }

    private readonly List<FacilityEntry> facilities = new();
    private readonly List<RailEntry> rails = new();
    private Vector2Int anchorCell;

    private int blueprintRotationY = 0;
    private readonly List<GameObject> facilityGhosts = new();
    private readonly List<GameObject> railGhosts = new();

    public bool IsActive => isActive;

    public bool TryGetPointerWorldPosition(out Vector3 worldPos)
    {
        worldPos = default;
        if (!isActive || owner == null || owner.mainCam == null) return false;

        if (state != State.Pasting) return false;

        Ray ray = owner.mainCam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, owner.rayDistance, owner.groundMask))
            return false;

        worldPos = hit.point;
        return true;
    }

    public bool TryGetBlueprintBoundsRadius(out int radiusCells)
    {
        radiusCells = 0;
        if (!isActive || state != State.Pasting) return false;
        if (facilities.Count == 0 && rails.Count == 0) return false;

        int minX = int.MaxValue, minZ = int.MaxValue;
        int maxX = int.MinValue, maxZ = int.MinValue;

        foreach (var e in facilities)
        {
            Vector2Int size = (blueprintRotationY == 90 || blueprintRotationY == 270)
                ? new Vector2Int(e.size.y, e.size.x) : e.size;
            Vector2Int s = RotateFacilityStartOffset(e.startCellOffset, e.size, blueprintRotationY);

            if (s.x < minX) minX = s.x;
            if (s.y < minZ) minZ = s.y;
            if (s.x + size.x > maxX) maxX = s.x + size.x;
            if (s.y + size.y > maxZ) maxZ = s.y + size.y;
        }

        foreach (var r in rails)
        {
            Vector2Int c = RotateCell(r.cellOffset, blueprintRotationY);
            if (c.x < minX) minX = c.x;
            if (c.y < minZ) minZ = c.y;
            if (c.x + 1 > maxX) maxX = c.x + 1;
            if (c.y + 1 > maxZ) maxZ = c.y + 1;
        }

        int halfX = Mathf.Max(Mathf.Abs(minX), Mathf.Abs(maxX));
        int halfZ = Mathf.Max(Mathf.Abs(minZ), Mathf.Abs(maxZ));
        radiusCells = Mathf.Max(halfX, halfZ);
        return true;
    }

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

        anchorCell = new Vector2Int(
            Mathf.FloorToInt((bbMinX + bbMaxX) * 0.5f),
            Mathf.FloorToInt((bbMinZ + bbMaxZ) * 0.5f));

        foreach (var (pb, size) in captured)
        {
            int yRot = NormalizeRotation(Mathf.RoundToInt(pb.transform.rotation.eulerAngles.y));
            Vector2Int unrotatedSize = (yRot == 90 || yRot == 270) ? new Vector2Int(size.y, size.x) : size;

            facilities.Add(new FacilityEntry
            {
                facilityId = pb.facilityId,
                size = unrotatedSize,
                ownRotationY = yRot,
                startCellOffset = pb.originCell - anchorCell
            });
        }

        foreach (var rp in capturedRails)
        {
            rails.Add(new RailEntry
            {
                cellOffset = rp.cell - anchorCell,
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

        for (int i = 0; i < facilities.Count; i++)
        {
            var e = facilities[i];
            GameObject ghost = facilityGhosts[i];
            if (ghost == null) continue;

            ComputeFacilityPlacement(e, cursorCell,
                out Vector2Int startCell, out Vector2Int finalSize, out int finalOwnRotation);

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

            if (!thisValid) allValid = false;
            TintGhost(ghost, thisValid);
        }

        for (int i = 0; i < rails.Count; i++)
        {
            var r = rails[i];
            GameObject ghost = railGhosts[i];
            if (ghost == null) continue;

            Vector2Int targetCell = cursorCell + RotateCell(r.cellOffset, blueprintRotationY);
            Vector3 worldCenter = owner.CellCenterToWorld(new Vector2(targetCell.x + 0.5f, targetCell.y + 0.5f));
            ghost.transform.position = worldCenter;

            bool railCellFree = owner.RailManager == null || !owner.RailManager.RailMap.ContainsKey(targetCell);
            bool notOnFacility = !owner.AreCellsOccupied(new List<Vector2Int> { targetCell });
            bool thisValid = railCellFree && notOnFacility && owner.IsInBuildZoneNow;

            if (!thisValid) allValid = false;
            TintGhostQuad(ghost, thisValid);
        }
    }

    private void CommitPlacement(Vector2Int cursorCell)
    {
        foreach (var e in facilities)
        {
            ComputeFacilityPlacement(e, cursorCell,
                out Vector2Int startCell, out Vector2Int finalSize, out int finalOwnRotation);

            Vector3 worldCenter = owner.CellCenterToWorld(
                new Vector2(startCell.x + finalSize.x * 0.5f, startCell.y + finalSize.y * 0.5f));
            Quaternion rot = Quaternion.Euler(0f, finalOwnRotation, 0f);

            var footprint = owner.FootprintOf(startCell, finalSize);
            owner.PlaceFacilityWithHologram(e.facilityId, worldCenter, rot, footprint);
        }

        foreach (var r in rails)
        {
            Vector2Int targetCell = cursorCell + RotateCell(r.cellOffset, blueprintRotationY);
            RotateDirs(r.up, r.down, r.left, r.right, blueprintRotationY,
                out bool nu, out bool nd, out bool nl, out bool nr);
            owner.RailManager?.PlaceRailImmediate(targetCell, nu, nd, nl, nr);
        }

        owner.SetSubMode(BuildManager.BuildSubMode.Facility);
    }

    private void ComputeFacilityPlacement(FacilityEntry e, Vector2Int anchorCellNow,
        out Vector2Int startCell, out Vector2Int finalSize, out int finalOwnRotation)
    {
        finalOwnRotation = NormalizeRotation(e.ownRotationY + blueprintRotationY);
        finalSize = (blueprintRotationY == 90 || blueprintRotationY == 270)
            ? new Vector2Int(e.size.y, e.size.x) : e.size;

        Vector2Int rotatedStart = RotateFacilityStartOffset(e.startCellOffset, e.size, blueprintRotationY);
        startCell = anchorCellNow + rotatedStart;
    }

    private static Vector2Int RotateCell(Vector2Int v, int rotationY)
    {
        switch (NormalizeRotation(rotationY))
        {
            case 90: return new Vector2Int(v.y, -v.x);
            case 180: return new Vector2Int(-v.x, -v.y);
            case 270: return new Vector2Int(-v.y, v.x);
            default: return v;
        }
    }

    private static Vector2Int RotateFacilityStartOffset(Vector2Int startOff, Vector2Int size, int rotationY)
    {
        switch (NormalizeRotation(rotationY))
        {
            case 90: return new Vector2Int(startOff.y, -(startOff.x + size.x - 1));
            case 180: return new Vector2Int(-(startOff.x + size.x - 1), -(startOff.y + size.y - 1));
            case 270: return new Vector2Int(-(startOff.y + size.y - 1), startOff.x);
            default: return startOff;
        }
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

    private static Texture2D _pixelTex;
    private static Texture2D PixelTex
    {
        get
        {
            if (_pixelTex == null)
            {
                _pixelTex = new Texture2D(1, 1);
                _pixelTex.SetPixel(0, 0, Color.white);
                _pixelTex.Apply();
            }
            return _pixelTex;
        }
    }

    private void OnGUI()
    {
        if (!isActive || !isMouseDragging) return;
        if (owner == null || owner.mainCam == null) return;

        Vector2Int min = new Vector2Int(
            Mathf.Min(dragStartCell.x, dragCurrentCell.x),
            Mathf.Min(dragStartCell.y, dragCurrentCell.y));
        Vector2Int max = new Vector2Int(
            Mathf.Max(dragStartCell.x, dragCurrentCell.x),
            Mathf.Max(dragStartCell.y, dragCurrentCell.y));

        Vector3 origin = owner.GridOriginPos;
        float y = owner.fixedY + 0.01f;
        float cs = owner.cellSize;

        Vector3 w0 = new Vector3(origin.x + min.x * cs, y, origin.z + min.y * cs);
        Vector3 w1 = new Vector3(origin.x + (max.x + 1) * cs, y, origin.z + min.y * cs);
        Vector3 w2 = new Vector3(origin.x + (max.x + 1) * cs, y, origin.z + (max.y + 1) * cs);
        Vector3 w3 = new Vector3(origin.x + min.x * cs, y, origin.z + (max.y + 1) * cs);

        Vector2 s0 = WorldToGui(w0);
        Vector2 s1 = WorldToGui(w1);
        Vector2 s2 = WorldToGui(w2);
        Vector2 s3 = WorldToGui(w3);

        float xMin = Mathf.Min(s0.x, s1.x, s2.x, s3.x);
        float xMax = Mathf.Max(s0.x, s1.x, s2.x, s3.x);
        float yMin = Mathf.Min(s0.y, s1.y, s2.y, s3.y);
        float yMax = Mathf.Max(s0.y, s1.y, s2.y, s3.y);

        Rect rect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);

        DrawRectFilled(rect, selectionFillColor);

        float b = Mathf.Max(1f, selectionBorderPixels);
        DrawRectFilled(new Rect(rect.xMin, rect.yMin, rect.width, b), selectionOutlineColor);
        DrawRectFilled(new Rect(rect.xMin, rect.yMax - b, rect.width, b), selectionOutlineColor);
        DrawRectFilled(new Rect(rect.xMin, rect.yMin, b, rect.height), selectionOutlineColor);
        DrawRectFilled(new Rect(rect.xMax - b, rect.yMin, b, rect.height), selectionOutlineColor);
    }

    private Vector2 WorldToGui(Vector3 world)
    {
        Vector3 sp = owner.mainCam.WorldToScreenPoint(world);
        return new Vector2(sp.x, Screen.height - sp.y);
    }

    private static void DrawRectFilled(Rect rect, Color color)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, PixelTex);
        GUI.color = prev;
    }

    private void UpdateSelectionBoxVisual() { /* OnGUI가 매 프레임 그림 */ }
    private void HideSelectionBox() { /* OnGUI가 isMouseDragging=false면 안 그림 */ }

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
}