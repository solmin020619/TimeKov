using System.Collections.Generic;
using UnityEngine;

public class BuildManager : MonoBehaviour
{
    [System.Serializable]
    public class BuildSlot
    {
        [Header("DataStore.FacilityById¿¡ ÀÖ´Â facilityId")]
        public int facilityId;
    }

    [Header("Demolish")]
    public LayerMask placedBuildingMask;

    private bool isDemolishMode = false;
    private PlacedBuilding currentHoveredBuilding;

    [Header("References")]
    public Camera mainCam;
    public PlayerBuildZoneChecker zoneChecker;
    public Transform buildParent;
    public FacilityPrefabDatabase prefabDatabase;

    [Header("Build Slots (1~5 keys)")]
    public BuildSlot[] buildSlots;

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
    public float checkHeight = 0.45f;

    public bool IsBuildMode { get; private set; }

    private int currentIndex = 0;
    private int currentRotationY = 0;

    private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

    private void Start()
    {
        if (!DataStore.IsLoaded)
        {
            Debug.LogWarning("[BuildManager] DataStore is not loaded. Make sure DataBoot runs before BuildManager.");
        }

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
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetCurrentSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetCurrentSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetCurrentSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SetCurrentSlot(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SetCurrentSlot(4);
    }

    private void HandleRotateInput()
    {
        if (!CanCurrentFacilityRotate())
            return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            currentRotationY += 90;

            if (currentRotationY >= 360)
                currentRotationY = 0;
        }
    }

