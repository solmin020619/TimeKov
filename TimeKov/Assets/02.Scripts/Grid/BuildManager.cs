// =====================================================================
// BuildManager.cs
// 건축 모드 전체 관리 — 시설 배치, 레일, 청사진, 해제
// 구버전 FacilityRow / DataStore 참조를 새 스키마(FacilityDataSheetData / GameDataHolder)로 교체
// =====================================================================

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

    // 현재 화면에 활성화된 프리뷰 월드 위치 반환 (BuildGridOverlay 등 외부 표시용)
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

    // 청사진 유령이 커서를 따라다닐 때 오버레이 격자 반경 확장용
    public bool TryGetOverridePatchRadius(out int radiusCells)
    {
        radiusCells = 0;
        if (blueprintModeManager != null && blueprintModeManager.TryGetBlueprintBoundsRadius(out radiusCells))
            return true;
        return false;
    }

    [Header("Build Slots (1~9 keys)")]
    public BuildSlot[] buildSlots;

    [Header("UI Effects")]
    public HotbarSlotEffect[] slotEffects;
    public HotbarSlotEffect railSlotEffect;

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

    private int currentIndex = -1;
    private bool hasSelectedSlot = false;
    private int currentRotationY = 0;

    private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

    private bool isDragBuilding = false;
    private readonly HashSet<Vector2Int> dragPlacedStartCells = new HashSet<Vector2Int>();

    private void Start()
    {
        // 로딩씬을 거쳐 진입하므로 데이터는 항상 로드 완료 상태
        // (DataBoot.IsLoaded 체크 불필요)

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

            if (hasSelectedSlot)
                RefreshPreviewMarker();
            else if (previewMarker != null)
                previewMarker.SetActive(false);
        }
    }

    private void HandleModeInput()
    {
        // B 키: 빌드 모드 진입/종료 토글
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (IsBuildMode) ExitBuildMode();
            else             EnterBuildMode();
        }

        // 우클릭: 빌드 모드 중에만 종료 (ESC는 WindowManager가 디스패치)
        if (Input.GetMouseButtonDown(1) && IsBuildMode)
            ExitBuildMode();
    }

    // ── 빌드 모드 진입/종료 ─────────────────────────────────────────
    // WindowManager의 BuildModeWindowAdapter가 OnOpen/OnClose에서 호출.
    // 양쪽 모두 idempotent — 이미 그 상태면 즉시 return.

    public void EnterBuildMode()
    {
        if (IsBuildMode) return;

        // 다른 UI가 열려있으면 진입 차단
        if (GameUIController.Instance != null && GameUIController.Instance.IsUIBlocking())
            return;

        IsBuildMode = true;
        hasSelectedSlot = false;
        currentIndex = -1;
        CurrentSubMode = BuildSubMode.Facility;

        if (previewMarker != null)
            previewMarker.SetActive(false);

        SetTopViewMode(true);

        GameUIController.Instance?.SetState(GameUIController.UIState.Build);

        // 활성 FacilityPlaceObjective 있으면 BuildZone 위에 화살표
        ShowBuildHintIfQuestActive();
    }

    public void ExitBuildMode()
    {
        if (!IsBuildMode) return;

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

        GameUIController.Instance?.CloseAllUI();

        // 빌드 가이드 화살표 정리
        TimeKov.UI.HintArrowManager.I?.Hide("build_zone_hint");
    }

    /// <summary>활성 FacilityPlaceObjective 있으면 BuildZone 위에 화살표.</summary>
    void ShowBuildHintIfQuestActive()
    {
        var mgr = TimeKov.UI.HintArrowManager.I;
        if (mgr == null) return;
        if (QuestManager.Instance == null)
        {
            mgr.Hide("build_zone_hint");
            return;
        }

        bool hasPlace = false;
        foreach (var rt in QuestManager.Instance.Runtimes)
        {
            if (rt?.activeObjectives == null) continue;
            foreach (var obj in rt.activeObjectives)
            {
                if (obj is FacilityPlaceObjective place && !place.IsCompleted)
                {
                    hasPlace = true;
                    break;
                }
            }
            if (hasPlace) break;
        }

        if (!hasPlace) { mgr.Hide("build_zone_hint"); return; }

        var zone = FindAnyObjectByType<BuildZone>();
        if (zone == null) { mgr.Hide("build_zone_hint"); return; }

        mgr.Show("build_zone_hint", zone.transform, 0f);
    }

    private void SetTopViewMode(bool value, bool force = false)
    {
        if (!force && IsTopViewMode == value)
            return;

        IsTopViewMode = value;

        if (value)
            StopPlayerImmediately();

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

        // Cursor.lockState / visible 은 WindowManager.ApplyGlobalState가 BuildMode Open/Close 시
        // 어댑터의 LocksGameplayInput 플래그 기반으로 자동 처리 (BuildModeWindowAdapter 부착 필요).
        if (value)
        {
            if (topViewCamera != null)
            {
                Vector3 startPos = topViewCamera.transform.position;

                if (topViewStartTarget != null)
                    startPos = topViewStartTarget.position + topViewStartOffset;

                topViewCamera.transform.position = startPos;
                topViewCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                // SmoothDamp 잔여 속도 제거 + targetPosition 동기화 (진입 직후 첫 입력이 튀는 것 방지)
                if (topViewPanCamera != null)
                    topViewPanCamera.SnapToCurrent();
            }
        }

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
            TryDragPlace(startCell, snappedPos, rotation, footprintCells, canBuild);

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

        // 구버전: DataStore.GetFacility() → 신버전: GetFacilityData()
        if (GetFacilityData(buildSlots[index].facilityId) == null)
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
        // 구버전: FacilityRow facility = GetCurrentFacilityRow(); facility.facilityId
        // 신버전: facilityId int 를 직접 사용
        int facilityId = GetCurrentFacilityId();
        if (facilityId == 0) yield break;

        FacilityDataSheetData facility = GetFacilityData(facilityId);
        if (facility == null) yield break;

        yield return PlaceFacilityRoutine(facilityId, position, rotation, footprintCells);
    }

    // facilityId를 지정해서 홀로그램 연출을 거쳐 배치. Blueprint 붙여넣기에서 호출.
    public void PlaceFacilityWithHologram(int facilityId, Vector3 position, Quaternion rotation, List<Vector2Int> footprintCells)
    {
        StartCoroutine(PlaceFacilityRoutine(facilityId, position, rotation, footprintCells));
    }

    private IEnumerator PlaceFacilityRoutine(int facilityId, Vector3 position, Quaternion rotation, List<Vector2Int> footprintCells)
    {
        // 구버전: FacilityRow facility = DataStore.GetFacility(facilityId);
        // 신버전: FacilityDataSheetData
        FacilityDataSheetData facility = GetFacilityData(facilityId);
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
            if (behaviours[i] == null) continue; // 프리팹의 미싱 스크립트 보호
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

        // 구버전: placedBuilding.facilityId = facility.facilityId
        // 신버전: facilityId 파라미터 int 직접 사용
        placedBuilding.facilityId = facilityId;
        placedBuilding.currentLevel = 1;
        placedBuilding.occupiedCells = new List<Vector2Int>(footprintCells);
        placedBuilding.originCell = footprintCells[0];
        placedBuilding.CacheRenderers();

        FacilityInstance facilityInstance = obj.GetComponent<FacilityInstance>();
        if (facilityInstance == null)
            facilityInstance = obj.AddComponent<FacilityInstance>();

        // 구버전: facilityInstance.Initialize(facility.facilityId)
        facilityInstance.Initialize(facilityId);

        // facility.facilityName, gridW, gridH → 신버전에도 동일 컬럼명
        placedBuilding.SetupLabel(facility.facilityName, facility.gridW, facility.gridH, cellSize);

        if (!IsTopViewMode)
            placedBuilding.HideLabel();

        if (IsRailSubMode)
            railBuildManager?.RefreshPortIndicators();

        PlayBuildCompleteSound();
        SpawnBuildCompleteEffect(position, rotation);

        // 퀘스트 시스템에 설치 완료 통지
        GameEvents.RaiseFacilityPlaced(facilityId);
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

    // ===== Blueprint에서 쓰는 Public Helper =====

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

    // 홀로그램 연출 없이 즉시 설비 배치. Blueprint 붙여넣기에서 호출.
    public PlacedBuilding PlaceFacilityImmediate(int facilityId, Vector3 worldPos, Quaternion rotation, List<Vector2Int> footprintCells)
    {
        // 구버전: FacilityRow facility = DataStore.GetFacility(facilityId);
        FacilityDataSheetData facility = GetFacilityData(facilityId);
        GameObject prefab = prefabDatabase != null ? prefabDatabase.GetPrefab(facilityId) : null;

        if (facility == null || prefab == null)
        {
            Debug.LogWarning($"[BuildManager] PlaceFacilityImmediate 실패. facilityId={facilityId}");
            return null;
        }

        OccupyCells(footprintCells);

        GameObject obj = Instantiate(prefab, worldPos, rotation, buildParent);

        PlacedBuilding placedBuilding = obj.GetComponent<PlacedBuilding>() ?? obj.AddComponent<PlacedBuilding>();
        placedBuilding.facilityId = facilityId;
        placedBuilding.currentLevel = 1;
        placedBuilding.occupiedCells = new List<Vector2Int>(footprintCells);
        placedBuilding.originCell = footprintCells[0];
        placedBuilding.CacheRenderers();

        FacilityInstance facilityInstance = obj.GetComponent<FacilityInstance>() ?? obj.AddComponent<FacilityInstance>();
        facilityInstance.Initialize(facilityId);

        placedBuilding.SetupLabel(facility.facilityName, facility.gridW, facility.gridH, cellSize);

        if (!IsTopViewMode)
            placedBuilding.HideLabel();

        SpawnBuildCompleteEffect(worldPos, rotation);

        // 퀘스트 시스템에 설치 완료 통지 (Blueprint 즉시 배치 경로)
        GameEvents.RaiseFacilityPlaced(facilityId);

        return placedBuilding;
    }

    public Vector2Int SizeOfFacility(int facilityId)
    {
        // 구버전: FacilityRow row = DataStore.GetFacility(facilityId);
        FacilityDataSheetData data = GetFacilityData(facilityId);
        return data != null ? new Vector2Int(data.gridW, data.gridH) : Vector2Int.one;
    }

    // ===== end Public Helper =====

    private Vector2Int WorldToStartCell(Vector3 worldPos)
    {
        Vector3 origin = gridOrigin != null ? gridOrigin.position : Vector3.zero;
        Vector3 local = worldPos - origin;

        int cellX = Mathf.FloorToInt(local.x / cellSize);
        int cellZ = Mathf.FloorToInt(local.z / cellSize);

        return new Vector2Int(cellX, cellZ);
    }

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
            if (behaviours[i] == null) continue; // 프리팹의 미싱 스크립트 보호
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
        // 구버전: FacilityRow row = GetCurrentFacilityRow();
        FacilityDataSheetData data = GetCurrentFacilityData();

        if (data == null)
            return "None";

        return data.facilityName;
    }

    private int GetCurrentFacilityId()
    {
        if (!hasSelectedSlot) return 0;

        if (buildSlots == null || currentIndex < 0 || currentIndex >= buildSlots.Length)
            return 0;

        return buildSlots[currentIndex].facilityId;
    }

    // 구버전: private FacilityRow GetCurrentFacilityRow()
    // 신버전: FacilityDataSheetData 반환
    private FacilityDataSheetData GetCurrentFacilityData()
    {
        int facilityId = GetCurrentFacilityId();

        if (facilityId == 0)
            return null;

        return GetFacilityData(facilityId);
    }

    private GameObject GetCurrentFacilityPrefab()
    {
        if (prefabDatabase == null)
            return null;

        return prefabDatabase.GetPrefab(GetCurrentFacilityId());
    }

    private Vector2Int GetCurrentFacilitySize()
    {
        // 구버전: FacilityRow row = GetCurrentFacilityRow();
        FacilityDataSheetData data = GetCurrentFacilityData();

        if (data == null)
            return Vector2Int.one;

        return new Vector2Int(data.gridW, data.gridH);
    }

    private bool CanCurrentFacilityRotate()
    {
        // 구버전: row.canRotate == 1 (int)
        // 신버전: data.canRotate (bool)
        FacilityDataSheetData data = GetCurrentFacilityData();

        if (data == null)
            return false;

        return data.canRotate;
    }

    // installRule 컬럼이 새 스키마에서 삭제됨 → null 체크만 수행
    private bool CheckInstallRule(FacilityDataSheetData facility)
    {
        return facility != null;
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

    [Header("Demolish Rail Highlight")]
    [Tooltip("레일 호버 시 덮어씌울 머티리얼 (투명한 빨간색 Unlit 추천)")]
    public Material railDemolishOverlayMaterial;
    [Tooltip("레일 셀에 덮을 빨간 박스 높이")]
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

        float cs = railBuildManager != null ? railBuildManager.CellSizeRail : cellSize;
        float y = railBuildManager != null ? railBuildManager.FixedYRail : fixedY;
        Vector3 center = rail.transform.position;
        center.y = y + 0.02f;

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
            {
                rend.sharedMaterial = railDemolishOverlayMaterial;
            }
            else
            {
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
        if (audioSource == null || demolishClip == null) return;
        audioSource.PlayOneShot(demolishClip, demolishVolume);
    }

    private void PlayBuildStartSound()
    {
        if (audioSource == null || buildStartClip == null) return;
        audioSource.PlayOneShot(buildStartClip, 0.1f);
    }

    private void PlayBuildCompleteSound()
    {
        if (audioSource == null || buildCompleteClip == null) return;
        audioSource.PlayOneShot(buildCompleteClip);
    }

    private void SpawnBuildCompleteEffect(Vector3 position, Quaternion rotation)
    {
        if (buildCompleteEffectPrefab == null) return;

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

        // 구버전: FacilityRow currentFacility = GetCurrentFacilityRow();
        FacilityDataSheetData currentFacility = GetCurrentFacilityData();
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
        if (!canBuild) return;
        if (dragPlacedStartCells.Contains(startCell)) return;

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

    // ===== UI =====

    private void LateUpdate()
    {
        RefreshSlotUI();
    }

    private void RefreshSlotUI()
    {
        if (slotEffects != null)
        {
            for (int i = 0; i < slotEffects.Length; i++)
            {
                if (slotEffects[i] != null)
                {
                    bool isThisSelected = (IsBuildMode && hasSelectedSlot && currentIndex == i && CurrentSubMode == BuildSubMode.Facility);
                    slotEffects[i].SetSelected(isThisSelected);
                }
            }
        }

        if (railSlotEffect != null)
        {
            bool isRailSelected = (IsBuildMode && CurrentSubMode == BuildSubMode.Rail);
            railSlotEffect.SetSelected(isRailSelected);
        }
    }

    // =====================================================================
    // 신규 스키마 헬퍼 메서드
    // 구버전 DataStore.GetFacility(id) 를 대체한다
    // GameDataHolder.I.FacilityData 의 키는 facilityId.ToString() (예: "1", "2")
    // =====================================================================
    private FacilityDataSheetData GetFacilityData(int facilityId)
    {
        if (GameDataHolder.I.FacilityData.TryGet(facilityId.ToString(), out var data))
            return data;
        return null;
    }
}