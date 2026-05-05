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
        Rail,
        Blueprint
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
    [Tooltip("레일 오브젝트 레이어. 해제 모드에서 레일도 대상으로 삼을 때 사용.")]
    public LayerMask railMask;

    [Header("Demolish Audio")]
    public AudioClip demolishClip;
    [Range(0f, 1f)] public float demolishVolume = 1f;

    private bool isDemolishMode = false;
    private PlacedBuilding currentHoveredBuilding;

    // 레일 해제용 hover 상태
    private RailPiece hoveredRailPiece;
    private bool hasHoveredRail = false;

    // Shift 드래그 연속 해제 상태
    private bool isDragDemolishing = false;
    private readonly HashSet<PlacedBuilding> dragDemolishedBuildings = new HashSet<PlacedBuilding>();
    private readonly HashSet<Vector2Int> dragDemolishedRailCells = new HashSet<Vector2Int>();

    [Header("References")]
    public Camera mainCam;
    public PlayerBuildZoneChecker zoneChecker;
    public Transform buildParent;
    public FacilityPrefabDatabase prefabDatabase;

    [Header("Rail")]
    [SerializeField] private RailBuildManager railBuildManager;

    [Header("Blueprint")]
    [SerializeField] private BlueprintModeManager blueprintModeManager;

    public BuildSubMode CurrentSubMode { get; private set; } = BuildSubMode.Facility;
    public bool IsRailSubMode => IsBuildMode && CurrentSubMode == BuildSubMode.Rail;
    public bool IsBlueprintSubMode => IsBuildMode && CurrentSubMode == BuildSubMode.Blueprint;
    public int CurrentSlotIndex => currentIndex;

    public RailBuildManager RailManager => railBuildManager;
    public FacilityPrefabDatabase PrefabDatabase => prefabDatabase;
    public Transform BuildParent => buildParent;

    /// <summary>
    /// 현재 화면에 활성화되어 있는 프리뷰(시설 or 레일)의 월드 위치를 돌려준다.
    /// BuildGridOverlay 등 외부 표시용.
    /// </summary>
    public bool TryGetActivePreviewPosition(out Vector3 worldPos)
    {
        if (previewMarker != null && previewMarker.activeSelf)
        {
            worldPos = previewMarker.transform.position;
            return true;
        }

        if (railBuildManager != null && railBuildManager.TryGetPreviewPosition(out worldPos))
            return true;

        if (blueprintModeManager != null && blueprintModeManager.TryGetPointerWorldPosition(out worldPos))
            return true;

        worldPos = default;
        return false;
    }

    /// <summary>
    /// 청사진 유령이 커서를 따라다닐 때 오버레이가 격자 반경을 늘려야 하는 경우 사용.
    /// 해당 상황이 아니면 false.
    /// </summary>
    public bool TryGetOverridePatchRadius(out int radiusCells)
    {
        radiusCells = 0;
        if (blueprintModeManager != null && blueprintModeManager.TryGetBlueprintBoundsRadius(out radiusCells))
            return true;
        return false;
    }

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
        UpdateCameraDragGate();

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

        if (IsBlueprintSubMode)
        {
            blueprintModeManager?.Tick();
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

        if (Input.GetKeyDown(KeyCode.N))
        {
            ToggleBlueprintMode();
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

    private void ToggleBlueprintMode()
    {
        if (blueprintModeManager == null)
        {
            Debug.LogWarning("[BuildManager] BlueprintModeManager가 인스펙터에 연결돼 있지 않음.");
            return;
        }

        SetSubMode(CurrentSubMode == BuildSubMode.Blueprint ? BuildSubMode.Facility : BuildSubMode.Blueprint);
    }

    private void SelectRailMode()
    {
        if (CurrentSubMode == BuildSubMode.Rail)
        {
            // E 다시 → 토글 OFF: Facility 로 가되 슬롯 선택까지 해제 (이전에 1~9 누른 게 있어도 안 뜨게)
            SetSubMode(BuildSubMode.Facility);
            hasSelectedSlot = false;
            currentIndex = -1;
            if (previewMarker != null)
                previewMarker.SetActive(false);
            return;
        }

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

        // 기존 모드 종료 처리
        if (CurrentSubMode == BuildSubMode.Rail)
            railBuildManager?.EndRailMode();
        if (CurrentSubMode == BuildSubMode.Blueprint)
            blueprintModeManager?.Deactivate();

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
        else if (mode == BuildSubMode.Blueprint)
        {
            isDemolishMode = false;
            isDragBuilding = false;
            dragPlacedStartCells.Clear();

            ClearHoveredBuilding();
            SetPreviewActive(false);

            blueprintModeManager?.Activate(this);
        }
        else
        {
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
            isDragDemolishing = false;
            dragDemolishedBuildings.Clear();
            dragDemolishedRailCells.Clear();
            hasSelectedSlot = false;
            currentIndex = -1;

            ClearHoveredBuilding();
            ClearHoveredRail();
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

        // 탑뷰 진입/종료 시 건물 레이블 표시/숨김
        var labels = UnityEngine.Object.FindObjectsByType<BuildingLabelUI>(UnityEngine.FindObjectsSortMode.None);
        foreach (var label in labels)
        {
            if (value) label.ShowLabel();
            else label.HideLabel();
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
        if (facility == null) yield break;
        yield return PlaceFacilityRoutine(facility.facilityId, position, rotation, footprintCells);
    }

    /// <summary>
    /// facilityId를 지정해서 홀로그램 연출을 거쳐 배치한다. Blueprint 붙여넣기에서 호출.
    /// 호출자가 footprintCells 비점유/물리 미충돌을 이미 검증했다고 가정.
    /// </summary>
    public void PlaceFacilityWithHologram(int facilityId, Vector3 position, Quaternion rotation, List<Vector2Int> footprintCells)
    {
        StartCoroutine(PlaceFacilityRoutine(facilityId, position, rotation, footprintCells));
    }

    private IEnumerator PlaceFacilityRoutine(int facilityId, Vector3 position, Quaternion rotation, List<Vector2Int> footprintCells)
    {
        FacilityRow facility = DataStore.GetFacility(facilityId);
        GameObject prefab = prefabDatabase != null ? prefabDatabase.GetPrefab(facilityId) : null;

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

        placedBuilding.SetupLabel(facility.facilityName, facility.gridW, facility.gridH, cellSize);

        if (!IsTopViewMode)
            placedBuilding.HideLabel();

        // 레일 모드 활성 중 새 설비가 생겼다면 포트 인디케이터 새로고침
        if (IsRailSubMode)
            railBuildManager?.RefreshPortIndicators();

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
        Vector2Int startCell = WorldToStartCellCentered(worldPos, size);
        return StartCellToWorldCenter(startCell, size);
    }

    // ───── Blueprint에서 쓰는 Public Helper ─────

    public Vector3 GridOriginPos => gridOrigin != null ? gridOrigin.position : Vector3.zero;

    public Vector2Int WorldToCellCoord(Vector3 worldPos)
    {
        Vector3 origin = GridOriginPos;
        return new Vector2Int(
            Mathf.FloorToInt((worldPos.x - origin.x) / cellSize),
            Mathf.FloorToInt((worldPos.z - origin.z) / cellSize));
    }

    public Vector3 CellCenterToWorld(Vector2 cellCoord)
    {
        Vector3 origin = GridOriginPos;
        return new Vector3(
            origin.x + cellCoord.x * cellSize,
            fixedY,
            origin.z + cellCoord.y * cellSize);
    }

    public Vector2Int RotatedSizeOf(Vector2Int size, int rotationY) => GetRotatedSize(size, rotationY);

    public List<Vector2Int> FootprintOf(Vector2Int startCell, Vector2Int size) => GetFootprintCellsFromStartCell(startCell, size);

    public bool AreCellsOccupied(List<Vector2Int> cells) => IsAnyCellOccupied(cells);

    public bool IsPhysicallyBlocked(Vector3 centerPos, Vector2Int size, Quaternion rotation)
        => IsBlockedByPhysics(centerPos, size, rotation);

    public bool IsInBuildZoneNow => zoneChecker != null && zoneChecker.IsInBuildZone;

    /// <summary>
    /// 홀로그램 연출 없이 즉시 설비를 배치한다. Blueprint 붙여넣기에서 호출.
    /// 호출자가 footprintCells 비점유/물리 미충돌을 이미 검증했다고 가정.
    /// </summary>
    public PlacedBuilding PlaceFacilityImmediate(int facilityId, Vector3 worldPos, Quaternion rotation, List<Vector2Int> footprintCells)
    {
        FacilityRow facility = DataStore.GetFacility(facilityId);
        GameObject prefab = prefabDatabase != null ? prefabDatabase.GetPrefab(facilityId) : null;
        if (facility == null || prefab == null)
        {
            Debug.LogWarning($"[BuildManager] PlaceFacilityImmediate 실패. facilityId={facilityId}");
            return null;
        }

        OccupyCells(footprintCells);

        GameObject obj = Instantiate(prefab, worldPos, rotation, buildParent);

        PlacedBuilding placedBuilding = obj.GetComponent<PlacedBuilding>() ?? obj.AddComponent<PlacedBuilding>();
        placedBuilding.facilityId = facility.facilityId;
        placedBuilding.currentLevel = 1;
        placedBuilding.occupiedCells = new List<Vector2Int>(footprintCells);
        placedBuilding.originCell = footprintCells[0];
        placedBuilding.CacheRenderers();

        FacilityInstance facilityInstance = obj.GetComponent<FacilityInstance>() ?? obj.AddComponent<FacilityInstance>();
        facilityInstance.Initialize(facility.facilityId);

        placedBuilding.SetupLabel(facility.facilityName, facility.gridW, facility.gridH, cellSize);

        if (!IsTopViewMode)
            placedBuilding.HideLabel();

        SpawnBuildCompleteEffect(worldPos, rotation);
        return placedBuilding;
    }

    public Vector2Int SizeOfFacility(int facilityId)
    {
        FacilityRow row = DataStore.GetFacility(facilityId);
        return row != null ? new Vector2Int(row.gridW, row.gridH) : Vector2Int.one;
    }

    // ───── end Public Helper ─────

    private Vector2Int WorldToStartCell(Vector3 worldPos)
    {
        Vector3 origin = gridOrigin != null ? gridOrigin.position : Vector3.zero;
        Vector3 local = worldPos - origin;

        int cellX = Mathf.FloorToInt(local.x / cellSize);
        int cellZ = Mathf.FloorToInt(local.z / cellSize);

        return new Vector2Int(cellX, cellZ);
    }

    /// <summary>
    /// 커서(worldPos)가 건물의 '중앙'에 오도록 시작 셀을 계산한다.
    /// 홀수 크기면 커서가 들어간 셀이 중심 셀, 짝수 크기면 가장 가까운 격자 교차점이 중심이 된다.
    /// </summary>
    private Vector2Int WorldToStartCellCentered(Vector3 worldPos, Vector2Int size)
    {
        Vector3 origin = gridOrigin != null ? gridOrigin.position : Vector3.zero;
        Vector3 local = worldPos - origin;

        int startX = Mathf.RoundToInt(local.x / cellSize - size.x * 0.5f);
        int startZ = Mathf.RoundToInt(local.z / cellSize - size.y * 0.5f);

        return new Vector2Int(startX, startZ);
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

    private bool IsAnyCellOnRail(List<Vector2Int> cells)
    {
        if (railBuildManager == null) return false;

        for (int i = 0; i < cells.Count; i++)
        {
            if (railBuildManager.HasRailAt(cells[i]))
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

                // 해제 모드 진입 시 다른 서브모드(레일/청사진) 정리
                if (CurrentSubMode == BuildSubMode.Rail)
                {
                    railBuildManager?.EndRailMode();
                    CurrentSubMode = BuildSubMode.Facility;
                }
                else if (CurrentSubMode == BuildSubMode.Blueprint)
                {
                    blueprintModeManager?.Deactivate();
                    CurrentSubMode = BuildSubMode.Facility;
                }
            }
            else
            {
                ClearHoveredBuilding();
                ClearHoveredRail();
                isDragDemolishing = false;
                dragDemolishedBuildings.Clear();
                dragDemolishedRailCells.Clear();
            }
        }
    }

    private void UpdateCameraDragGate()
    {
        if (topViewPanCamera == null)
            return;

        // 좌클릭이 본래 동작(시설 배치/라우팅/영역 드래그/해제)에 안 쓰일 때만 카메라 드래그 허용.
        bool canDrag = !isDemolishMode
                    && CurrentSubMode == BuildSubMode.Facility
                    && currentIndex < 0;

        topViewPanCamera.SetMouseDragEnabled(canDrag);
    }

    private void HandleDemolish()
    {
        if (mainCam == null)
            return;

        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // Shift 드래그 상태 토글
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

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

        // 1) 시설 호버/해제
        if (Physics.Raycast(ray, out RaycastHit bHit, rayDistance, placedBuildingMask))
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

        // 2) 레일 호버/해제 (railMask가 설정돼 있어야 동작)
        if (railBuildManager != null && railMask.value != 0 &&
            Physics.Raycast(ray, out RaycastHit rHit, rayDistance, railMask))
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

    private void DemolishBuilding(PlacedBuilding building)
    {
        if (building == null) return;

        railBuildManager?.RemoveRailsConnectedToBuilding(building);

        RemoveOccupiedCells(building.occupiedCells);
        if (currentHoveredBuilding == building) currentHoveredBuilding = null;

        PlayDemolishSound();
        Destroy(building.gameObject);

        if (IsRailSubMode)
            railBuildManager?.RefreshPortIndicators();
    }

    private void DemolishRail(Vector2Int cell)
    {
        if (railBuildManager == null) return;
        if (!railBuildManager.RemoveRailAt(cell)) return;

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

    // ───── 레일 호버 하이라이트 (오버레이 쿼드 방식) ─────
    [Header("Demolish Rail Highlight")]
    [Tooltip("레일 호버 시 덮어씌울 머티리얼 (투명한 빨간색 Unlit 추천)")]
    public Material railDemolishOverlayMaterial;
    [Tooltip("레일 셀에 덮을 빨간 박스 높이. 0이면 바닥에 얇게 깔림")]
    public float railDemolishOverlayHeight = 0.2f;

    private GameObject _railOverlayGO;

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

        // 셀 크기에 맞춰 스케일, 레일 셀 중심 월드 좌표
        float cs = railBuildManager != null ? railBuildManager.CellSizeRail : cellSize;
        float y = railBuildManager != null ? railBuildManager.FixedYRail : fixedY;
        Vector3 center = rail.transform.position;
        center.y = y + 0.02f; // 지면 바로 위

        _railOverlayGO.transform.position = center;
        _railOverlayGO.transform.rotation = Quaternion.identity;
        _railOverlayGO.transform.localScale = new Vector3(cs * 0.95f, Mathf.Max(0.01f, railDemolishOverlayHeight), cs * 0.95f);
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
        if (col != null) Destroy(col);

        var rend = go.GetComponent<MeshRenderer>();
        if (rend != null)
        {
            if (railDemolishOverlayMaterial != null)
                rend.sharedMaterial = railDemolishOverlayMaterial;
            else
            {
                // 폴백: Unlit 빨간 반투명 런타임 머티리얼
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
        if (audioSource == null || demolishClip == null)
            return;

        audioSource.PlayOneShot(demolishClip, demolishVolume);
    }

    private void PlayBuildStartSound()
    {
        if (audioSource == null || buildStartClip == null)
            return;

        audioSource.PlayOneShot(buildStartClip, 0.1f);
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

        startCell = WorldToStartCellCentered(hit.point, rotatedSize);
        snappedPos = StartCellToWorldCenter(startCell, rotatedSize);
        footprintCells = GetFootprintCellsFromStartCell(startCell, rotatedSize);

        bool isInBuildZone = zoneChecker != null && zoneChecker.IsInBuildZone;

        bool isOccupied = IsAnyCellOccupied(footprintCells);
        bool isOnRail = IsAnyCellOnRail(footprintCells);

        bool isBlocked = IsBlockedByPhysics(snappedPos, rotatedSize, rotation);
        bool installRuleOk = CheckInstallRule(currentFacility);

        canBuild = isInBuildZone && !isOccupied && !isOnRail && !isBlocked && installRuleOk;

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