using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class BuildManager : MonoBehaviour
{
    public enum BuildSubMode
    {
        Facility,
        Rail
    }

    [System.Serializable]
    public class BuildSlot
    {
        public int facilityId;
    }

    [Header("Top View")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Camera topViewCamera;
    [SerializeField] private TopViewPanCamera topViewPanCamera;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Transform topViewStartTarget;
    [SerializeField] private Vector3 topViewStartOffset = new Vector3(0f, 25f, 0f);
    [SerializeField] private MonoBehaviour[] disableInTopView;

    [Header("Build Effect")]
    public Material hologramMaterial;
    public float buildEffectDuration = 1.2f;

    [Header("Build Audio")]
    public AudioSource audioSource;
    public AudioClip buildStartClip;
    public AudioClip buildCompleteClip;

    [Header("Build VFX")]
    public GameObject buildCompleteEffectPrefab;
    public Vector3 buildCompleteEffectOffset = Vector3.zero;

    [Header("Demolish")]
    public LayerMask placedBuildingMask;

    private bool isDemolishMode = false;
    private PlacedBuilding currentHoveredBuilding;

    [Header("References")]
    public Camera mainCam;
    public PlayerBuildZoneChecker zoneChecker;
    public Transform buildParent;
    public FacilityPrefabDatabase prefabDatabase;

    [Header("Rail")]
    [SerializeField] private RailBuildManager railBuildManager;

    public BuildSubMode CurrentSubMode { get; private set; } = BuildSubMode.Facility;
    public bool IsRailSubMode => IsBuildMode && CurrentSubMode == BuildSubMode.Rail;
    public int CurrentSlotIndex => currentIndex;

    [Header("Build Slots (1~9 keys)")]
    public BuildSlot[] buildSlots;

    [Header("Preview")]
    public GameObject previewMarker;

    [Header("Raycast")]
    public LayerMask groundMask;
    public float rayDistance = 300f;

    [Header("Grid")]
    public Transform gridOrigin;
    public float cellSize = 1f;
    public float fixedY = 0f;
    public float yTolerance = 0.1f;

    [Header("Build Check")]
    public LayerMask blockingMask;
    public float checkHeight = 0.45f;

    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private CharacterController playerCharacterController;
    [SerializeField] private MonoBehaviour playerMovementScript;
    [SerializeField] private Animator playerAnimator;

    public bool IsBuildMode { get; private set; }
    public bool IsTopViewMode { get; private set; }

    private int currentIndex = -1;          // -1 = 미선택 상태
    private bool hasSelectedSlot = false;   // 슬롯이 선택됐는지 여부
    private int currentRotationY = 0;

    private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

    private bool isDragBuilding = false;
    private readonly HashSet<Vector2Int> dragPlacedStartCells = new HashSet<Vector2Int>();

    private void Start()
    {
        if (!DataStore.IsLoaded)
        {
            Debug.LogWarning("[BuildManager] DataStore is not loaded. Make sure DataBoot runs before BuildManager.");
        }

        if (previewMarker != null)
            previewMarker.SetActive(false);

        SetTopViewMode(false, true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (railBuildManager != null)
            railBuildManager.EndRailMode();
    }

    private void Update()
    {
        HandleModeInput();

        if (!IsBuildMode)
            return;

        HandleSelectionInput();
        HandleDemolishModeInput();

        if (isDemolishMode)
        {
            HandleDemolish();
            return;
        }

        if (IsRailSubMode)
        {
            railBuildManager?.TickRailMode();
            return;
        }

        HandleRotateInput();
        HandleBuild();
    }

    private void HandleSelectionInput()
    {
        if (!IsBuildMode)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            SelectRailMode();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectFacilitySlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectFacilitySlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectFacilitySlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectFacilitySlot(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SelectFacilitySlot(4);
        if (Input.GetKeyDown(KeyCode.Alpha6)) SelectFacilitySlot(5);
        if (Input.GetKeyDown(KeyCode.Alpha7)) SelectFacilitySlot(6);
        if (Input.GetKeyDown(KeyCode.Alpha8)) SelectFacilitySlot(7);
        if (Input.GetKeyDown(KeyCode.Alpha9)) SelectFacilitySlot(8);
    }

    private void SelectRailMode()
    {
        SetSubMode(BuildSubMode.Rail);
    }

    private void SelectFacilitySlot(int index)
    {
        // 같은 슬롯 다시 누르면 선택 해제
        if (hasSelectedSlot && currentIndex == index)
        {
            hasSelectedSlot = false;
            currentIndex = -1;

            if (CurrentSubMode != BuildSubMode.Facility)
                CurrentSubMode = BuildSubMode.Facility;

            if (previewMarker != null)
                previewMarker.SetActive(false);

            return;
        }

        // 레일 모드였으면 종료
        if (CurrentSubMode == BuildSubMode.Rail)
        {
            railBuildManager?.EndRailMode();
            CurrentSubMode = BuildSubMode.Facility;
        }

        hasSelectedSlot = true;
        SetCurrentSlot(index);
    }

    public void SetSubMode(BuildSubMode mode)
    {
        if (CurrentSubMode == mode)
            return;

        CurrentSubMode = mode;

        if (mode == BuildSubMode.Rail)
        {
            isDemolishMode = false;
            isDragBuilding = false;
            dragPlacedStartCells.Clear();

            ClearHoveredBuilding();
            SetPreviewActive(false);

            railBuildManager?.BeginRailMode(this);
        }
        else
        {
            railBuildManager?.EndRailMode();

            currentRotationY = 0;

            // 선택된 슬롯이 있을 때만 프리뷰 갱신
            if (hasSelectedSlot)
                RefreshPreviewMarker();
            else if (previewMarker != null)
                previewMarker.SetActive(false);
        }
    }

    private void HandleModeInput()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            IsBuildMode = !IsBuildMode;

            if (!IsBuildMode)
            {
                // 건축 모드 종료
                isDemolishMode = false;
                isDragBuilding = false;
                dragPlacedStartCells.Clear();
                hasSelectedSlot = false;
                currentIndex = -1;

                ClearHoveredBuilding();
                SetTopViewMode(false);

                railBuildManager?.EndRailMode();
                CurrentSubMode = BuildSubMode.Facility;

                if (previewMarker != null)
                    previewMarker.SetActive(false);

                UIStateManager.Instance?.CloseAllUI();
            }
            else
            {
                // 건축 모드 진입 — 슬롯은 자동 선택 안 함
                hasSelectedSlot = false;
                currentIndex = -1;
                CurrentSubMode = BuildSubMode.Facility;

                if (previewMarker != null)
                    previewMarker.SetActive(false);

                // B키 진입 시 탑뷰 자동 활성화
                SetTopViewMode(true);

                UIStateManager.Instance?.SetState(UIStateManager.UIState.Build);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            if (!IsBuildMode)
                return;

            IsBuildMode = false;
            isDemolishMode = false;
            isDragBuilding = false;
            dragPlacedStartCells.Clear();
            hasSelectedSlot = false;
            currentIndex = -1;

            ClearHoveredBuilding();
            SetTopViewMode(false);

            railBuildManager?.EndRailMode();
            CurrentSubMode = BuildSubMode.Facility;

            if (previewMarker != null)
                previewMarker.SetActive(false);

            UIStateManager.Instance?.CloseAllUI();
        }
    }

    private void SetTopViewMode(bool value, bool force = false)
    {
        if (!force && IsTopViewMode == value)
            return;

        IsTopViewMode = value;

        if (value)
        {
            StopPlayerImmediately();
        }

        if (gameplayCamera != null)
            gameplayCamera.gameObject.SetActive(!value);

        if (topViewCamera != null)
            topViewCamera.gameObject.SetActive(value);

        if (playerInput != null)
            playerInput.enabled = !value;

        if (disableInTopView != null)
        {
            for (int i = 0; i < disableInTopView.Length; i++)
            {
                if (disableInTopView[i] != null)
                    disableInTopView[i].enabled = !value;
            }
        }

        if (topViewPanCamera != null)
            topViewPanCamera.SetControlEnabled(value);

        if (value)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (topViewCamera != null)
            {
                Vector3 startPos = topViewCamera.transform.position;

                if (topViewStartTarget != null)
                {
                    startPos = topViewStartTarget.position + topViewStartOffset;
                }

                topViewCamera.transform.position = startPos;
                topViewCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        ResolveActiveBuildCamera();
    }

    private void ResolveActiveBuildCamera()
    {
        if (IsTopViewMode && topViewCamera != null)
            mainCam = topViewCamera;
        else if (gameplayCamera != null)
            mainCam = gameplayCamera;
    }

    private void HandleRotateInput()
    {
        if (IsRailSubMode)
            return;

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
        if (IsRailSubMode)
            return;

        if (!TryGetCurrentBuildData(
            out RaycastHit hit,
            out Vector2Int startCell,
            out Vector3 snappedPos,
            out Quaternion rotation,
            out Vector2Int rotatedSize,
            out List<Vector2Int> footprintCells,
            out bool canBuild))
        {
            if (Input.GetMouseButtonUp(0))
            {
                isDragBuilding = false;
                dragPlacedStartCells.Clear();
            }

            return;
        }

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            isDragBuilding = true;
            dragPlacedStartCells.Clear();

            TryDragPlace(startCell, snappedPos, rotation, footprintCells, canBuild);
        }

        if (Input.GetMouseButton(0) && isDragBuilding)
        {
            TryDragPlace(startCell, snappedPos, rotation, footprintCells, canBuild);
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragBuilding = false;
            dragPlacedStartCells.Clear();
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

    private IEnumerator PlaceCurrentFacilityRoutine(Vector3 position, Quaternion rotation, List<Vector2Int> footprintCells)
    {
        FacilityRow facility = GetCurrentFacilityRow();
        GameObject prefab = GetCurrentFacilityPrefab();

        if (facility == null || prefab == null)
            yield break;

        OccupyCells(footprintCells);

        PlayBuildStartSound();

        GameObject hologramObj = Instantiate(prefab, position, rotation);
        ApplyHologramVisual(hologramObj);

        Collider[] hologramColliders = hologramObj.GetComponentsInChildren<Collider>();
        for (int i = 0; i < hologramColliders.Length; i++)
            hologramColliders[i].enabled = false;

        Rigidbody[] rigidbodies = hologramObj.GetComponentsInChildren<Rigidbody>();
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
        }

        MonoBehaviour[] behaviours = hologramObj.GetComponentsInChildren<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != this)
                behaviours[i].enabled = false;
        }

        yield return new WaitForSeconds(buildEffectDuration);

        if (hologramObj != null)
            Destroy(hologramObj);

        GameObject obj = Instantiate(prefab, position, rotation, buildParent);

        PlacedBuilding placedBuilding = obj.GetComponent<PlacedBuilding>();
        if (placedBuilding == null)
            placedBuilding = obj.AddComponent<PlacedBuilding>();

        placedBuilding.facilityId = facility.facilityId;
        placedBuilding.currentLevel = 1;
        placedBuilding.occupiedCells = new List<Vector2Int>(footprintCells);
        placedBuilding.originCell = footprintCells[0];
        placedBuilding.CacheRenderers();

        FacilityInstance facilityInstance = obj.GetComponent<FacilityInstance>();
        if (facilityInstance == null)
            facilityInstance = obj.AddComponent<FacilityInstance>();

        facilityInstance.Initialize(facility.facilityId);

        PlayBuildCompleteSound();
        SpawnBuildCompleteEffect(position, rotation);
    }

    private void ApplyHologramVisual(GameObject target)
    {
        if (target == null || hologramMaterial == null)
            return;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;

            for (int j = 0; j < mats.Length; j++)
                mats[j] = hologramMaterial;

            renderers[i].materials = mats;
        }
    }

    private Vector3 SnapToGrid(Vector3 worldPos)
    {
        Vector2Int size = GetRotatedSize(GetCurrentFacilitySize(), currentRotationY);
        Vector2Int startCell = WorldToStartCell(worldPos);
        return StartCellToWorldCenter(startCell, size);
    }

    private Vector2Int WorldToStartCell(Vector3 worldPos)
    {
        Vector3 origin = gridOrigin != null ? gridOrigin.position : Vector3.zero;
        Vector3 local = worldPos - origin;

        int cellX = Mathf.FloorToInt(local.x / cellSize);
        int cellZ = Mathf.FloorToInt(local.z / cellSize);

        return new Vector2Int(cellX, cellZ);
    }

    private Vector3 StartCellToWorldCenter(Vector2Int startCell, Vector2Int size)
    {
        Vector3 origin = gridOrigin != null ? gridOrigin.position : Vector3.zero;

        float centerX = origin.x + (startCell.x + size.x * 0.5f) * cellSize;
        float centerZ = origin.z + (startCell.y + size.y * 0.5f) * cellSize;

        return new Vector3(centerX, fixedY, centerZ);
    }

    private List<Vector2Int> GetFootprintCellsFromStartCell(Vector2Int startCell, Vector2Int size)
    {
        List<Vector2Int> cells = new List<Vector2Int>();

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                cells.Add(new Vector2Int(startCell.x + x, startCell.y + z));
            }
        }

        return cells;
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
        Vector3 origin = gridOrigin != null ? gridOrigin.position : Vector3.zero;

        float localCenterX = snappedPos.x - origin.x;
        float localCenterZ = snappedPos.z - origin.z;

        int startX = Mathf.RoundToInt((localCenterX / cellSize) - (size.x * 0.5f));
        int startZ = Mathf.RoundToInt((localCenterZ / cellSize) - (size.y * 0.5f));

        return GetFootprintCellsFromStartCell(new Vector2Int(startX, startZ), size);
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
            occupiedCells.Add(cells[i]);
    }

    private void RemoveOccupiedCells(List<Vector2Int> cells)
    {
        for (int i = 0; i < cells.Count; i++)
            occupiedCells.Remove(cells[i]);
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
            colliders[i].enabled = false;

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
        return IsRailSubMode ? "Rail" : GetCurrentFacilityName();
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
        if (!hasSelectedSlot) return 0;

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

                if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
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

    private void PlayBuildStartSound()
    {
        if (audioSource == null || buildStartClip == null)
            return;

        audioSource.PlayOneShot(buildStartClip);
    }

    private void PlayBuildCompleteSound()
    {
        if (audioSource == null || buildCompleteClip == null)
            return;

        audioSource.PlayOneShot(buildCompleteClip);
    }

    private void SpawnBuildCompleteEffect(Vector3 position, Quaternion rotation)
    {
        if (buildCompleteEffectPrefab == null)
            return;

        Instantiate(
            buildCompleteEffectPrefab,
            position + buildCompleteEffectOffset,
            rotation
        );
    }

    private bool TryGetCurrentBuildData(
        out RaycastHit hit,
        out Vector2Int startCell,
        out Vector3 snappedPos,
        out Quaternion rotation,
        out Vector2Int rotatedSize,
        out List<Vector2Int> footprintCells,
        out bool canBuild)
    {
        hit = default;
        startCell = default;
        snappedPos = default;
        rotation = Quaternion.identity;
        rotatedSize = default;
        footprintCells = null;
        canBuild = false;

        if (mainCam == null || buildSlots == null || buildSlots.Length == 0)
            return false;

        FacilityRow currentFacility = GetCurrentFacilityRow();
        GameObject currentPrefab = GetCurrentFacilityPrefab();

        if (currentFacility == null || currentPrefab == null)
        {
            SetPreviewActive(false);
            return false;
        }

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out hit, rayDistance, groundMask))
        {
            SetPreviewActive(false);
            return false;
        }

        rotation = Quaternion.Euler(0f, currentRotationY, 0f);
        rotatedSize = GetRotatedSize(GetCurrentFacilitySize(), currentRotationY);

        startCell = WorldToStartCell(hit.point);
        snappedPos = StartCellToWorldCenter(startCell, rotatedSize);
        footprintCells = GetFootprintCellsFromStartCell(startCell, rotatedSize);

        bool isInBuildZone = zoneChecker != null && zoneChecker.IsInBuildZone;

        bool isOccupied = IsAnyCellOccupied(footprintCells);

        bool isBlocked = IsBlockedByPhysics(snappedPos, rotatedSize, rotation);
        bool installRuleOk = CheckInstallRule(currentFacility);

        canBuild = isInBuildZone && !isOccupied && !isBlocked && installRuleOk;

        UpdatePreview(snappedPos, rotation, canBuild);
        return true;
    }

    private void TryDragPlace(Vector2Int startCell, Vector3 snappedPos, Quaternion rotation, List<Vector2Int> footprintCells, bool canBuild)
    {
        if (!canBuild)
            return;

        if (dragPlacedStartCells.Contains(startCell))
            return;

        dragPlacedStartCells.Add(startCell);
        StartCoroutine(PlaceCurrentFacilityRoutine(snappedPos, rotation, footprintCells));
    }

    private void StopPlayerImmediately()
    {
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        if (playerMovementScript != null)
        {
            playerMovementScript.SendMessage("ResetInputState", SendMessageOptions.DontRequireReceiver);
            playerMovementScript.SendMessage("StopImmediately", SendMessageOptions.DontRequireReceiver);
        }

        if (playerAnimator != null)
        {
            playerAnimator.SetFloat("MoveSpeed", 0f);
            playerAnimator.SetFloat("InputX", 0f);
            playerAnimator.SetFloat("InputY", 0f);
        }
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}