    private void HandleBuild()
    {
        if (mainCam == null || buildSlots == null || buildSlots.Length == 0)
            return;

        FacilityRow currentFacility = GetCurrentFacilityRow();
        GameObject currentPrefab = GetCurrentFacilityPrefab();

        if (currentFacility == null || currentPrefab == null)
        {
            SetPreviewActive(false);
            return;
        }

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, groundMask))
        {
            SetPreviewActive(false);
            return;
        }

        Vector3 snappedPos = SnapToGrid(hit.point);
        Quaternion rotation = Quaternion.Euler(0f, currentRotationY, 0f);

        Vector2Int rotatedSize = GetRotatedSize(GetCurrentFacilitySize(), currentRotationY);
        List<Vector2Int> footprintCells = GetFootprintCells(snappedPos, rotatedSize);

        bool isInBuildZone = zoneChecker != null && zoneChecker.IsInBuildZone;
        bool isCorrectHeight = Mathf.Abs(hit.point.y - fixedY) <= yTolerance;
        bool isOccupied = IsAnyCellOccupied(footprintCells);
        bool isBlocked = IsBlockedByPhysics(snappedPos, rotatedSize, rotation);
        bool installRuleOk = CheckInstallRule(currentFacility);

        bool canBuild = isInBuildZone && isCorrectHeight && !isOccupied && !isBlocked && installRuleOk;

        UpdatePreview(snappedPos, rotation, canBuild);

        if (Input.GetMouseButtonDown(0) && canBuild)
        {
            PlaceCurrentFacility(snappedPos, rotation);
        }
    }

    private void SetCurrentSlot(int index)
    {
        if (buildSlots == null || index < 0 || index >= buildSlots.Length)
            return;

        if (DataStore.GetFacility(buildSlots[index].facilityId) == null)
        {
            Debug.LogWarning($"[BuildManager] Invalid facilityId in slot index={index}, facilityId={buildSlots[index].facilityId}");
            return;
        }

        if (prefabDatabase == null || prefabDatabase.GetPrefab(buildSlots[index].facilityId) == null)
        {
            Debug.LogWarning($"[BuildManager] Missing prefab mapping for facilityId={buildSlots[index].facilityId}");
            return;
        }

        currentIndex = index;
        currentRotationY = 0;
        RefreshPreviewMarker();
    }

    private void PlaceCurrentFacility(Vector3 position, Quaternion rotation)
    {
        FacilityRow facility = GetCurrentFacilityRow();
        GameObject prefab = GetCurrentFacilityPrefab();

        if (facility == null || prefab == null)
            return;

        GameObject obj = Instantiate(prefab, position, rotation, buildParent);

        Vector2Int rotatedSize = GetRotatedSize(GetCurrentFacilitySize(), currentRotationY);
        List<Vector2Int> footprintCells = GetFootprintCells(position, rotatedSize);

        OccupyCells(footprintCells);

        PlacedBuilding placedBuilding = obj.GetComponent<PlacedBuilding>();
        if (placedBuilding == null)
            placedBuilding = obj.AddComponent<PlacedBuilding>();

        placedBuilding.facilityId = facility.facilityId;
        placedBuilding.currentLevel = 1;
        placedBuilding.occupiedCells = new List<Vector2Int>(footprintCells);
        placedBuilding.CacheRenderers();

        FacilityInstance facilityInstance = obj.GetComponent<FacilityInstance>();
        if (facilityInstance == null)
            facilityInstance = obj.AddComponent<FacilityInstance>();

        facilityInstance.Initialize(facility.facilityId);
    }

    private Vector3 SnapToGrid(Vector3 worldPos)
    {
        Vector2Int size = GetRotatedSize(GetCurrentFacilitySize(), currentRotationY);

        float x;
        float z;

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

        int baseX = Mathf.FloorToInt(snappedPos.x / cellSize);
        int baseZ = Mathf.FloorToInt(snappedPos.z / cellSize);

        int startX = baseX - (size.x - 1) / 2;
        int startZ = baseZ - (size.y - 1) / 2;

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

    private void RemoveOccupiedCells(List<Vector2Int> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            occupiedCells.Remove(cells[i]);
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

        GameObject prefab = GetCurrentFacilityPrefab();

        if (prefab == null)
            return;

        previewMarker = Instantiate(prefab);
        previewMarker.name = GetCurrentFacilityName() + "_Preview";

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
        return GetCurrentFacilityName();
    }

    public string GetCurrentFacilityName()
    {
        FacilityRow row = GetCurrentFacilityRow();

        if (row == null)
            return "None";

        return row.facilityName;
    }

    private int GetCurrentFacilityId()
    {
        if (buildSlots == null || currentIndex < 0 || currentIndex >= buildSlots.Length)
            return 0;

        return buildSlots[currentIndex].facilityId;
    }

    private FacilityRow GetCurrentFacilityRow()
    {
        int facilityId = GetCurrentFacilityId();

        if (facilityId == 0)
            return null;

        return DataStore.GetFacility(facilityId);
    }

    private GameObject GetCurrentFacilityPrefab()
    {
        if (prefabDatabase == null)
            return null;

        return prefabDatabase.GetPrefab(GetCurrentFacilityId());
    }

    private Vector2Int GetCurrentFacilitySize()
    {
        FacilityRow row = GetCurrentFacilityRow();

        if (row == null)
            return Vector2Int.one;

        return new Vector2Int(row.gridW, row.gridH);
    }

    private bool CanCurrentFacilityRotate()
    {
        FacilityRow row = GetCurrentFacilityRow();

        if (row == null)
            return false;

        return row.canRotate == 1;
    }

    private bool CheckInstallRule(FacilityRow facility)
    {
        if (facility == null)
            return false;

        if (string.IsNullOrWhiteSpace(facility.installRule))
            return true;

        string rule = facility.installRule.Trim().ToLower();

        switch (rule)
        {
            case "any":
            case "default":
            case "ground":
            case "buildzone":
            case "baseonly":
            case "veinonly":
            case "gridonly":
                return true;

            default:
                Debug.LogWarning($"[BuildManager] Unknown installRule={facility.installRule}, facilityId={facility.facilityId}");
                return true;
        }
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
    }

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
    }

    private void SetHoveredBuilding(PlacedBuilding building)
    {
        if (currentHoveredBuilding == building)
            return;

        ClearHoveredBuilding();

        currentHoveredBuilding = building;
        currentHoveredBuilding.SetHighlight(Color.red);
    }

    private void ClearHoveredBuilding()
    {
        if (currentHoveredBuilding != null)
        {
            currentHoveredBuilding.RestoreColor();
            currentHoveredBuilding = null;
        }
    }
}