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

    private BuildManager owner;

    private BuildPort startPort;
    private BuildPort endPort;

    private bool isRouting = false;
    private Vector2Int currentEndCell;

    private GameObject previewInstance;
    private Renderer[] previewRenderers;

    private readonly Dictionary<Vector2Int, RailPiece> railMap = new();
    private readonly List<Vector2Int> currentPathCells = new();

    private enum RouteEvalResult
    {
        Invalid,
        Normal,
        Finish
    }

    public void BeginRailMode(BuildManager buildManager)
    {
        owner = buildManager;
        CancelCurrentRoute();
        ShowPreview();
        Debug.Log("[Rail] Rail Mode ON");
    }

    public void EndRailMode()
    {
        CancelCurrentRoute();
        HidePreview();
        Debug.Log("[Rail] Rail Mode OFF");
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

    private void HandleLeftClick()
    {
        BuildPort hoveredPort = null;
        bool hasPortUnderMouse = TryGetPortUnderMouse(out hoveredPort) && hoveredPort != null;

        if (!isRouting)
        {
            if (hasPortUnderMouse)
                TryStartRoute(hoveredPort);

            return;
        }

 
        if (hasPortUnderMouse)
        {
            if (CanFinishByPlacingNextCell(hoveredPort))
            {
                Vector2Int finishCell = hoveredPort.GetFrontCell();
                TryPlaceNextStep(finishCell);
            }
            else
            {
                Debug.Log("[Rail] Hovered port is not a valid finish target.");
            }

            return;
        }
        if (!TryGetMouseCell(out Vector2Int cell))
            return;

        TryPlaceNextStep(cell);
    }

    private void TryStartRoute(BuildPort port)
    {
        if (port == null || !port.CanStartConnection())
        {
            Debug.Log("[Rail] Start port invalid.");
            return;
        }

        startPort = port;
        endPort = null;

        Vector2Int firstCell = startPort.GetFrontCell();

        if (!CanUseCellAsRail(firstCell, default, allowExisting: true))
        {
            Debug.Log("[Rail] Front cell of start port cannot be used.");
            return;
        }

        isRouting = true;
        currentEndCell = firstCell;
        currentPathCells.Clear();

        EnsureRailExists(firstCell);
        currentPathCells.Add(firstCell);

        Debug.Log($"[Rail] Route started from {startPort.name}");
    }

    private bool IsFirstExpansionPending()
    {
        return isRouting && startPort != null && currentPathCells.Count == 1;
    }

    private bool HasLeftStartForwardOnce()
    {
        return currentPathCells.Count >= 2;
    }

    private Vector2Int GetRequiredFirstExpansionCell()
    {
        return startPort.GetFrontCell() + startPort.GetWorldDirection();
    }

    private RouteEvalResult EvaluateCellCandidate(Vector2Int nextCell, out BuildPort finishPort)
    {
        finishPort = null;

        if (!isRouting)
            return RouteEvalResult.Invalid;

        if (nextCell == currentEndCell)
            return RouteEvalResult.Invalid;

        if (!IsAdjacent(currentEndCell, nextCell))
            return RouteEvalResult.Invalid;

        if (IsFirstExpansionPending() && nextCell != GetRequiredFirstExpansionCell())
            return RouteEvalResult.Invalid;

        BuildPort[] ports = FindObjectsByType<BuildPort>(FindObjectsSortMode.None);
        for (int i = 0; i < ports.Length; i++)
        {
            BuildPort port = ports[i];
            if (port == null)
                continue;

            Vector2Int frontCell = port.GetFrontCell();
            if (frontCell != nextCell)
                continue;

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

    private bool IsExactFinishCandidate(BuildPort port, Vector2Int prevCell, Vector2Int nextCell)
    {
        if (port == null || !port.CanEndConnection())
            return false;

        if (startPort == null)
            return false;

        if (port == startPort)
            return false;

        if (!HasLeftStartForwardOnce())
            return false;

        if (port.OwnerBuilding != null && startPort.OwnerBuilding != null && port.OwnerBuilding == startPort.OwnerBuilding)
            return false;

        Vector2Int frontCell = port.GetFrontCell();
        Vector2Int requiredApproachCell = frontCell + port.GetWorldDirection();

        return nextCell == frontCell && prevCell == requiredApproachCell;
    }

    private bool CanFinishNow(BuildPort port)
    {
        if (!isRouting || port == null)
            return false;

        if (currentPathCells.Count < 2)
            return false;

        Vector2Int frontCell = port.GetFrontCell();
        if (currentEndCell != frontCell)
            return false;

        Vector2Int prevCell = currentPathCells[currentPathCells.Count - 2];
        return IsExactFinishCandidate(port, prevCell, frontCell);
    }

    private bool CanFinishByPlacingNextCell(BuildPort port)
    {
        if (!isRouting || port == null)
            return false;

        RouteEvalResult result = EvaluateCellCandidate(port.GetFrontCell(), out BuildPort finishPort);
        return result == RouteEvalResult.Finish && finishPort == port;
    }
    private void CompleteRoute(BuildPort port)
    {
        if (!CanFinishNow(port))
            return;

        startPort.AddConnection();
        port.AddConnection();
        endPort = port;

        // 경로 방향에 맞게 각 레일 조각의 쉐이더 흐름 방향을 설정
        AssignFlowDirections();

        Debug.Log($"[Rail] Route completed: {startPort.name} -> {endPort.name}");

        CancelCurrentRouteStateOnly();
    }

    /// <summary>
    /// currentPathCells 순서를 기반으로 각 RailPiece에 flowFrom을 설정하고
    /// 쉐이더가 경로 방향과 일치하도록 ApplyVisual을 재호출합니다.
    /// </summary>
    private void AssignFlowDirections()
    {
        if (currentPathCells.Count == 0 || startPort == null)
            return;

        for (int i = 0; i < currentPathCells.Count; i++)
        {
            if (!railMap.TryGetValue(currentPathCells[i], out RailPiece piece))
                continue;

            Vector2Int flowFrom;

            if (i == 0)
            {
                // 첫 번째 셀: startPort가 가리키는 방향의 반대에서 흐름이 들어옴
                // GetWorldDirection()이 포트가 그리드 안쪽으로 향하는 방향이라면
                // 흐름 진입은 그 반대 방향 (포트 쪽)
                flowFrom = -startPort.GetWorldDirection();
            }
            else
            {
                // 이전 셀 → 현재 셀 방향 = 흐름 진입 방향 (curr 기준 prev 쪽)
                Vector2Int prev = currentPathCells[i - 1];
                Vector2Int curr = currentPathCells[i];
                flowFrom = prev - curr; // curr 입장에서 prev가 있는 방향
            }

            piece.flowFrom = flowFrom;
            piece.ApplyVisual(straightPrefab, cornerPrefab);
        }
    }

    private void TryPlaceNextStep(Vector2Int cell)
    {
        if (!isRouting)
            return;

        RouteEvalResult result = EvaluateCellCandidate(cell, out BuildPort finishPort);
        if (result == RouteEvalResult.Invalid)
        {
            Debug.Log("[Rail] This cell cannot be used.");
            return;
        }

        ConnectOrCreateRail(currentEndCell, cell);

        if (currentPathCells.Count == 0 || currentPathCells[currentPathCells.Count - 1] != cell)
            currentPathCells.Add(cell);

        currentEndCell = cell;

        if (result == RouteEvalResult.Finish && finishPort != null)
            CompleteRoute(finishPort);
    }

    private bool CanUseCellAsRail(Vector2Int cell, Vector2Int previousCell, bool allowExisting)
    {
        if (!railMap.TryGetValue(cell, out RailPiece existingPiece))
            return true;

        if (!allowExisting)
            return false;

        int existingConnections = GetConnectionCount(existingPiece);

        if (previousCell == default || previousCell == cell)
            return existingConnections < 2;

        bool alreadyConnectedToPrevious = IsConnectedTo(existingPiece, previousCell);
        if (alreadyConnectedToPrevious)
            return true;

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
        if (piece == null)
            return false;

        if (piece.cell + Vector2Int.up == otherCell) return piece.up;
        if (piece.cell + Vector2Int.down == otherCell) return piece.down;
        if (piece.cell + Vector2Int.left == otherCell) return piece.left;
        if (piece.cell + Vector2Int.right == otherCell) return piece.right;

        return false;
    }

    private void ConnectOrCreateRail(Vector2Int fromCell, Vector2Int toCell)
    {
        if (!IsAdjacent(fromCell, toCell))
        {
            Debug.LogWarning($"[Rail] Connect failed. {fromCell} and {toCell} are not adjacent.");
            return;
        }

        RailPiece fromPiece = EnsureRailExists(fromCell);
        RailPiece toPiece = EnsureRailExists(toCell);

        if (fromPiece == null || toPiece == null)
            return;

        if (!CanAddConnection(fromPiece, toCell) || !CanAddConnection(toPiece, fromCell))
        {
            Debug.LogWarning($"[Rail] Connect failed. Connection limit reached. from={fromCell}, to={toCell}");
            return;
        }

        SetConnection(fromPiece, toCell, true);
        SetConnection(toPiece, fromCell, true);

        RefreshRail(fromCell);
        RefreshRail(toCell);
    }

    private RailPiece EnsureRailExists(Vector2Int cell)
    {
        if (railMap.TryGetValue(cell, out RailPiece existingPiece))
            return existingPiece;

        RailPiece newPiece = CreateRailPiece(cell);
        railMap.Add(cell, newPiece);
        RefreshRail(cell);
        return newPiece;
    }

    private bool CanAddConnection(RailPiece piece, Vector2Int otherCell)
    {
        if (piece == null)
            return false;

        if (IsConnectedTo(piece, otherCell))
            return true;

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

    private bool TryGetPortUnderMouse(out BuildPort port)
    {
        port = null;

        Ray ray = owner.mainCam.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, portMask))
            return false;

        port = hit.collider.GetComponentInParent<BuildPort>();

        if (port != null)
            Debug.Log("[Rail] Hover Port: " + port.name);

        return port != null;
    }

    private bool TryGetMouseCell(out Vector2Int cell)
    {
        cell = default;

        Ray ray = owner.mainCam.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, groundMask))
            return false;

        Vector3 origin = gridOrigin != null ? gridOrigin.position : Vector3.zero;
        Vector3 local = hit.point - origin;

        int x = Mathf.FloorToInt(local.x / cellSize);
        int z = Mathf.FloorToInt(local.z / cellSize);

        cell = new Vector2Int(x, z);
        return true;
    }

    private bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        Vector2Int delta = b - a;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1;
    }

    private RailPiece CreateRailPiece(Vector2Int cell)
    {
        Vector3 worldPos = CellToWorld(cell);

        GameObject root = new GameObject($"Rail_{cell.x}_{cell.y}");
        root.transform.SetParent(railParent);
        root.transform.position = worldPos;

        RailPiece piece = root.AddComponent<RailPiece>();
        piece.cell = cell;
        return piece;
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        Vector3 origin = gridOrigin != null ? gridOrigin.position : Vector3.zero;

        float x = origin.x + (cell.x + 0.5f) * cellSize;
        float z = origin.z + (cell.y + 0.5f) * cellSize;

        return new Vector3(x, fixedY, z);
    }

    private void RefreshRail(Vector2Int cell)
    {
        if (!railMap.TryGetValue(cell, out RailPiece piece))
            return;

        piece.ApplyVisual(straightPrefab, cornerPrefab);
    }

    private void CancelCurrentRoute()
    {
        CancelCurrentRouteStateOnly();
        Debug.Log("[Rail] Route canceled");
    }

    private void CancelCurrentRouteStateOnly()
    {
        isRouting = false;
        startPort = null;
        endPort = null;
        currentEndCell = default;
        currentPathCells.Clear();
    }

    private void ShowPreview()
    {
        if (previewInstance != null)
            return;

        GameObject source = previewPrefab != null ? previewPrefab : straightPrefab;
        if (source == null)
            return;

        previewInstance = Instantiate(source);
        previewInstance.name = "RailPreview";

        Collider[] colliders = previewInstance.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

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

        if (previewInstance == null)
            return;

        if (TryGetPortUnderMouse(out BuildPort port) && port != null)
        {
            Vector2Int frontCell = port.GetFrontCell();
            previewInstance.SetActive(true);
            previewInstance.transform.position = CellToWorld(frontCell);
            previewInstance.transform.rotation = Quaternion.identity;

            bool valid;

            if (!isRouting)
            {
                valid = port.CanStartConnection() && CanUseCellAsRail(frontCell, default, allowExisting: true);
            }
            else
            {
                valid = CanFinishByPlacingNextCell(port);
            }

            ApplyPreviewMaterial(valid);
            return;
        }

        if (!TryGetMouseCell(out Vector2Int cell))
        {
            previewInstance.SetActive(false);
            return;
        }

        previewInstance.SetActive(true);
        previewInstance.transform.position = CellToWorld(cell);
        previewInstance.transform.rotation = Quaternion.identity;

        RouteEvalResult result = EvaluateCellCandidate(cell, out _);
        bool canPlace = result != RouteEvalResult.Invalid;

        ApplyPreviewMaterial(canPlace);
    }

    private void ApplyPreviewMaterial(bool valid)
    {
        if (previewRenderers == null)
            return;

        Material targetMat = valid ? previewValidMaterial : previewInvalidMaterial;
        if (targetMat == null)
            return;

        for (int i = 0; i < previewRenderers.Length; i++)
        {
            Material[] mats = previewRenderers[i].materials;
            for (int j = 0; j < mats.Length; j++)
                mats[j] = targetMat;

            previewRenderers[i].materials = mats;
        }
    }
}