using System.Collections.Generic;
using UnityEngine;

public class BuildManager : MonoBehaviour
{
    [System.Serializable]
    public class BuildItem
    {
        public string itemName;
        public GameObject prefab;

        [Header("Grid Footprint")]
        public Vector2Int size = Vector2Int.one; // x,z 기준 몇 칸 차지하는지
    }

    [Header("Demolish")] // 건축 해제관련
    public LayerMask placedBuildingMask;

    private bool isDemolishMode = false;
    private PlacedBuilding currentHoveredBuilding;

    [Header("References")]
    public Camera mainCam;
    public PlayerBuildZoneChecker zoneChecker;
    public Transform buildParent;

    [Header("Build List (1~5 keys)")]
    public BuildItem[] buildItems;

    [Header("Preview")]
    public GameObject previewMarker;

    [Header("Raycast")]
    public LayerMask groundMask;
    public float rayDistance = 300f;

    [Header("Grid")]
    public float cellSize = 1f;
    public float fixedY = 0f;
    public float yTolerance = 0.1f;

    [Header("Build Check")]
    public LayerMask blockingMask;
    public float checkHeight = 0.45f; // 높이만 따로 사용

    public bool IsBuildMode { get; private set; }

    private int currentIndex = 0;
    private int currentRotationY = 0;

    private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

    private void Start()
    {
        RefreshPreviewMarker();

        if (previewMarker != null)
            previewMarker.SetActive(false);
    }

    private void Update()
    {
        HandleModeInput();

        if (!IsBuildMode)
            return;

        HandleSelectInput();
        HandleRotateInput();
        HandleDemolishModeInput();

        if (isDemolishMode)
            HandleDemolish();
        else
            HandleBuild();
    }

    private void HandleModeInput()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            IsBuildMode = !IsBuildMode;


