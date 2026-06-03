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
    private BuildDemolisher demolisher;

    [Header("References")]
    public Camera mainCam;
    public PlayerBuildZoneChecker zoneChecker;
    public Transform buildParent;
    public FacilityPrefabDatabase prefabDatabase;

    // [BuildZone 확장] 건축 가능 영역 판정을 "플레이어 위치"가 아니라 "건물이 놓이는 칸"
    // 기준으로 바꾸기 위한 BuildZone 콜라이더 참조.
    // BuildZoneProgression 이 부모(Grid_plane) 스케일을 키우면 이 콜라이더의
    // bounds(월드 AABB)도 자동으로 커지므로, 영역이 넓어질수록 더 멀리 지을 수 있게 됨.
    // 비워두면 기존 동작(플레이어가 zone 안에 있으면 OK)으로 폴백.
    [Header("Build Zone (영역 판정)")]
    [Tooltip("BuildZone 의 BoxCollider. 건물 칸이 이 영역 안에 있어야 건축 허용.\n" +
             "비워두면 기존 방식(플레이어 위치 기준)으로 동작.")]
    public BoxCollider buildZoneCollider;

    [Tooltip("체크 시 건축존(zoneChecker) 안에 있을 때만 B키 빌드 모드 진입 가능. 기획자 토글.\n" +
             "zoneChecker 미연결 시에는 이 옵션과 무관하게 게이팅 생략.")]
    public bool requireZoneToBuild = true;
    [Tooltip("존 밖에서 빌드 모드 진입 시도 시 띄울 토스트 (선택). 기존 ToastNotification 재사용.\n" +
             "비워두면 콘솔 로그로 폴백.")]
    public ToastNotification buildZoneToast;
    [Tooltip("존 밖 안내 토스트 메시지")]
    public string buildZoneBlockedMessage = "건축 가능 지역이 아닙니다!";

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

    private readonly GridOccupancy occupancy = new GridOccupancy();

    private bool isDragBuilding = false;
    private readonly HashSet<Vector2Int> dragPlacedStartCells = new HashSet<Vector2Int>();

    private void Start()
    {
        // 로딩씬을 거쳐 진입하므로 데이터는 항상 로드 완료 상태
        // (DataBoot.IsLoaded 체크 불필요)

        demolisher = new BuildDemolisher(this, occupancy);

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
            demolisher?.Tick();
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

            demolisher?.Cancel();
            SetPreviewActive(false);

            railBuildManager?.BeginRailMode(this);
        }
        else if (mode == BuildSubMode.Blueprint)
        {
            isDemolishMode = false;
            isDragBuilding = false;
            dragPlacedStartCells.Clear();

            demolisher?.Cancel();
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

    // 존 밖 빌드 시도 안내 토스트. buildZoneToast 미연결 시 콘솔 로그로 폴백.
    void ShowBuildZoneToast()
    {
        if (buildZoneToast != null)
            buildZoneToast.Show(buildZoneBlockedMessage);
        else
            Debug.Log($"[BuildManager] {buildZoneBlockedMessage} (buildZoneToast 미연결)");
    }

    public void EnterBuildMode()
    {
        if (IsBuildMode) return;

        // 다른 UI가 열려있으면 진입 차단
        if (GameUIController.Instance != null && GameUIController.Instance.IsUIBlocking())
            return;

        // 건축존 밖이면 진입 차단 + 안내 토스트 (zoneChecker 미연결 시 게이팅 생략)
        if (requireZoneToBuild && zoneChecker != null && !zoneChecker.IsInBuildZone)
        {
            ShowBuildZoneToast();
            return;
        }

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
        hasSelectedSlot = false;
        currentIndex = -1;

        demolisher.Cancel();
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

        DisablePlacedObjectComponents(hologramObj);

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

    // 홀로그램/프리뷰 오브젝트의 물리·스크립트 비활성화 (충돌·동작 없이 보이기만).
    // PlaceFacilityRoutine(홀로그램)과 RefreshPreviewMarker(프리뷰)가 공유.
    private void DisablePlacedObjectComponents(GameObject obj)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody[] rigidbodies = obj.GetComponentsInChildren<Rigidbody>();
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
        }

        MonoBehaviour[] behaviours = obj.GetComponentsInChildren<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null) continue; // 프리팹의 미싱 스크립트 보호
            if (behaviours[i] != this)
                behaviours[i].enabled = false;
        }
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

    // ===== Blueprint에서 쓰는 Public Helper =====

    public Vector3 GridOriginPos => gridOrigin != null ? gridOrigin.position : Vector3.zero;

    public Vector2Int WorldToCellCoord(Vector3 worldPos) => GridMath.WorldToCell(worldPos, GridOriginPos, cellSize);

    public Vector3 CellCenterToWorld(Vector2 cellCoord) => GridMath.CellToWorld(cellCoord, GridOriginPos, cellSize, fixedY);

    public List<Vector2Int> FootprintOf(Vector2Int startCell, Vector2Int size) => GridMath.Footprint(startCell, size);

    public bool AreCellsOccupied(List<Vector2Int> cells) => IsAnyCellOccupied(cells);

    // [BuildZone 확장] Blueprint 모드 등 외부에서 footprint 기준 영역 검사용 public 래퍼
    public bool AreCellsInBuildZone(List<Vector2Int> cells) => IsFootprintInBuildZone(cells);

    public bool IsPhysicallyBlocked(Vector3 centerPos, Vector2Int size, Quaternion rotation)
        => IsBlockedByPhysics(centerPos, size, rotation);

    // [BuildZone 확장] 건물의 footprint(차지하는 모든 칸)가 BuildZone 영역 안에 완전히
    // 들어오는지 검사한다. buildZoneCollider 미연결 시 기존 플레이어 위치 기준으로 폴백.
    private bool IsFootprintInBuildZone(List<Vector2Int> footprintCells)
    {
        // 폴백: 콜라이더 미연결이면 기존 동작(플레이어가 zone 안에 있으면 허용)
        if (buildZoneCollider == null)
            return zoneChecker != null && zoneChecker.IsInBuildZone;

        if (footprintCells == null || footprintCells.Count == 0)
            return false;

        for (int i = 0; i < footprintCells.Count; i++)
        {
            if (!IsCellInBuildZone(footprintCells[i]))
                return false;
        }
        return true;
    }

    // [BuildZone 확장] 단일 칸이 BuildZone 영역 안에 완전히 들어오는지 검사한다.
    // 레일 등 1칸 단위 판정에서 호출 (시설 footprint 검사와 동일 규칙 재사용).
    // buildZoneCollider 미연결 시 기존 플레이어 위치 기준으로 폴백.
    public bool IsCellInBuildZone(Vector2Int cell)
    {
        if (buildZoneCollider == null)
            return zoneChecker != null && zoneChecker.IsInBuildZone;

        Bounds zb = buildZoneCollider.bounds; // 월드 AABB (부모 스케일·회전 반영됨)
        float half = cellSize * 0.5f;
        const float eps = 0.01f; // 경계 부동소수 오차 허용

        // 칸의 월드 중심 — StartCellToWorldCenter 에 1x1 크기를 주면 칸 중심이 나옴
        Vector3 c = StartCellToWorldCenter(cell, new Vector2Int(1, 1));

        // 칸의 X/Z 네 모서리가 모두 zone bounds 안에 있어야 함 (Y는 평면이라 무시)
        return c.x - half >= zb.min.x - eps && c.x + half <= zb.max.x + eps
            && c.z - half >= zb.min.z - eps && c.z + half <= zb.max.z + eps;
    }

    // ===== end Public Helper =====

    private Vector2Int WorldToStartCellCentered(Vector3 worldPos, Vector2Int size) => GridMath.WorldToStartCellCentered(worldPos, size, GridOriginPos, cellSize);

    private Vector3 StartCellToWorldCenter(Vector2Int startCell, Vector2Int size) => GridMath.StartCellToWorldCenter(startCell, size, GridOriginPos, cellSize, fixedY);

    private List<Vector2Int> GetFootprintCellsFromStartCell(Vector2Int startCell, Vector2Int size) => GridMath.Footprint(startCell, size);

    private Vector2Int GetRotatedSize(Vector2Int originalSize, int rotationY) => GridMath.RotatedSize(originalSize, rotationY);

    private bool IsAnyCellOccupied(List<Vector2Int> cells) => occupancy.IsAnyOccupied(cells);

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

    private void OccupyCells(List<Vector2Int> cells) => occupancy.Occupy(cells);

    private void RemoveOccupiedCells(List<Vector2Int> cells) => occupancy.Free(cells);

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

        DisablePlacedObjectComponents(previewMarker);

        previewMarker.SetActive(false);
    }

    private void SetPreviewActive(bool value)
    {
        if (previewMarker != null)
            previewMarker.SetActive(value);
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
                demolisher?.Cancel();
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

    [Header("Demolish Rail Highlight")]
    [Tooltip("레일 호버 시 덮어씌울 머티리얼 (투명한 빨간색 Unlit 추천)")]
    public Material railDemolishOverlayMaterial;
    [Tooltip("레일 셀에 덮을 빨간 박스 높이")]
    public float railDemolishOverlayHeight = 0.2f;

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

        // [BuildZone 확장] 플레이어 위치가 아니라 건물이 놓이는 칸이 영역 안인지 검사
        bool isInBuildZone = IsFootprintInBuildZone(footprintCells);
        bool isOccupied = IsAnyCellOccupied(footprintCells);
        bool isOnRail = IsAnyCellOnRail(footprintCells);
        bool isBlocked = IsBlockedByPhysics(snappedPos, rotatedSize, rotation);

        canBuild = isInBuildZone && !isOccupied && !isOnRail && !isBlocked;

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