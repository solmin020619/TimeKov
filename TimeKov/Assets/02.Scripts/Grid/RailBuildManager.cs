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

    [Header("Routing")]
    [SerializeField] private int maxStepsPerFrame = 200;

    [Header("Ghost Preview")]
    [Tooltip("직선 ghost 전용 머티리얼. 비워두면 rail 머티리얼에 alpha/intensity 조정으로 fallback.")]
    [SerializeField] private Material ghostStraightMaterial;
    [Tooltip("코너 ghost 전용 머티리얼. 비워두면 rail 머티리얼에 alpha/intensity 조정으로 fallback.")]
    [SerializeField] private Material ghostCornerMaterial;
    [Tooltip("라우팅 중 source/destination 빌딩에 입힐 ghost 머티리얼. 비워두면 BuildManager.hologramMaterial(파란색) 사용.")]
    [SerializeField] private Material ghostBuildingMaterial;
    [SerializeField, Range(0f, 1f)] private float ghostAlpha = 0.4f;

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
    private bool isRailModeActive = false;

    private Vector2Int currentEndCell;

    private BuildPort[] cachedPorts = Array.Empty<BuildPort>();
    private readonly Dictionary<Vector2Int, BuildPort> cachedPortByFrontCell = new();

    private GameObject previewInstance;
    private Renderer[] previewRenderers;

    private readonly List<GameObject> pathPreviewInstances = new();
    private readonly List<Vector2Int> lastPredictedPath = new();
    private Vector2Int? lastPredictedTarget = null;
    private Vector2Int? lastPredictedFromCell = null;
    private bool lastPredictedReachesPort = false;

    // Visual overlay on the last committed cell while a prediction is showing,
    // so its rendered connection visually merges into the prediction's first cell.
    private RailPiece overlayedRailPiece = null;
    private bool overlaySavedUp, overlaySavedDown, overlaySavedLeft, overlaySavedRight;

    private readonly Dictionary<Vector2Int, RailPiece> railMap = new();
    private readonly List<Vector2Int> currentPathCells = new();

    private Vector2Int lastPreviewCell = NoCell;
    private bool lastPreviewValid = false;
    private bool lastPreviewWasPort = false;
    private BuildPort lastPreviewPort = null;

    private PlacedBuilding _railHighlightedBuilding;
    private PlacedBuilding _railTargetHighlightedBuilding;
    private PlacedBuilding _railHoverPreviewBuilding;

    private readonly Dictionary<BuildPort, GameObject> portIndicatorMap = new();

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
        ClearHoverSourcePreview();
        isRailModeActive = false;
        CancelCurrentRouteStateOnly();
        ResetPreviewCache();
        ClearPathPreviewInstances();
        HidePortIndicators();
        HidePreview();
        owner = null;
        cachedPorts = Array.Empty<BuildPort>();
        cachedPortByFrontCell.Clear();
        Log("[Rail] Rail Mode OFF");
    }

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

    public IReadOnlyDictionary<Vector2Int, RailPiece> RailMap => railMap;
    public Transform GridOriginRail => gridOrigin;
    public float CellSizeRail => cellSize;
    public float FixedYRail => fixedY;

    public RailPiece PlaceRailImmediate(Vector2Int cell, bool up, bool down, bool left, bool right)
    {
        if (railMap.ContainsKey(cell))
            return null;

        RailPiece piece = CreateRailPiece(cell);
        if (piece == null) return null;

        piece.up = up;
        piece.down = down;
        piece.left = left;
        piece.right = right;

        railMap.Add(cell, piece);
        piece.ApplyVisual(straightPrefab, cornerPrefab);

        RefreshNeighbors(cell);
        return piece;
    }

    private void RefreshNeighbors(Vector2Int cell)
    {
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in dirs)
        {
            Vector2Int neighbor = cell + d;
            if (railMap.TryGetValue(neighbor, out RailPiece np) && np != null)
                np.ApplyVisual(straightPrefab, cornerPrefab);
        }
    }

    public bool RemoveRailAt(Vector2Int cell)
    {
        if (!railMap.TryGetValue(cell, out RailPiece piece) || piece == null)
            return false;

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in dirs)
        {
            Vector2Int neighbor = cell + d;
            if (railMap.TryGetValue(neighbor, out RailPiece np) && np != null)
                SetConnection(np, cell, false);
        }

        railMap.Remove(cell);
        if (piece.gameObject != null)
            Destroy(piece.gameObject);

        foreach (var d in dirs)
        {
            Vector2Int neighbor = cell + d;
            if (railMap.TryGetValue(neighbor, out RailPiece np) && np != null)
                np.ApplyVisual(straightPrefab, cornerPrefab);
        }

        ReleasePortIfFrontCell(cell);

        return true;
    }

    private void ReleasePortIfFrontCell(Vector2Int cell)
    {
        ValidateAllPortConnections();
    }

    private void ValidateAllPortConnections()
    {
        if (cachedPorts == null || cachedPorts.Length == 0)
            RefreshPortCache();

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        for (int i = 0; i < cachedPorts.Length; i++)
        {
            BuildPort port = cachedPorts[i];
            if (port == null) continue;
            if (port.connectionCount == 0) continue;

            Vector2Int frontCell = port.GetFrontCell();
            if (!ChainReachesAnotherPort(frontCell, port, dirs))
                port.RemoveConnection();
        }

        if (isRailModeActive)
            RefreshIndicators();
    }

    private bool ChainReachesAnotherPort(Vector2Int startCell, BuildPort startPort, Vector2Int[] dirs)
    {
        if (!railMap.ContainsKey(startCell)) return false;

        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(startCell);
        visited.Add(startCell);

        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            if (!railMap.TryGetValue(c, out RailPiece piece) || piece == null) continue;

            if (c != startCell &&
                cachedPortByFrontCell.TryGetValue(c, out BuildPort otherPort) &&
                otherPort != null &&
                otherPort != startPort)
                return true;

            foreach (Vector2Int d in dirs)
            {
                Vector2Int neighbor = c + d;
                if (visited.Contains(neighbor)) continue;
                if (!IsConnectedTo(piece, neighbor)) continue;
                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return false;
    }

    public bool HasRailAt(Vector2Int cell) => railMap.ContainsKey(cell);

    public void RemoveRailsConnectedToBuilding(PlacedBuilding building)
    {
        if (building == null) return;

        BuildPort[] ports = building.GetComponentsInChildren<BuildPort>();
        if (ports == null || ports.Length == 0) return;

        HashSet<Vector2Int> toRemove = new HashSet<Vector2Int>();
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (BuildPort port in ports)
        {
            if (port == null) continue;

            Vector2Int frontCell = port.GetFrontCell();
            if (!railMap.ContainsKey(frontCell)) continue;
            if (toRemove.Contains(frontCell)) continue;

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(frontCell);
            toRemove.Add(frontCell);

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                if (!railMap.TryGetValue(cell, out RailPiece piece) || piece == null) continue;

                foreach (Vector2Int d in dirs)
                {
                    Vector2Int neighbor = cell + d;
                    if (toRemove.Contains(neighbor)) continue;
                    if (!IsConnectedTo(piece, neighbor)) continue;
                    if (!railMap.ContainsKey(neighbor)) continue;

                    toRemove.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        foreach (Vector2Int cell in toRemove)
            RemoveRailAt(cell);
    }

    private Material GetBuildingGhostMaterial()
    {
        if (ghostBuildingMaterial != null) return ghostBuildingMaterial;
        if (owner != null) return owner.hologramMaterial;
        return null;
    }

    private void OnRailSourceSelected(BuildPort port)
    {
        ClearRailHighlight();
        var building = port.OwnerBuilding;
        if (building != null)
        {
            building.SetRailConnectingHighlight(true);
            building.SetGhostMode(true, GetBuildingGhostMaterial());
            _railHighlightedBuilding = building;
        }
    }

    private void ClearRailHighlight()
    {
        if (_railHighlightedBuilding != null)
        {
            _railHighlightedBuilding.SetRailConnectingHighlight(false);
            _railHighlightedBuilding.SetGhostMode(false);
        }
        _railHighlightedBuilding = null;
        ClearDestinationBuildingHighlight();
    }

    private void ClearDestinationBuildingHighlight()
    {
        if (_railTargetHighlightedBuilding != null
            && _railTargetHighlightedBuilding != _railHighlightedBuilding)
        {
            _railTargetHighlightedBuilding.SetRailConnectingHighlight(false);
            _railTargetHighlightedBuilding.SetGhostMode(false);
        }
        _railTargetHighlightedBuilding = null;
    }

    private void UpdateDestinationBuildingHighlight()
    {
        if (!isRouting)
        {
            ClearDestinationBuildingHighlight();
            return;
        }

        PlacedBuilding newTarget = null;
        if (TryGetPortUnderMouse(out BuildPort hoveredPort)
            && hoveredPort != null
            && IsCandidateDestination(hoveredPort))
        {
            newTarget = hoveredPort.OwnerBuilding;
        }

        if (newTarget == _railTargetHighlightedBuilding) return;

        // Remove old target highlight (skip if it's the source - keep that on).
        if (_railTargetHighlightedBuilding != null
            && _railTargetHighlightedBuilding != _railHighlightedBuilding)
        {
            _railTargetHighlightedBuilding.SetRailConnectingHighlight(false);
            _railTargetHighlightedBuilding.SetGhostMode(false);
        }

        _railTargetHighlightedBuilding = newTarget;

        if (_railTargetHighlightedBuilding != null
            && _railTargetHighlightedBuilding != _railHighlightedBuilding)
        {
            _railTargetHighlightedBuilding.SetRailConnectingHighlight(true);
            _railTargetHighlightedBuilding.SetGhostMode(true, GetBuildingGhostMaterial());
        }
    }

    /// <summary>
    /// Idle 상태에서 시작 가능한 포트 위에 마우스를 올리면 해당 빌딩을 미리 ghost 로 표시.
    /// 클릭하면 그대로 source highlight 로 전환되고, 마우스 빠지면 해제.
    /// </summary>
    private void UpdateHoverSourcePreview()
    {
        PlacedBuilding newHover = null;

        if (!isRouting
            && TryGetPortUnderMouse(out BuildPort port)
            && port != null
            && port.CanStartConnection())
        {
            newHover = port.OwnerBuilding;
        }

        if (newHover == _railHoverPreviewBuilding) return;

        // 이전 hover 빌딩이 있으면 해제 (단, 라우팅 중 source/target 으로 승격된 경우엔 그대로 둠)
        if (_railHoverPreviewBuilding != null
            && _railHoverPreviewBuilding != _railHighlightedBuilding
            && _railHoverPreviewBuilding != _railTargetHighlightedBuilding)
        {
            _railHoverPreviewBuilding.SetRailConnectingHighlight(false);
            _railHoverPreviewBuilding.SetGhostMode(false);
        }

        _railHoverPreviewBuilding = newHover;

        // 새 hover 빌딩에 ghost 적용 (이미 source/target 인 경우 중복 적용 방지)
        if (_railHoverPreviewBuilding != null
            && _railHoverPreviewBuilding != _railHighlightedBuilding
            && _railHoverPreviewBuilding != _railTargetHighlightedBuilding)
        {
            _railHoverPreviewBuilding.SetRailConnectingHighlight(true);
            _railHoverPreviewBuilding.SetGhostMode(true, GetBuildingGhostMaterial());
        }
    }

    private void ClearHoverSourcePreview()
    {
        if (_railHoverPreviewBuilding != null
            && _railHoverPreviewBuilding != _railHighlightedBuilding
            && _railHoverPreviewBuilding != _railTargetHighlightedBuilding)
        {
            _railHoverPreviewBuilding.SetRailConnectingHighlight(false);
            _railHoverPreviewBuilding.SetGhostMode(false);
        }
        _railHoverPreviewBuilding = null;
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
            HandleLeftClick();

        if (Input.GetMouseButtonDown(1))
            CancelCurrentRoute();
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
                // Arrow prefab 헤드가 시설 안쪽을 향하게 모델링돼 있어
                // Output 포트(시설 밖으로 흐름)에선 +180° 보정해야 화살표가 시설 밖을 가리킴.
                if (port.portType == PortType.Output)
                    yAngle += 180f;
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

            // 라우팅 시작은 Output 포트에서만 가능. Input 포트는 X.
            if (port.CanStartConnection())
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

    private void HandleLeftClick()
    {
        bool hasPort = TryGetPortUnderMouse(out BuildPort hoveredPort) && hoveredPort != null;

        if (!isRouting)
        {
            if (hasPort)
            {
                TryStartRoute(hoveredPort);
            }
            else if (TryGetMouseCell(out Vector2Int startCell))
            {
                // 포트가 아니더라도 기존 레일 셀에서 이어 시작 가능
                TryStartRouteFromCell(startCell);
            }
            return;
        }

        if (hasPort)
        {
            if (IsCandidateDestination(hoveredPort))
                TryPlacePathToward(hoveredPort.GetFrontCell());
            else
                Log("[Rail] Hovered port is not a valid finish target.");

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

        // 시작 셀에도 flow direction 미리 부여. 라우팅 중 코너에서 ghost 와 시각 일치
        firstPiece.flowFrom = -port.GetWorldDirection();
        firstPiece.pathIndex = 0;
        firstPiece.ApplyVisual(straightPrefab, cornerPrefab);

        startPort = port;
        endPort = null;
        isRouting = true;
        currentEndCell = firstCell;
        currentPathCells.Clear();
        currentPathCells.Add(firstCell);

        OnRailSourceSelected(port);
        RefreshIndicators();
        ResetPreviewCache();
        Log($"[Rail] Route started from {startPort.name}");
        return true;
    }

    /// <summary>
    /// 포트가 아니라 기존에 깔린 레일 셀에서 라우팅을 이어 시작.
    /// 셀에 빈 connection 슬롯이 있어야 함(< 2 connections).
    /// </summary>
    private bool TryStartRouteFromCell(Vector2Int cell)
    {
        if (!railMap.TryGetValue(cell, out RailPiece piece) || piece == null)
            return false;

        if (GetConnectionCount(piece) >= 2)
        {
            Log("[Rail] Cell already at max connections, cannot extend.");
            return false;
        }

        startPort = null;
        endPort = null;
        isRouting = true;
        currentEndCell = cell;
        currentPathCells.Clear();
        currentPathCells.Add(cell);

        // 기존 셀에 connection 이 1개 있으면 그 방향을 incoming flow 로 둠
        piece.flowFrom = ComputeStartCellFlowFrom(piece);
        piece.pathIndex = 0;
        piece.ApplyVisual(straightPrefab, cornerPrefab);

        RefreshIndicators();
        ResetPreviewCache();
        Log($"[Rail] Route started from existing cell {cell}");
        return true;
    }

    private Vector2Int ComputeStartCellFlowFrom(RailPiece piece)
    {
        if (piece == null) return Vector2Int.zero;
        if (piece.up) return Vector2Int.up;
        if (piece.down) return Vector2Int.down;
        if (piece.left) return Vector2Int.left;
        if (piece.right) return Vector2Int.right;
        return Vector2Int.zero;
    }

    private void TryPlacePathToward(Vector2Int targetCell)
    {
        if (!isRouting || targetCell == currentEndCell)
            return;

        // Revert any visual overlay BEFORE state mutation, so PlaceStep's
        // ConnectOrCreateRail operates on real boolean state and the overlay's
        // saved snapshot doesn't get out of sync.
        RestoreOverlayedRailPiece();

        List<Vector2Int> path = SimulatePath(targetCell, out bool reachesPort);

        foreach (Vector2Int cell in path)
        {
            if (!PlaceStep(cell))
            {
                ClearPathPreviewInstances();
                return;
            }
        }

        // Commit happened. Drop stale ghost preview so the next
        // UpdatePathPreview rebuilds fresh from the new currentEndCell.
        ClearPathPreviewInstances();

        if (reachesPort
            && cachedPortByFrontCell.TryGetValue(currentEndCell, out BuildPort port)
            && port != null)
        {
            CompleteRoute(port);
        }
    }

    private bool PlaceStep(Vector2Int cell)
    {
        Vector2Int previousEnd = currentEndCell;

        if (!ConnectOrCreateRail(currentEndCell, cell))
            return false;

        if (currentPathCells.Count == 0 || currentPathCells[currentPathCells.Count - 1] != cell)
            currentPathCells.Add(cell);

        currentEndCell = cell;

        // 부분 commit 시점에도 placed 셀이 ghost 와 동일한 flow direction / pathIndex 를 가지게
        // 해서 시각이 어긋나지 않도록 갱신.
        if (railMap.TryGetValue(cell, out RailPiece newPiece) && newPiece != null)
        {
            newPiece.flowFrom = previousEnd - cell;
            newPiece.pathIndex = currentPathCells.Count - 1;
            newPiece.ApplyVisual(straightPrefab, cornerPrefab);
        }

        return true;
    }

    private List<Vector2Int> SimulatePath(Vector2Int targetCell, out bool reachesPort)
    {
        List<Vector2Int> result = new();
        reachesPort = false;

        if (!isRouting) return result;

        // If target is a candidate destination port's frontCell, route to its requiredApproach
        // first, then add the head-on final step into the port.
        BuildPort destPort = null;
        Vector2Int simulatedTarget = targetCell;
        if (cachedPortByFrontCell.TryGetValue(targetCell, out BuildPort portAtTarget)
            && IsCandidateDestination(portAtTarget))
        {
            Vector2Int requiredApproach = targetCell + portAtTarget.GetWorldDirection();
            if (requiredApproach != currentEndCell
                && !currentPathCells.Contains(requiredApproach))
            {
                destPort = portAtTarget;
                simulatedTarget = requiredApproach;
            }
        }

        Vector2Int simEnd = currentEndCell;
        HashSet<Vector2Int> simVisited = new(currentPathCells);
        Vector2Int? lastDir = null;

        int safety = maxStepsPerFrame;
        while (simEnd != simulatedTarget && safety-- > 0)
        {
            if (!TrySimulateOneStep(simEnd, simVisited, simulatedTarget, result.Count, lastDir, out Vector2Int next, out bool isFinish))
                break;

            lastDir = next - simEnd;
            result.Add(next);
            simVisited.Add(next);
            simEnd = next;

            if (isFinish)
            {
                reachesPort = true;
                break;
            }
        }

        // After reaching the requiredApproach, append the port frontCell as head-on final step.
        if (destPort != null && simEnd == simulatedTarget && !reachesPort
            && !simVisited.Contains(targetCell)
            && IsExactFinishCandidate(destPort, simEnd, targetCell, result.Count))
        {
            result.Add(targetCell);
            reachesPort = true;
        }

        return result;
    }

    private bool IsCandidateDestination(BuildPort port)
    {
        if (!isRouting || port == null) return false;
        // startPort 가 null 이면 (cell-start) port-비교/동일빌딩 검사 건너뜀
        if (startPort != null && port == startPort) return false;
        if (!port.CanEndConnection()) return false;
        if (startPort != null
            && port.OwnerBuilding != null
            && startPort.OwnerBuilding != null
            && port.OwnerBuilding == startPort.OwnerBuilding)
            return false;
        return true;
    }

    private bool TrySimulateOneStep(
        Vector2Int simEnd,
        HashSet<Vector2Int> simVisited,
        Vector2Int targetCell,
        int simExpansionsSoFar,
        Vector2Int? lastDir,
        out Vector2Int next,
        out bool isFinish)
    {
        next = simEnd;
        isFinish = false;

        Vector2Int delta = targetCell - simEnd;
        if (delta == Vector2Int.zero) return false;

        // First expansion is forced to be in start port's direction
        bool isFirstSimExpansion = currentPathCells.Count == 1 && simExpansionsSoFar == 0;
        if (isFirstSimExpansion && startPort != null)
        {
            Vector2Int forcedStep = startPort.GetWorldDirection();
            if (forcedStep != Vector2Int.zero
                && TrySimulateCandidate(simEnd + forcedStep, simEnd, simVisited, simExpansionsSoFar, out next, out isFinish))
                return true;
            return false;
        }

        Vector2Int xStep = delta.x == 0 ? Vector2Int.zero : new Vector2Int(delta.x > 0 ? 1 : -1, 0);
        Vector2Int yStep = delta.y == 0 ? Vector2Int.zero : new Vector2Int(0, delta.y > 0 ? 1 : -1);

        // L-shape preference: keep going on the same axis as the last step.
        // When that axis is exhausted (delta becomes 0), switch to the perpendicular axis.
        Vector2Int firstStep, secondStep;
        if (lastDir.HasValue && lastDir.Value.x != 0)
        {
            // Last step was X-axis. Continue X if there's still X delta, else switch to Y.
            if (xStep != Vector2Int.zero) { firstStep = xStep; secondStep = yStep; }
            else { firstStep = yStep; secondStep = xStep; }
        }
        else if (lastDir.HasValue && lastDir.Value.y != 0)
        {
            // Last step was Y-axis. Continue Y if possible, else switch to X.
            if (yStep != Vector2Int.zero) { firstStep = yStep; secondStep = xStep; }
            else { firstStep = xStep; secondStep = yStep; }
        }
        else
        {
            // No prior direction (right after forced first expansion or pure start).
            // Pick the axis with the larger remaining distance to make a clean L-shape.
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)) { firstStep = xStep; secondStep = yStep; }
            else { firstStep = yStep; secondStep = xStep; }
        }

        if (firstStep != Vector2Int.zero
            && TrySimulateCandidate(simEnd + firstStep, simEnd, simVisited, simExpansionsSoFar, out next, out isFinish))
            return true;

        if (secondStep != Vector2Int.zero
            && TrySimulateCandidate(simEnd + secondStep, simEnd, simVisited, simExpansionsSoFar, out next, out isFinish))
            return true;

        return false;
    }

    private bool TrySimulateCandidate(
        Vector2Int candidate,
        Vector2Int simEnd,
        HashSet<Vector2Int> simVisited,
        int simExpansionsSoFar,
        out Vector2Int next,
        out bool isFinish)
    {
        next = candidate;
        isFinish = false;

        if (candidate == simEnd) return false;
        if (simVisited.Contains(candidate)) return false;
        if (!IsAdjacent(simEnd, candidate)) return false;

        // first expansion 강제 방향 제약은 startPort 가 있을 때만 (cell-start 케이스는 자유)
        bool isFirstSimExpansion = currentPathCells.Count == 1 && simExpansionsSoFar == 0;
        if (isFirstSimExpansion && startPort != null && candidate != GetRequiredFirstExpansionCell())
            return false;

        if (cachedPortByFrontCell.TryGetValue(candidate, out BuildPort port) && port != null)
        {
            if (IsExactFinishCandidate(port, simEnd, candidate, simExpansionsSoFar))
            {
                isFinish = true;
                return true;
            }
            return false;
        }

        return CanUseCellAsRail(candidate, simEnd, allowExisting: true);
    }

    private bool IsFirstExpansionPending()
        => isRouting && startPort != null && currentPathCells.Count == 1;

    private Vector2Int GetRequiredFirstExpansionCell()
        => startPort.GetFrontCell() + startPort.GetWorldDirection();

    private bool IsExactFinishCandidate(BuildPort port, Vector2Int prevCell, Vector2Int nextCell, int extraSimulatedCells = 0)
    {
        if (port == null || !port.CanEndConnection()) return false;
        // startPort 가 null 이면 레일 셀에서 시작한 케이스. port-에서-시작 검사만 스킵
        if (startPort != null && port == startPort) return false;

        // Need at least one forward expansion before finishing. During simulation,
        // currentPathCells has only the start frontCell; the simulated cells
        // beyond that count too.
        if (currentPathCells.Count + extraSimulatedCells < 2) return false;

        // 동일 빌딩 검사도 startPort 가 있을 때만
        if (startPort != null
            && port.OwnerBuilding != null
            && startPort.OwnerBuilding != null
            && port.OwnerBuilding == startPort.OwnerBuilding)
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

    private void CompleteRoute(BuildPort port)
    {
        if (!CanFinishNow(port)) return;

        if (startPort != null) startPort.AddConnection();
        port.AddConnection();
        endPort = port;

        AssignFlowDirections();

        Log($"[Rail] Route completed: {(startPort != null ? startPort.name : "[cell-start]")} -> {endPort.name}");

        ClearRailHighlight();
        CancelCurrentRouteStateOnly();
        ResetPreviewCache();
        ClearPathPreviewInstances();
        RefreshIndicators();
    }

    private void AssignFlowDirections()
    {
        if (currentPathCells.Count == 0) return;

        for (int i = 0; i < currentPathCells.Count; i++)
        {
            if (!railMap.TryGetValue(currentPathCells[i], out RailPiece piece)) continue;

            Vector2Int flowFrom;
            if (i == 0)
            {
                flowFrom = startPort != null
                    ? -startPort.GetWorldDirection()
                    : ComputeStartCellFlowFrom(piece);
            }
            else
            {
                flowFrom = currentPathCells[i - 1] - currentPathCells[i];
            }

            piece.flowFrom = flowFrom;
            piece.pathIndex = i;
            piece.ApplyVisual(straightPrefab, cornerPrefab);
        }
    }

    private void CancelCurrentRoute()
    {
        ClearRailHighlight();
        CancelCurrentRouteStateOnly();
        ResetPreviewCache();
        ClearPathPreviewInstances();
        RefreshIndicators();
        Log("[Rail] Route canceled");
    }

    private void CancelCurrentRouteStateOnly()
    {
        isRouting = false;
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
        if (!isRouting)
            ClearPathPreviewInstances();
        else
            UpdatePathPreview();

        // Always run - keeps a per-cell preview at the mouse position so the
        // user can see the rail mode is active even on plain ground, and so
        // BuildGridOverlay has a position to anchor the grid patch to.
        UpdateSinglePreview();

        UpdateDestinationBuildingHighlight();
        UpdateHoverSourcePreview();
    }

    private void UpdateSinglePreview()
    {
        ShowPreview();
        if (previewInstance == null) return;

        Vector2Int targetCell;
        BuildPort port = null;
        bool isPort = TryGetPortUnderMouse(out port) && port != null;
        bool valid;

        if (isPort)
        {
            targetCell = port.GetFrontCell();
            valid = !isRouting
                ? port.CanStartConnection() && CanUseCellAsRail(targetCell, NoCell, allowExisting: true)
                : IsCandidateDestination(port);
        }
        else if (TryGetMouseCell(out targetCell))
        {
            // 라우팅 안 하는 중에 기존 레일 셀(슬롯 빈) 위에 hover 시 거기서 시작 가능 (녹색)
            if (!isRouting
                && railMap.TryGetValue(targetCell, out RailPiece existing)
                && existing != null
                && GetConnectionCount(existing) < 2)
            {
                valid = true;
            }
            else
            {
                valid = false;
            }
        }
        else
        {
            previewInstance.SetActive(false);
            ResetPreviewCache();
            return;
        }

        // While routing, the predicted ghost path already renders a piece at
        // its last cell. Hide the single preview there to avoid overlap.
        if (isRouting && lastPredictedPath.Count > 0
            && lastPredictedPath[lastPredictedPath.Count - 1] == targetCell)
        {
            previewInstance.SetActive(false);
            ResetPreviewCache();
            return;
        }

        bool same = lastPreviewWasPort == isPort
            && lastPreviewPort == port
            && lastPreviewCell == targetCell
            && lastPreviewValid == valid
            && previewInstance.activeSelf;
        if (same) return;

        previewInstance.SetActive(true);
        previewInstance.transform.position = CellToWorld(targetCell);
        previewInstance.transform.rotation = Quaternion.identity;
        ApplyPreviewMaterial(valid);

        lastPreviewWasPort = isPort;
        lastPreviewPort = port;
        lastPreviewCell = targetCell;
        lastPreviewValid = valid;
    }

    private void UpdatePathPreview()
    {
        Vector2Int? targetCell = null;

        if (TryGetPortUnderMouse(out BuildPort hoveredPort) && hoveredPort != null)
        {
            if (IsCandidateDestination(hoveredPort))
                targetCell = hoveredPort.GetFrontCell();
        }
        else if (TryGetMouseCell(out Vector2Int cell))
        {
            targetCell = cell;
        }

        if (!targetCell.HasValue || targetCell.Value == currentEndCell)
        {
            if (lastPredictedTarget.HasValue)
            {
                ClearPathPreviewInstances();
            }
            return;
        }

        if (lastPredictedTarget.HasValue
            && lastPredictedTarget.Value == targetCell.Value
            && lastPredictedFromCell.HasValue
            && lastPredictedFromCell.Value == currentEndCell)
            return;

        List<Vector2Int> predicted = SimulatePath(targetCell.Value, out bool reachesPort);

        ClearPathPreviewInstances();
        RenderPathPreview(predicted);

        // Visually merge currentEnd with the prediction's first cell so the
        // committed rail doesn't look like a dead-end during routing.
        if (predicted.Count > 0)
            OverlayCurrentEndConnection(predicted[0]);

        lastPredictedPath.Clear();
        lastPredictedPath.AddRange(predicted);
        lastPredictedTarget = targetCell.Value;
        lastPredictedFromCell = currentEndCell;
        lastPredictedReachesPort = reachesPort;
    }

    private void RenderPathPreview(List<Vector2Int> predictedPath)
    {
        if (predictedPath.Count == 0) return;

        for (int i = 0; i < predictedPath.Count; i++)
        {
            Vector2Int cell = predictedPath[i];
            Vector2Int prevCell = i == 0 ? currentEndCell : predictedPath[i - 1];
            bool hasNext = i < predictedPath.Count - 1;
            Vector2Int nextCell = hasNext ? predictedPath[i + 1] : cell;

            bool up = false, down = false, left = false, right = false;
            ApplyConnectionFromNeighbor(cell, prevCell, ref up, ref down, ref left, ref right);
            if (hasNext)
                ApplyConnectionFromNeighbor(cell, nextCell, ref up, ref down, ref left, ref right);

            // straight vs corner. RailPiece.ApplyVisual 와 동일한 판정 로직
            int connCount = (up ? 1 : 0) + (down ? 1 : 0) + (left ? 1 : 0) + (right ? 1 : 0);
            bool useStraight = connCount <= 1 || (up && down) || (left && right);
            Material ghostOverride = useStraight ? ghostStraightMaterial : ghostCornerMaterial;

            GameObject root = new GameObject($"GhostRail_{cell.x}_{cell.y}");
            root.transform.SetParent(railParent);
            root.transform.position = CellToWorld(cell);

            RailPiece piece = root.AddComponent<RailPiece>();
            piece.cell = cell;
            piece.up = up;
            piece.down = down;
            piece.left = left;
            piece.right = right;
            piece.flowFrom = prevCell - cell;
            piece.pathIndex = currentPathCells.Count + i;

            piece.ApplyVisual(straightPrefab, cornerPrefab);

            foreach (Collider col in root.GetComponentsInChildren<Collider>())
                col.enabled = false;

            ApplyGhostMaterial(root, ghostOverride, currentPathCells.Count + i);

            pathPreviewInstances.Add(root);
        }
    }

    private void ApplyConnectionFromNeighbor(Vector2Int cell, Vector2Int neighbor, ref bool up, ref bool down, ref bool left, ref bool right)
    {
        Vector2Int delta = neighbor - cell;
        if (delta == Vector2Int.up) up = true;
        else if (delta == Vector2Int.down) down = true;
        else if (delta == Vector2Int.left) left = true;
        else if (delta == Vector2Int.right) right = true;
    }

    private static readonly string[] GhostColorPropertyCandidates =
    {
        "_rail_color",
        "_RailColor",
        "_BaseColor",
        "_Color",
        "_MainColor",
        "_TintColor"
    };

    private static readonly string[] GhostIntensityPropertyCandidates =
    {
        "_intelsity",   // 프로젝트의 typo'd 이름 그대로
        "_Intensity",
        "_Brightness"
    };

    private static readonly int GhostPathOffsetId = Shader.PropertyToID("_PathOffset");

    private void ApplyGhostMaterial(GameObject ghost, Material ghostOverride, int pathIndex)
    {
        Renderer[] renderers = ghost.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;

                // 1. override 머티리얼 있으면 통째로 swap
                if (ghostOverride != null)
                {
                    mats[i] = ghostOverride;
                    continue;
                }

                // 2. 없으면 기존 머티리얼에 intensity / alpha 조정
                Material m = mats[i];

                foreach (string prop in GhostIntensityPropertyCandidates)
                {
                    if (!m.HasProperty(prop)) continue;
                    float v = m.GetFloat(prop);
                    m.SetFloat(prop, v * ghostAlpha);
                    break;
                }

                foreach (string prop in GhostColorPropertyCandidates)
                {
                    if (!m.HasProperty(prop)) continue;
                    Color c = m.GetColor(prop);
                    c.a *= ghostAlpha;
                    m.SetColor(prop, c);
                    break;
                }
            }
            r.materials = mats;

            // 머티리얼 swap 으로 _PathOffset 이 asset default(보통 0)로 리셋되니
            // 다시 pathIndex 로 덮어써서 ghost 들끼리 흐름이 이어지게.
            Material[] instanced = r.materials;
            for (int i = 0; i < instanced.Length; i++)
            {
                if (instanced[i] == null) continue;
                if (!instanced[i].HasProperty(GhostPathOffsetId)) continue;
                instanced[i].SetFloat(GhostPathOffsetId, pathIndex);
            }
        }
    }

    private void ClearPathPreviewInstances()
    {
        RestoreOverlayedRailPiece();

        for (int i = 0; i < pathPreviewInstances.Count; i++)
        {
            if (pathPreviewInstances[i] != null)
            {
                // DestroyImmediate to avoid 1-frame visual residue.
                // Ghost previews are isolated visual-only objects with no
                // external references, so this is safe at runtime.
                DestroyImmediate(pathPreviewInstances[i]);
            }
        }
        pathPreviewInstances.Clear();
        lastPredictedPath.Clear();
        lastPredictedTarget = null;
        lastPredictedFromCell = null;
        lastPredictedReachesPort = false;
    }

    private void OverlayCurrentEndConnection(Vector2Int predictionFirstCell)
    {
        // Always start clean: revert any previous overlay first.
        RestoreOverlayedRailPiece();

        if (!railMap.TryGetValue(currentEndCell, out RailPiece piece) || piece == null)
            return;

        Vector2Int delta = predictionFirstCell - currentEndCell;
        bool wantUp    = delta == Vector2Int.up;
        bool wantDown  = delta == Vector2Int.down;
        bool wantLeft  = delta == Vector2Int.left;
        bool wantRight = delta == Vector2Int.right;

        // Already connected in that direction. nothing to overlay.
        if ((wantUp && piece.up) || (wantDown && piece.down) ||
            (wantLeft && piece.left) || (wantRight && piece.right))
            return;

        overlayedRailPiece = piece;
        overlaySavedUp    = piece.up;
        overlaySavedDown  = piece.down;
        overlaySavedLeft  = piece.left;
        overlaySavedRight = piece.right;

        if (wantUp)    piece.up    = true;
        if (wantDown)  piece.down  = true;
        if (wantLeft)  piece.left  = true;
        if (wantRight) piece.right = true;

        piece.ApplyVisual(straightPrefab, cornerPrefab);
    }

    private void RestoreOverlayedRailPiece()
    {
        if (overlayedRailPiece == null) return;

        overlayedRailPiece.up    = overlaySavedUp;
        overlayedRailPiece.down  = overlaySavedDown;
        overlayedRailPiece.left  = overlaySavedLeft;
        overlayedRailPiece.right = overlaySavedRight;

        overlayedRailPiece.ApplyVisual(straightPrefab, cornerPrefab);
        overlayedRailPiece = null;
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