            if (!IsBuildMode)
            {
                isDemolishMode = false;
                ClearHoveredBuilding();

                if (previewMarker != null)
                    previewMarker.SetActive(false);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            IsBuildMode = false;
            isDemolishMode = false;
            ClearHoveredBuilding();

            if (previewMarker != null)
                previewMarker.SetActive(false);
        }
    }

    private void HandleSelectInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetCurrentItem(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetCurrentItem(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetCurrentItem(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SetCurrentItem(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SetCurrentItem(4);
    }

    private void HandleRotateInput()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            currentRotationY += 90;
            if (currentRotationY >= 360)
                currentRotationY = 0;
        }
    }

    private void HandleBuild()
    {
        if (mainCam == null || buildItems == null || buildItems.Length == 0)
            return;

        if (buildItems[currentIndex] == null || buildItems[currentIndex].prefab == null)
            return;

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, groundMask))
        {
            SetPreviewActive(false);
            return;
        }

        Vector3 snappedPos = SnapToGrid(hit.point);
        Quaternion rotation = Quaternion.Euler(0f, currentRotationY, 0f);

        BuildItem currentItem = buildItems[currentIndex];
        Vector2Int rotatedSize = GetRotatedSize(currentItem.size, currentRotationY);
        List<Vector2Int> footprintCells = GetFootprintCells(snappedPos, rotatedSize);

        bool isInBuildZone = zoneChecker != null && zoneChecker.IsInBuildZone;
        bool isCorrectHeight = Mathf.Abs(hit.point.y - fixedY) <= yTolerance;
        bool isOccupied = IsAnyCellOccupied(footprintCells);
        bool isBlocked = IsBlockedByPhysics(snappedPos, rotatedSize, rotation);

        bool canBuild = isInBuildZone && isCorrectHeight && !isOccupied && !isBlocked;

        UpdatePreview(snappedPos, rotation, canBuild);

        if (Input.GetMouseButtonDown(0) && canBuild)
        {
            PlaceCurrentItem(snappedPos, rotation);
        }
    }

    private void SetCurrentItem(int index)
    {
        if (buildItems == null || index < 0 || index >= buildItems.Length)
            return;

        if (buildItems[index] == null || buildItems[index].prefab == null)
            return;

        currentIndex = index;
        RefreshPreviewMarker();
    }

    private void PlaceCurrentItem(Vector3 position, Quaternion rotation)
    {
        BuildItem currentItem = buildItems[currentIndex];
        GameObject prefab = currentItem.prefab;

        if (prefab == null)
            return;

        GameObject obj = Instantiate(prefab, position, rotation, buildParent);

        Vector2Int rotatedSize = GetRotatedSize(currentItem.size, currentRotationY);
        List<Vector2Int> footprintCells = GetFootprintCells(position, rotatedSize);

        OccupyCells(footprintCells);

        PlacedBuilding placedBuilding = obj.GetComponent<PlacedBuilding>();
        if (placedBuilding == null)
            placedBuilding = obj.AddComponent<PlacedBuilding>();

        placedBuilding.occupiedCells = new List<Vector2Int>(footprintCells);
        placedBuilding.CacheRenderers();
    }//해제관련 스크립트 추가

    private Vector3 SnapToGrid(Vector3 worldPos)
    {
        if (buildItems == null || buildItems.Length == 0 || buildItems[currentIndex] == null)
            return worldPos;

        Vector2Int size = GetRotatedSize(buildItems[currentIndex].size, currentRotationY);

        float x;
        float z;

        // 홀수 크기면 셀 중심에 맞춤
        if (size.x % 2 == 1)
            x = Mathf.Floor(worldPos.x / cellSize) * cellSize + cellSize * 0.5f;
        else
            x = Mathf.Round(worldPos.x / cellSize) * cellSize;

        if (size.y % 2 == 1)
            z = Mathf.Floor(worldPos.z / cellSize) * cellSize + cellSize * 0.5f;
        else
            z = Mathf.Round(worldPos.z / cellSize) * cellSize;

        return new Vector3(x, fixedY, z);
    }
    private Vector2Int WorldToCell(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / cellSize);
        int z = Mathf.FloorToInt(worldPos.z / cellSize);
        return new Vector2Int(x, z);
    }

    private Vector2Int GetRotatedSize(Vector2Int originalSize, int rotationY)
    {
        rotationY %= 360;

        if (rotationY == 90 || rotationY == 270)
            return new Vector2Int(originalSize.y, originalSize.x);

        return originalSize;
    }

    private List<Vector2Int> GetFootprintCells(Vector3 snappedPos, Vector2Int size)
    {
        List<Vector2Int> cells = new List<Vector2Int>();

        // snappedPos는 현재 "한 칸 중심" 기준으로 잡혀 있으니
        // 여기서 설치 기준 셀을 먼저 구한다.
        int baseX = Mathf.FloorToInt(snappedPos.x / cellSize);
        int baseZ = Mathf.FloorToInt(snappedPos.z / cellSize);

        // 홀수 크기면 현재 셀 중심 기준으로 좌우 대칭
        // 짝수 크기면 현재 셀을 좌하단 쪽 기준으로 포함하도록 보정
        int startX = baseX - (size.x - 1) / 2;
        int startZ = baseZ - (size.y - 1) / 2;

        // 짝수 크기는 한 칸 더 왼쪽/아래로 시작해야 셀 밀림이 안 생김
        if (size.x % 2 == 0)
            startX -= 1;

        if (size.y % 2 == 0)
            startZ -= 1;

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                cells.Add(new Vector2Int(startX + x, startZ + z));
            }
        }

        return cells;
    }

    private bool IsAnyCellOccupied(List<Vector2Int> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (occupiedCells.Contains(cells[i]))
                return true;
        }

        return false;
    }

    private void OccupyCells(List<Vector2Int> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            occupiedCells.Add(cells[i]);
        }
    }

    private bool IsBlockedByPhysics(Vector3 centerPos, Vector2Int size, Quaternion rotation)
    {
        if (blockingMask.value == 0)
            return false;

        float margin = 0.05f;

        Vector3 halfExtents = new Vector3(
            (size.x * cellSize * 0.5f) - margin,
            checkHeight,
            (size.y * cellSize * 0.5f) - margin
        );

        Collider[] hits = Physics.OverlapBox(centerPos, halfExtents, rotation, blockingMask);

        for (int i = 0; i < hits.Length; i++)
        {
            if (previewMarker != null && hits[i].transform.IsChildOf(previewMarker.transform))
                continue;

            return true;
        }

        return false;
    }

    private void UpdatePreview(Vector3 position, Quaternion rotation, bool canBuild)
    {
        if (previewMarker == null)
            return;

        previewMarker.SetActive(true);
        previewMarker.transform.position = position;
        previewMarker.transform.rotation = rotation;

        Renderer[] renderers = previewMarker.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_Color"))
                renderers[i].material.color = canBuild ? Color.green : Color.red;
        }
    }

    private void RefreshPreviewMarker()
    {
        if (previewMarker != null)
            Destroy(previewMarker);

        if (buildItems == null || buildItems.Length == 0)
            return;

        if (buildItems[currentIndex] == null || buildItems[currentIndex].prefab == null)
            return;

        previewMarker = Instantiate(buildItems[currentIndex].prefab);
        previewMarker.name = buildItems[currentIndex].itemName + "_Preview";

        Collider[] colliders = previewMarker.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] rigidbodies = previewMarker.GetComponentsInChildren<Rigidbody>();
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
        }

        MonoBehaviour[] behaviours = previewMarker.GetComponentsInChildren<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != this)
                behaviours[i].enabled = false;
        }

        previewMarker.SetActive(false);
    }

    private void SetPreviewActive(bool value)
    {
        if (previewMarker != null)
            previewMarker.SetActive(value);
    }

    public string GetCurrentItemName()
    {
        if (buildItems == null || buildItems.Length == 0)
            return "None";

        if (buildItems[currentIndex] == null)
            return "None";

        return buildItems[currentIndex].itemName;
    }

    private void HandleDemolishModeInput()
    {
        if (Input.GetKeyDown(KeyCode.X))
        {
            isDemolishMode = !isDemolishMode;

            if (isDemolishMode)
            {
                if (previewMarker != null)
                    previewMarker.SetActive(false);
            }
            else
            {
                ClearHoveredBuilding();
            }
        }
    }//해제관련
    private void HandleDemolish()
    {
        if (mainCam == null)
            return;

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, placedBuildingMask))
        {
            PlacedBuilding building = hit.collider.GetComponentInParent<PlacedBuilding>();

            if (building != null)
            {
                SetHoveredBuilding(building);

                if (Input.GetMouseButtonDown(0))
                {
                    RemoveOccupiedCells(building.occupiedCells);
                    ClearHoveredBuilding();
                    Destroy(building.gameObject);
                }

                return;
            }
        }

        ClearHoveredBuilding();
    }//해제관련
    private void SetHoveredBuilding(PlacedBuilding building)
    {
        if (currentHoveredBuilding == building)
            return;

        ClearHoveredBuilding();

        currentHoveredBuilding = building;
        currentHoveredBuilding.SetHighlight(Color.red);
    }//해제관련

    private void ClearHoveredBuilding()
    {
        if (currentHoveredBuilding != null)
        {
            currentHoveredBuilding.RestoreColor();
            currentHoveredBuilding = null;
        }
    }//해제관련
    private void RemoveOccupiedCells(List<Vector2Int> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            occupiedCells.Remove(cells[i]);
        }
    } //해제관련
}