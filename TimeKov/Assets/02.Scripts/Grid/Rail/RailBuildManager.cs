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
        if (!isRouting)
        {
            if (TryGetPortUnderMouse(out BuildPort clickedPort) && clickedPort != null)
            {
                TryStartRoute(clickedPort);
            }

            return;
        }

        if (TryGetPortUnderMouse(out BuildPort targetPort) && targetPort != null)
        {
            TryFinishRoute(targetPort);
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

        PlaceRailAt(firstCell);
        currentPathCells.Add(firstCell);

        Debug.Log($"[Rail] Route started from {startPort.name}");
    }

    private void TryFinishRoute(BuildPort port)
    {
        if (!isRouting || startPort == null || port == null)
            return;

        if (port == startPort)
            return;

        if (!port.CanEndConnection())
        {
            Debug.Log("[Rail] End port invalid.");
            return;
        }

        if (port.OwnerBuilding != null && startPort.OwnerBuilding != null && port.OwnerBuilding == startPort.OwnerBuilding)
        {
            Debug.Log("[Rail] Cannot connect to same building.");
            return;
        }

        Vector2Int targetFrontCell = port.GetFrontCell();

        if (!IsAdjacent(currentEndCell, targetFrontCell) && currentEndCell != targetFrontCell)
        {
            Debug.Log("[Rail] End port is not adjacent to current rail end.");
            return;
        }

        if (currentEndCell != targetFrontCell)
        {
            if (!CanUseCellAsRail(targetFrontCell, currentEndCell, allowExisting: true))
            {
                Debug.Log("[Rail] End port front cell cannot be used.");
                return;
            }

            PlaceRailAt(targetFrontCell);

            if (!currentPathCells.Contains(targetFrontCell))
                currentPathCells.Add(targetFrontCell);

            currentEndCell = targetFrontCell;
        }

        startPort.AddConnection();
        port.AddConnection();
        endPort = port;

        Debug.Log($"[Rail] Route completed: {startPort.name} -> {endPort.name}");

        CancelCurrentRouteStateOnly();
    }

    private void TryPlaceNextStep(Vector2Int cell)
    {
        if (!isRouting)
            return;

        if (cell == currentEndCell)
            return;

        if (!IsAdjacent(currentEndCell, cell))
        {
            Debug.Log("[Rail] Only adjacent cells can be connected.");
            return;
        }

        if (!CanUseCellAsRail(cell, currentEndCell, allowExisting: true))
        {
            Debug.Log("[Rail] This cell cannot be used.");
            return;
        }

        PlaceRailAt(cell);

        if (!currentPathCells.Contains(cell))
            currentPathCells.Add(cell);

        currentEndCell = cell;
    }

    private bool CanUseCellAsRail(Vector2Int cell, Vector2Int previousCell, bool allowExisting)
    {
        if (railMap.TryGetValue(cell, out RailPiece existingPiece))
        {
            int existingConnections = GetConnectionCount(existingPiece);

            if (!allowExisting)
                return false;

            // 기존 칸 사용은 가능하지만 2연결 초과는 막음
            if (previousCell != cell)
            {
                bool alreadyConnectedToPrevious = IsConnectedTo(existingPiece, previousCell);
                if (!alreadyConnectedToPrevious && existingConnections >= 2)
                    return false;
            }

            return true;
        }

        int futureConnections = 0;

        if (cell + Vector2Int.up == previousCell || HasRail(cell + Vector2Int.up)) futureConnections++;
        if (cell + Vector2Int.down == previousCell || HasRail(cell + Vector2Int.down)) futureConnections++;
        if (cell + Vector2Int.left == previousCell || HasRail(cell + Vector2Int.left)) futureConnections++;
        if (cell + Vector2Int.right == previousCell || HasRail(cell + Vector2Int.right)) futureConnections++;

        return futureConnections <= 2;
    }

    private bool HasRail(Vector2Int cell)
    {
        return railMap.ContainsKey(cell);
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

    private void PlaceRailAt(Vector2Int cell)
    {
        if (!railMap.ContainsKey(cell))
        {
            RailPiece piece = CreateRailPiece(cell);
            railMap.Add(cell, piece);
        }

        RefreshWithNeighbors(cell);
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

    private void RefreshWithNeighbors(Vector2Int cell)
    {
        RefreshRail(cell);
        RefreshRail(cell + Vector2Int.up);
        RefreshRail(cell + Vector2Int.down);
        RefreshRail(cell + Vector2Int.left);
        RefreshRail(cell + Vector2Int.right);
    }

    private void RefreshRail(Vector2Int cell)
    {
        if (!railMap.TryGetValue(cell, out RailPiece piece))
            return;

        piece.up = railMap.ContainsKey(cell + Vector2Int.up);
        piece.down = railMap.ContainsKey(cell + Vector2Int.down);
        piece.left = railMap.ContainsKey(cell + Vector2Int.left);
        piece.right = railMap.ContainsKey(cell + Vector2Int.right);

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
                valid = port.CanStartConnection();
            else
                valid = port != startPort &&
                        port.CanEndConnection() &&
                        IsAdjacent(currentEndCell, frontCell);

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

        bool canPlace = isRouting &&
                        IsAdjacent(currentEndCell, cell) &&
                        CanUseCellAsRail(cell, currentEndCell, allowExisting: true);

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