using System;
using System.Collections.Generic;
using UnityEngine;

public class RailBuildManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform railParent;
    [SerializeField] private Transform gridOrigin;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private float fixedY = 0f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private LayerMask portMask;
    [SerializeField] private float rayDistance = 300f;

    [Header("Rail Prefabs")]
    [SerializeField] private GameObject straightPrefab;
    [SerializeField] private GameObject cornerPrefab;

    [Header("Preview")]
    [SerializeField] private GameObject previewPrefab;
    [SerializeField] private Material previewValidMaterial;
    [SerializeField] private Material previewInvalidMaterial;

    [Header("Drag")]
    [SerializeField] private int maxStepsPerFrame = 20;

    [Header("Port Indicator")]
    [SerializeField] private GameObject portArrowPrefab;
    [SerializeField] private GameObject portXPrefab;
    [SerializeField] private float indicatorYOffset = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    private static readonly Vector2Int NoCell = new Vector2Int(int.MinValue, int.MinValue);

    private BuildManager owner;

    private BuildPort startPort;
    private BuildPort endPort;

    private bool isRouting = false;
    private bool isDragRouting = false;
    private bool isRailModeActive = false;

    private Vector2Int currentEndCell;

    private BuildPort[] cachedPorts = Array.Empty<BuildPort>();
    private readonly Dictionary<Vector2Int, BuildPort> cachedPortByFrontCell = new();

    private GameObject previewInstance;
    private Renderer[] previewRenderers;

    private readonly Dictionary<Vector2Int, RailPiece> railMap = new();
    private readonly List<Vector2Int> currentPathCells = new();

    private Vector2Int lastPreviewCell = NoCell;
    private bool lastPreviewValid = false;
    private bool lastPreviewWasPort = false;
    private BuildPort lastPreviewPort = null;

    private PlacedBuilding _railHighlightedBuilding;

    private readonly Dictionary<BuildPort, GameObject> portIndicatorMap = new();

    private enum RouteEvalResult { Invalid, Normal, Finish }

    private enum PortIndicatorState { Arrow, X, Hidden }

    public void BeginRailMode(BuildManager buildManager)
    {
        owner = buildManager;
        isRailModeActive = true;
        RefreshPortCache();
        CancelCurrentRouteStateOnly();
        ResetPreviewCache();
        ShowPortIndicators();
        ShowPreview();
        Log("[Rail] Rail Mode ON");
    }

    public void EndRailMode()
    {
        ClearRailHighlight();
        isRailModeActive = false;
        isDragRouting = false;
        CancelCurrentRouteStateOnly();
        ResetPreviewCache();
        HidePortIndicators();
        HidePreview();
        owner = null;
        cachedPorts = Array.Empty<BuildPort>();
        cachedPortByFrontCell.Clear();
        Log("[Rail] Rail Mode OFF");
    }

    /// <summary>
    /// 레일 모드가 활성이고 프리뷰가 켜져 있을 때 프리뷰의 월드 좌표를 돌려준다.
    /// 외부(BuildGridOverlay 등)에서 커서 위치 기준으로 UI를 맞추는 용도.
    /// </summary>
    public bool TryGetPreviewPosition(out Vector3 worldPos)
    {
        if (isRailModeActive && previewInstance != null && previewInstance.activeSelf)
        {
            worldPos = previewInstance.transform.position;
            return true;
        }

        worldPos = default;
        return false;
    }

    private void OnRailSourceSelected(BuildPort port)
    {
        ClearRailHighlight();
        var building = port.OwnerBuilding;
        if (building != null)
        {
            building.SetRailConnectingHighlight(true);
            _railHighlightedBuilding = building;
        }
    }

    private void ClearRailHighlight()
    {
        _railHighlightedBuilding?.SetRailConnectingHighlight(false);
        _railHighlightedBuilding = null;
    }

    public void RefreshPortCache()
    {
        cachedPorts = FindObjectsByType<BuildPort>(FindObjectsSortMode.None);
        cachedPortByFrontCell.Clear();

        for (int i = 0; i < cachedPorts.Length; i++)
        {
            BuildPort port = cachedPorts[i];
            if (port == null) continue;

            Vector2Int frontCell = port.GetFrontCell();

            if (!cachedPortByFrontCell.ContainsKey(frontCell))
                cachedPortByFrontCell.Add(frontCell, port);
            else
                Debug.LogWarning($"[Rail] Duplicate port frontCell detected: {frontCell}, ignored port: {port.name}");
        }
    }

    public void RefreshPortIndicators()
    {
        RefreshPortCache();

        if (!isRailModeActive) return;

        ShowPortIndicators();
    }

    public void TickRailMode()
    {
        if (owner == null || owner.mainCam == null)
            return;

        UpdatePreview();

        if (Input.GetMouseButtonDown(0))
        {
            isDragRouting = true;
            HandleLeftMouseDown();
        }
        else if (Input.GetMouseButton(0) && isDragRouting)
        {
            HandleLeftMouseHold();
        }

        if (Input.GetMouseButtonUp(0))
            isDragRouting = false;

        if (Input.GetMouseButtonDown(1))
        {
            isDragRouting = false;
            CancelCurrentRoute();
        }
    }


    private void ShowPortIndicators()
    {
        HidePortIndicators();

        for (int i = 0; i < cachedPorts.Length; i++)
        {
            BuildPort port = cachedPorts[i];
            if (port == null) continue;

            PortIndicatorState state = GetIndicatorState(port);
            GameObject indicator = CreateIndicator(port, state);

            if (indicator != null)
                portIndicatorMap[port] = indicator;
        }
    }

    private void RefreshIndicators()
    {
        for (int i = 0; i < cachedPorts.Length; i++)
        {
            BuildPort port = cachedPorts[i];
            if (port == null) continue;

            PortIndicatorState state = GetIndicatorState(port);

            if (portIndicatorMap.TryGetValue(port, out GameObject existing))
            {
                if (existing != null)
                    Destroy(existing);

                portIndicatorMap.Remove(port);
            }

            if (state == PortIndicatorState.Hidden) continue;

            GameObject indicator = CreateIndicator(port, state);
            if (indicator != null)
                portIndicatorMap[port] = indicator;
        }
    }

    private GameObject CreateIndicator(BuildPort port, PortIndicatorState state)
    {
        if (state == PortIndicatorState.Hidden) return null;

        GameObject prefab = state == PortIndicatorState.Arrow ? portArrowPrefab : portXPrefab;
        if (prefab == null) return null;

        Vector2Int dir = port.GetWorldDirection();

        Vector3 pos = CellToWorld(port.GetFrontCell());
        pos += new Vector3(dir.x, 0f, dir.y) * cellSize * 0.5f;
        pos.y += indicatorYOffset;

        Quaternion rot = Quaternion.identity;
        if (state == PortIndicatorState.Arrow)
        {
            if (dir != Vector2Int.zero)
            {
                float yAngle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                rot = Quaternion.Euler(90f, yAngle, 0f);
            }
            else
            {
                rot = Quaternion.Euler(90f, 0f, 0f);
            }
        }
        else
        {
            rot = Quaternion.Euler(90f, 0f, 0f);
        }

        GameObject indicator = Instantiate(prefab, pos, rot);
        indicator.name = $"PortIndicator_{port.name}_{state}";
        return indicator;
    }

    private PortIndicatorState GetIndicatorState(BuildPort port)
    {
        if (!isRouting)
        {
            if (!port.HasCapacity)
                return PortIndicatorState.X;

            if (port.CanStartConnection() || port.CanEndConnection())
                return PortIndicatorState.Arrow;

            return PortIndicatorState.X;
        }

        if (port == startPort)
            return PortIndicatorState.Arrow; 

        if (!port.HasCapacity)
            return PortIndicatorState.X;

        if (startPort != null &&
            port.OwnerBuilding != null &&
            startPort.OwnerBuilding != null &&
            port.OwnerBuilding == startPort.OwnerBuilding)
            return PortIndicatorState.X;

        return port.CanEndConnection() ? PortIndicatorState.Arrow : PortIndicatorState.X;
    }

    private void HidePortIndicators()
    {
        foreach (var (_, indicator) in portIndicatorMap)
        {
            if (indicator != null)
                Destroy(indicator);
        }
        portIndicatorMap.Clear();
    }


    private void HandleLeftMouseDown()
    {
        bool hasPort = TryGetPortUnderMouse(out BuildPort hoveredPort) && hoveredPort != null;

        if (!isRouting)
        {
            if (hasPort)
            {
                if (!TryStartRoute(hoveredPort))
                    isDragRouting = false;
            }
            else
            {
                isDragRouting = false;
            }
            return;
        }

        if (hasPort)
        {
            if (CanFinishByPlacingNextCell(hoveredPort))
                TryPlacePathToward(hoveredPort.GetFrontCell());
            else
                Log("[Rail] Hovered port is not a valid finish target.");

            return;
        }

        if (TryGetMouseCell(out Vector2Int cell))
            TryPlacePathToward(cell);
    }

    private void HandleLeftMouseHold()
    {
        if (!isRouting) return;

        if (TryGetPortUnderMouse(out BuildPort hoveredPort) && hoveredPort != null)
        {
            if (CanFinishByPlacingNextCell(hoveredPort))
                TryPlacePathToward(hoveredPort.GetFrontCell());

            return;
        }

        if (TryGetMouseCell(out Vector2Int cell))
            TryPlacePathToward(cell);
    }

    private bool TryStartRoute(BuildPort port)
    {
        if (port == null || !port.CanStartConnection())
        {
            Log("[Rail] Start port invalid.");
            return false;
        }

        Vector2Int firstCell = port.GetFrontCell();

        if (!CanUseCellAsRail(firstCell, NoCell, allowExisting: true))
        {
            Log("[Rail] Front cell of start port cannot be used.");
            return false;
        }

        RailPiece firstPiece = EnsureRailExists(firstCell);
        if (firstPiece == null)
            return false;

        startPort = port;
        endPort = null;
        isRouting = true;
        currentEndCell = firstCell;
        currentPathCells.Clear();
        currentPathCells.Add(firstCell);

        RefreshIndicators();
        ResetPreviewCache();
        Log($"[Rail] Route started from {startPort.name}");
        return true;
    }


    private void TryPlacePathToward(Vector2Int targetCell)
    {
        if (!isRouting || targetCell == currentEndCell)
            return;

        int steps = 0;

        while (isRouting && currentEndCell != targetCell && steps < maxStepsPerFrame)
        {
            if (!TryAdvanceOneStep(targetCell))
                break;

            steps++;
        }
    }

    private bool TryAdvanceOneStep(Vector2Int targetCell)
    {
        if (targetCell == currentEndCell)
        {
            Log("[Rail] TryAdvanceOneStep called with same target cell.");
            return false;
        }

        Vector2Int delta = targetCell - currentEndCell;

        Vector2Int primaryStep;
        Vector2Int secondaryStep;

        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
        {
            primaryStep = delta.x == 0 ? Vector2Int.zero : new Vector2Int(delta.x > 0 ? 1 : -1, 0);
            secondaryStep = delta.y == 0 ? Vector2Int.zero : new Vector2Int(0, delta.y > 0 ? 1 : -1);
        }
        else
        {
            primaryStep = delta.y == 0 ? Vector2Int.zero : new Vector2Int(0, delta.y > 0 ? 1 : -1);
            secondaryStep = delta.x == 0 ? Vector2Int.zero : new Vector2Int(delta.x > 0 ? 1 : -1, 0);
        }

        if (TryEvaluateStep(primaryStep, out Vector2Int nextCell, out RouteEvalResult result, out BuildPort finishPort))
        {
            if (!PlaceStep(nextCell)) return false;

            if (result == RouteEvalResult.Finish && finishPort != null)
            {
                CompleteRoute(finishPort);
                return false;
            }

            return true;
        }

        if (TryEvaluateStep(secondaryStep, out nextCell, out result, out finishPort))
        {
            if (!PlaceStep(nextCell)) return false;

            if (result == RouteEvalResult.Finish && finishPort != null)
            {
                CompleteRoute(finishPort);
                return false;
            }

            return true;
        }

        return false;
    }

    private bool TryEvaluateStep(
        Vector2Int step,
        out Vector2Int nextCell,
        out RouteEvalResult result,
        out BuildPort finishPort)
    {
        nextCell = currentEndCell;
        result = RouteEvalResult.Invalid;
        finishPort = null;

        if (step == Vector2Int.zero) return false;

        nextCell = currentEndCell + step;
        result = EvaluateCellCandidate(nextCell, out finishPort);

        return result != RouteEvalResult.Invalid;
    }

    private bool PlaceStep(Vector2Int cell)
    {
        if (!ConnectOrCreateRail(currentEndCell, cell))
            return false;

        if (currentPathCells.Count == 0 || currentPathCells[currentPathCells.Count - 1] != cell)
            currentPathCells.Add(cell);

        currentEndCell = cell;
        return true;
    }


    private RouteEvalResult EvaluateCellCandidate(Vector2Int nextCell, out BuildPort finishPort)
    {
        finishPort = null;

        if (!isRouting) return RouteEvalResult.Invalid;
        if (nextCell == currentEndCell) return RouteEvalResult.Invalid;
        if (!IsAdjacent(currentEndCell, nextCell)) return RouteEvalResult.Invalid;

        if (IsFirstExpansionPending() && nextCell != GetRequiredFirstExpansionCell())
            return RouteEvalResult.Invalid;

        if (cachedPortByFrontCell.TryGetValue(nextCell, out BuildPort port) && port != null)
        {
            if (IsExactFinishCandidate(port, currentEndCell, nextCell))
            {
                finishPort = port;
                return RouteEvalResult.Finish;
            }

            return RouteEvalResult.Invalid;
        }

        return CanUseCellAsRail(nextCell, currentEndCell, allowExisting: true)
            ? RouteEvalResult.Normal
            : RouteEvalResult.Invalid;
    }

    private bool IsFirstExpansionPending()
        => isRouting && startPort != null && currentPathCells.Count == 1;

    private bool HasLeftStartForwardOnce()
        => currentPathCells.Count >= 2;

    private Vector2Int GetRequiredFirstExpansionCell()
        => startPort.GetFrontCell() + startPort.GetWorldDirection();

    private bool IsExactFinishCandidate(BuildPort port, Vector2Int prevCell, Vector2Int nextCell)
    {
        if (port == null || !port.CanEndConnection()) return false;
        if (startPort == null) return false;
        if (port == startPort) return false;
        if (!HasLeftStartForwardOnce()) return false;

        if (port.OwnerBuilding != null &&
            startPort.OwnerBuilding != null &&
            port.OwnerBuilding == startPort.OwnerBuilding)
            return false;

        Vector2Int frontCell = port.GetFrontCell();
        Vector2Int requiredApproachCell = frontCell + port.GetWorldDirection();

        return nextCell == frontCell && prevCell == requiredApproachCell;
    }

    private bool CanFinishNow(BuildPort port)
    {
        if (!isRouting || port == null) return false;
        if (currentPathCells.Count < 2) return false;
        if (currentEndCell != port.GetFrontCell()) return false;

        Vector2Int prevCell = currentPathCells[currentPathCells.Count - 2];
        return IsExactFinishCandidate(port, prevCell, currentEndCell);
    }

    private bool CanFinishByPlacingNextCell(BuildPort port)
    {
        if (!isRouting || port == null) return false;

        RouteEvalResult result = EvaluateCellCandidate(port.GetFrontCell(), out BuildPort finishPort);
        return result == RouteEvalResult.Finish && finishPort == port;
    }

    private void CompleteRoute(BuildPort port)
    {
        if (!CanFinishNow(port)) return;

        startPort.AddConnection();
        port.AddConnection();
        endPort = port;

        AssignFlowDirections();

        Log($"[Rail] Route completed: {startPort.name} -> {endPort.name}");

        isDragRouting = false;
        CancelCurrentRouteStateOnly();
        ResetPreviewCache();
        RefreshIndicators();
    }

    private void AssignFlowDirections()
    {
        if (currentPathCells.Count == 0 || startPort == null) return;

        for (int i = 0; i < currentPathCells.Count; i++)
        {
            if (!railMap.TryGetValue(currentPathCells[i], out RailPiece piece)) continue;

            Vector2Int flowFrom = i == 0
                ? -startPort.GetWorldDirection()
                : currentPathCells[i - 1] - currentPathCells[i];

            piece.flowFrom = flowFrom;
            piece.ApplyVisual(straightPrefab, cornerPrefab);
        }
    }

    private void CancelCurrentRoute()
    {
        CancelCurrentRouteStateOnly();
        ResetPreviewCache();
        RefreshIndicators();
        Log("[Rail] Route canceled");
    }

    private void CancelCurrentRouteStateOnly()
    {
        isRouting = false;
        isDragRouting = false;
        startPort = null;
        endPort = null;
        currentEndCell = default;
        currentPathCells.Clear();
    }


    private bool CanUseCellAsRail(Vector2Int cell, Vector2Int previousCell, bool allowExisting)
    {
        if (!railMap.TryGetValue(cell, out RailPiece existingPiece))
            return true;

        if (!allowExisting) return false;

        int existingConnections = GetConnectionCount(existingPiece);

        if (previousCell == NoCell || previousCell == cell)
            return existingConnections < 2;

        if (IsConnectedTo(existingPiece, previousCell)) return true;

        return existingConnections < 2;
    }

    private int GetConnectionCount(RailPiece piece)
    {
        int count = 0;
        if (piece.up) count++;
        if (piece.down) count++;
        if (piece.left) count++;
        if (piece.right) count++;
        return count;
    }

    private bool IsConnectedTo(RailPiece piece, Vector2Int otherCell)
    {
        if (piece == null) return false;

        if (piece.cell + Vector2Int.up == otherCell) return piece.up;
        if (piece.cell + Vector2Int.down == otherCell) return piece.down;
        if (piece.cell + Vector2Int.left == otherCell) return piece.left;
        if (piece.cell + Vector2Int.right == otherCell) return piece.right;

        return false;
    }

    private bool ConnectOrCreateRail(Vector2Int fromCell, Vector2Int toCell)
    {
        if (!IsAdjacent(fromCell, toCell))
        {
            Debug.LogWarning($"[Rail] Connect failed - not adjacent: {fromCell} -> {toCell}");
            return false;
        }

        RailPiece fromPiece = GetOrCreateRailPiece(fromCell, out bool createdFrom);
        RailPiece toPiece = GetOrCreateRailPiece(toCell, out bool createdTo);

        if (fromPiece == null || toPiece == null)
        {
            if (createdFrom) RemoveRailPiece(fromCell);
            if (createdTo) RemoveRailPiece(toCell);
            return false;
        }

        if (!CanAddConnection(fromPiece, toCell) || !CanAddConnection(toPiece, fromCell))
        {
            Debug.LogWarning($"[Rail] Connect failed - limit reached: {fromCell} -> {toCell}");

            if (createdFrom && GetConnectionCount(fromPiece) == 0) RemoveRailPiece(fromCell);
            if (createdTo && GetConnectionCount(toPiece) == 0) RemoveRailPiece(toCell);

            return false;
        }

        SetConnection(fromPiece, toCell, true);
        SetConnection(toPiece, fromCell, true);

        RefreshRail(fromCell);
        RefreshRail(toCell);
        return true;
    }

    private RailPiece EnsureRailExists(Vector2Int cell)
    {
        if (railMap.TryGetValue(cell, out RailPiece existing))
            return existing;

        RailPiece newPiece = CreateRailPiece(cell);
        if (newPiece == null) return null;

        railMap.Add(cell, newPiece);
        RefreshRail(cell);
        return newPiece;
    }

    private RailPiece GetOrCreateRailPiece(Vector2Int cell, out bool createdNow)
    {
        createdNow = false;

        if (railMap.TryGetValue(cell, out RailPiece existing))
            return existing;

        RailPiece newPiece = CreateRailPiece(cell);
        if (newPiece == null) return null;

        railMap.Add(cell, newPiece);
        createdNow = true;
        return newPiece;
    }

    private void RemoveRailPiece(Vector2Int cell)
    {
        if (!railMap.TryGetValue(cell, out RailPiece piece)) return;

        railMap.Remove(cell);

        if (piece != null && piece.gameObject != null)
            Destroy(piece.gameObject);
    }

    private bool CanAddConnection(RailPiece piece, Vector2Int otherCell)
    {
        if (piece == null) return false;
        if (IsConnectedTo(piece, otherCell)) return true;
        return GetConnectionCount(piece) < 2;
    }

    private void SetConnection(RailPiece piece, Vector2Int otherCell, bool value)
    {
        Vector2Int delta = otherCell - piece.cell;

        if (delta == Vector2Int.up) piece.up = value;
        else if (delta == Vector2Int.down) piece.down = value;
        else if (delta == Vector2Int.left) piece.left = value;
        else if (delta == Vector2Int.right) piece.right = value;
    }

    private RailPiece CreateRailPiece(Vector2Int cell)
    {
        GameObject root = new GameObject($"Rail_{cell.x}_{cell.y}");
        root.transform.SetParent(railParent);
        root.transform.position = CellToWorld(cell);

        RailPiece piece = root.AddComponent<RailPiece>();
        piece.cell = cell;
        return piece;
    }

    private void RefreshRail(Vector2Int cell)
    {
        if (railMap.TryGetValue(cell, out RailPiece piece))
            piece.ApplyVisual(straightPrefab, cornerPrefab);
    }


    private bool TryGetPortUnderMouse(out BuildPort port)
    {
        port = null;

        if (owner == null || owner.mainCam == null) return false;

        Ray ray = owner.mainCam.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, portMask))
            return false;

        port = hit.collider.GetComponentInParent<BuildPort>();
        return port != null;
    }

    private bool TryGetMouseCell(out Vector2Int cell)
    {
        cell = default;

        if (owner == null || owner.mainCam == null) return false;
        if (cellSize <= 0f) return false;

        Ray ray = owner.mainCam.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, groundMask))
            return false;

        Vector3 origin = gridOrigin != null ? gridOrigin.position : Vector3.zero;
        Vector3 local = hit.point - origin;

        cell = new Vector2Int(
            Mathf.FloorToInt(local.x / cellSize),
            Mathf.FloorToInt(local.z / cellSize)
        );

        return true;
    }

    private bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        Vector2Int d = b - a;
        return Mathf.Abs(d.x) + Mathf.Abs(d.y) == 1;
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        Vector3 origin = gridOrigin != null ? gridOrigin.position : Vector3.zero;
        return new Vector3(
            origin.x + (cell.x + 0.5f) * cellSize,
            fixedY,
            origin.z + (cell.y + 0.5f) * cellSize
        );
    }


    private void ShowPreview()
    {
        if (previewInstance != null) return;

        GameObject source = previewPrefab != null ? previewPrefab : straightPrefab;
        if (source == null) return;

        previewInstance = Instantiate(source);
        previewInstance.name = "RailPreview";

        foreach (Collider col in previewInstance.GetComponentsInChildren<Collider>())
            col.enabled = false;

        previewRenderers = previewInstance.GetComponentsInChildren<Renderer>(true);
    }

    private void HidePreview()
    {
        if (previewInstance != null)
            Destroy(previewInstance);

        previewInstance = null;
        previewRenderers = null;
    }

    private void UpdatePreview()
    {
        ShowPreview();
        if (previewInstance == null) return;

        if (TryGetPortUnderMouse(out BuildPort port) && port != null)
        {
            Vector2Int frontCell = port.GetFrontCell();
            bool valid = !isRouting
                ? port.CanStartConnection() && CanUseCellAsRail(frontCell, NoCell, allowExisting: true)
                : CanFinishByPlacingNextCell(port);

            bool same = lastPreviewWasPort && lastPreviewPort == port && lastPreviewValid == valid;
            if (same) return;

            previewInstance.SetActive(true);
            previewInstance.transform.position = CellToWorld(frontCell);
            previewInstance.transform.rotation = Quaternion.identity;
            ApplyPreviewMaterial(valid);

            lastPreviewWasPort = true;
            lastPreviewPort = port;
            lastPreviewCell = frontCell;
            lastPreviewValid = valid;
            return;
        }

        if (!isRouting)
        {
            previewInstance.SetActive(false);
            ResetPreviewCache();
            return;
        }

        if (!TryGetMouseCell(out Vector2Int cell))
        {
            previewInstance.SetActive(false);
            ResetPreviewCache();
            return;
        }

        RouteEvalResult evalResult = EvaluateCellCandidate(cell, out _);
        bool validCell = evalResult != RouteEvalResult.Invalid;

        bool sameCellSameValidity = !lastPreviewWasPort && cell == lastPreviewCell && lastPreviewValid == validCell;
        if (sameCellSameValidity) return;

        previewInstance.SetActive(true);
        previewInstance.transform.position = CellToWorld(cell);
        previewInstance.transform.rotation = Quaternion.identity;
        ApplyPreviewMaterial(validCell);

        lastPreviewWasPort = false;
        lastPreviewPort = null;
        lastPreviewCell = cell;
        lastPreviewValid = validCell;
    }

    private void ApplyPreviewMaterial(bool valid)
    {
        if (previewRenderers == null) return;

        Material mat = valid ? previewValidMaterial : previewInvalidMaterial;
        if (mat == null) return;

        foreach (Renderer r in previewRenderers)
        {
            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
            r.materials = mats;
        }
    }

    private void ResetPreviewCache()
    {
        lastPreviewCell = NoCell;
        lastPreviewValid = false;
        lastPreviewWasPort = false;
        lastPreviewPort = null;
    }

    private void Log(string message)
    {
        if (enableDebugLog)
            Debug.Log(message);
    }
}