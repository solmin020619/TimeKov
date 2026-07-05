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

public class BuildManager : MonoBehaviour, ISaveable
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
    [Tooltip("레일 오브젝트 레이어. 해제 모드에서 레일도 대상으로 삼을 때 사용.")]
    public LayerMask railMask;

    [Header("Demolish Audio")]
    public AudioClip demolishClip;
    [Range(0f, 1f)] public float demolishVolume = 1f;

    private bool _isDemolishMode = false;
    // 해제 모드 on/off 가 바뀔 때마다 BeltSegment 에 알려 모든 벨트를 흰색으로 띄운다.
    // (설치 모드의 SuppressConnectionColor 와 동일한 방식 — 진입 시 전부 흰색, 호버만 빨강)
    private bool isDemolishMode
    {
        get => _isDemolishMode;
        set
        {
            if (_isDemolishMode == value) return;
            _isDemolishMode = value;
            TIMEKOV.Factory.BeltSegment.DemolishModeActive = value;
        }
    }
    private BuildDemolisher demolisher;
    private BuildPlacementValidator validator;
    private FacilityPlacer placer;

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

    public BuildSubMode CurrentSubMode { get; private set; } = BuildSubMode.Facility;
    public bool IsRailSubMode => IsBuildMode && CurrentSubMode == BuildSubMode.Rail;
    public bool IsDemolishMode => isDemolishMode;
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

        worldPos = default;
        return false;
    }

    [Header("Build Slots (1~9 keys)")]
    public BuildSlot[] buildSlots;

    [Header("UI Effects")]
    public HotbarSlotEffect[] slotEffects;
    public HotbarSlotEffect railSlotEffect;

    [Header("퀵슬롯 아이콘 UI")]
    [Tooltip("각 슬롯의 QuickSlotIconUI. 배열 순서 = 슬롯 1~9번.")]
    public QuickSlotIconUI[] slotIconUIs;

    [Tooltip("레일(E) 슬롯의 아이콘 오브젝트(설비 슬롯의 'Quick Slot_png'에 해당). 비워두면 레일 슬롯 스킨 미적용. QuickSlotIconUI가 없으면 런타임에 자동 추가.")]
    public Transform railSlotIcon;

    [Header("Preview")]
    public GameObject previewMarker;
    // 프리뷰 틴트(초록/빨강) 대상 = 시설 렌더러만 캐시. 입출구 화살표는 이 캐시 이후 추가돼 틴트에서 제외(고유색 유지).
    private Renderer[] previewRenderers;

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

    private Rigidbody _resolvedPlayerRb;
    /// <summary>플레이어 Rigidbody — 배치 검증(플레이어 제외)/밀어내기에서 참조.
    /// 인스펙터 미연결이어도 "Player" 태그로 자동 탐색(수동 연결 불필요).</summary>
    public Rigidbody PlayerRigidbody
    {
        get
        {
            if (playerRigidbody != null) return playerRigidbody;
            if (_resolvedPlayerRb == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _resolvedPlayerRb = p.GetComponent<Rigidbody>();
            }
            return _resolvedPlayerRb;
        }
    }

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
        validator = new BuildPlacementValidator(this);
        placer = new FacilityPlacer(this, occupancy);

        if (previewMarker != null)
            previewMarker.SetActive(false);

        SetTopViewMode(false, true);
        EnsureTopViewPostProcessing();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (railBuildManager != null)
            railBuildManager.EndRailMode();

        // 동적 해금 슬롯 초기화
        InitDynamicUnlockSlots();

        SaveSlotManager.Instance?.Register(this);
        RestoreFromSave();
    }

    private void OnDestroy()
    {
        if (FacilityUnlockManager.Instance != null)
            FacilityUnlockManager.Instance.OnFacilityUnlocked -= HandleFacilityUnlocked;

        SaveSlotManager.Instance?.Unregister(this);
    }

    // ── 세이브/로드 (ISaveable) ───────────────────────────────────────────
    // 위치·회전·강화레벨 + 설비 내부 생산 상태(버퍼/연료/고정레시피)까지 저장.

    /// <summary>SaveSlotManager.SaveActive()가 호출. 현재 배치된 모든 건물/레일을 캡처한다.</summary>
    public void Capture(GameSaveData data)
    {
        data.buildings.Clear();
        data.machines.Clear();
        foreach (var pb in buildParent.GetComponentsInChildren<PlacedBuilding>(true))
        {
            data.buildings.Add(new PlacedBuildingData
            {
                facilityId = pb.facilityId,
                originCellX = pb.originCell.x,
                originCellY = pb.originCell.y,
                rotationY = Mathf.RoundToInt(pb.transform.eulerAngles.y) % 360,
                currentLevel = pb.currentLevel,
            });

            var machine = pb.GetComponent<TIMEKOV.Factory.MachineBase>();
            if (machine == null) continue;

            var msd = new MachineSaveData
            {
                originCellX = pb.originCell.x,
                originCellY = pb.originCell.y,
                fuelTimeRemaining = machine.FuelTimeRemaining,
                lockedRecipeIndex = (machine as TIMEKOV.Factory.ProcessingMachine)?.LockedRecipeIndex ?? -1,
            };
            foreach (var kv in machine.InputBuffer.Stock)
                msd.inputStock.Add(new ItemStackData { itemId = kv.Key, amount = kv.Value });
            foreach (var kv in machine.OutputBuffer.Stock)
                msd.outputStock.Add(new ItemStackData { itemId = kv.Key, amount = kv.Value });
            data.machines.Add(msd);
        }

        data.rails.Clear();
        if (railBuildManager != null)
        {
            foreach (var kv in railBuildManager.RailMap)
            {
                RailPiece piece = kv.Value;
                if (piece == null) continue;

                data.rails.Add(new RailPieceData
                {
                    cellX = kv.Key.x,
                    cellY = kv.Key.y,
                    up = piece.up,
                    down = piece.down,
                    left = piece.left,
                    right = piece.right,
                });
            }
        }
    }

    private void RestoreFromSave()
    {
        if (SaveSlotManager.Instance == null || !SaveSlotManager.Instance.HasActiveSlot) return;

        GameSaveData data = SaveSlotManager.Instance.Data;

        foreach (var entry in data.buildings)
            RestoreBuilding(entry);

        if (railBuildManager != null && data.rails.Count > 0)
        {
            foreach (var entry in data.rails)
                railBuildManager.PlaceRailImmediate(new Vector2Int(entry.cellX, entry.cellY),
                    entry.up, entry.down, entry.left, entry.right);

            railBuildManager.RestoreReflowAndValidate();
        }
    }

    private void RestoreBuilding(PlacedBuildingData entry)
    {
        FacilityDataSheetData facility = GetFacilityData(entry.facilityId);
        if (facility == null)
        {
            Debug.LogWarning($"[BuildManager] 복원 실패 — facilityId={entry.facilityId} 데이터 없음");
            return;
        }

        Vector2Int originCell = new Vector2Int(entry.originCellX, entry.originCellY);
        Vector2Int rotatedSize = GetRotatedSize(new Vector2Int(facility.gridW, facility.gridH), entry.rotationY);
        List<Vector2Int> footprintCells = GetFootprintCellsFromStartCell(originCell, rotatedSize);
        Vector3 position = StartCellToWorldCenter(originCell, rotatedSize);
        Quaternion rotation = Quaternion.Euler(0f, entry.rotationY, 0f);

        GameObject obj = placer.PlaceInstant(entry.facilityId, position, rotation, footprintCells, entry.currentLevel);
        if (obj == null) return;

        MachineSaveData msd = FindMachineData(entry.originCellX, entry.originCellY);
        if (msd == null) return;

        var machine = obj.GetComponent<TIMEKOV.Factory.MachineBase>();
        if (machine == null) return;

        machine.RestoreBuffers(msd.inputStock, msd.outputStock, msd.fuelTimeRemaining);
        if (machine is TIMEKOV.Factory.ProcessingMachine pm)
            pm.RestoreLockedRecipe(msd.lockedRecipeIndex);
    }

    private MachineSaveData FindMachineData(int originCellX, int originCellY)
    {
        foreach (var m in SaveSlotManager.Instance.Data.machines)
            if (m.originCellX == originCellX && m.originCellY == originCellY)
                return m;
        return null;
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

        HandleRotateInput();
        HandleBuild();
    }
    private void HandleSelectionInput()
    {
        if (!IsBuildMode)
            return;
        if (GameUIController.Instance != null && GameUIController.Instance.IsTutorialCoachActive)
            return;   // 튜토리얼 코치(투어) 중엔 E/1~9 무시

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
        // 잠긴(미해금) 슬롯 키를 누르면 선택하지 않고 안내만 (잠긴 슬롯은 facilityId == 0)
        if (buildSlots != null && index >= 0 && index < buildSlots.Length
            && buildSlots[index].facilityId == 0)
        {
            ToastManager.Warning("아직 해금되지 않은 설비입니다");
            return;
        }

        // 설치 상한 도달 설비는 선택 자체를 막고 안내 (창고 출력 포트 등)
        if (buildSlots != null && index >= 0 && index < buildSlots.Length)
        {
            int fid = buildSlots[index].facilityId;
            if (fid > 0 && FacilityBuildLimit.HasLimit(fid))
            {
                int max = FacilityBuildLimit.GetMax(fid);
                int placed = CountPlacedFacilities(fid);
                if (placed >= max)
                {
                    ToastManager.Warning($"설치 한도에 도달했습니다 ({placed}/{max})");
                    return;
                }
            }
        }

        // 해제 모드(X) 중 슬롯 키를 누르면 해제에서 빠져나와 바로 건축 모드로 (편의)
        bool fromDemolish = isDemolishMode;
        if (fromDemolish)
        {
            isDemolishMode = false;
            demolisher?.Cancel();
        }

        // 레일/청사진 서브모드 중 슬롯 키를 누르면 그 모드를 정리하고 설비 모드로.
        // SetSubMode 가 EndRailMode/Deactivate(고스트 제거)까지 담당. 이 경우 토글 없이 무조건 선택.
        bool fromOtherSubMode = CurrentSubMode != BuildSubMode.Facility;
        if (fromOtherSubMode)
            SetSubMode(BuildSubMode.Facility);

        // 같은 슬롯 다시 누르면 선택 해제(토글). 단 해제 모드/다른 서브모드에서 막 나온 경우엔 토글 없이 무조건 선택.
        if (!fromDemolish && !fromOtherSubMode && hasSelectedSlot && currentIndex == index)
        {
            hasSelectedSlot = false;
            currentIndex = -1;

            if (previewMarker != null)
                previewMarker.SetActive(false);

            return;
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
        // 튜토리얼 코치마크(오버레이) 중에는 키보드 차단 — B 진입/종료, 우클릭 종료 무시.
        // (ESC는 GameUIController.HandleEscape가 별도 처리하므로 일시정지는 항상 가능)
        if (GameUIController.Instance != null && GameUIController.Instance.IsTutorialCoachActive)
            return;

        // 사망 오버레이 중에는 건축 입력 차단 (부활 버튼 외 입력 금지)
        if (DeathOverlayUI.IsOpen)
            return;

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
        // 공통 토스트로 통일 (#9) — 기존 buildZoneToast(퀘스트형) 대신 ToastManager 사용.
        ToastManager.Show(buildZoneBlockedMessage, ToastStyle.Warning);
    }

    // 진입 성공 여부 반환 (true=건설 모드, false=차단됨). 건축투어가 실패 시 대기하는 데 사용.
    public bool EnterBuildMode()
    {
        if (IsBuildMode) return true;

        // 다른 UI가 열려있으면 진입 차단
        if (GameUIController.Instance != null && GameUIController.Instance.IsUIBlocking())
            return false;

        // 건축존 밖이면 진입 차단 + 안내 토스트 (zoneChecker 미연결 시 게이팅 생략).
        // 점프로 BuildZone 트리거를 벗어나도(Y축) 막히던 #21 방지 — XZ 위치로 판정.
        if (requireZoneToBuild && zoneChecker != null && !IsPlayerInBuildZoneXZ())
        {
            ShowBuildZoneToast();
            return false;
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

        // 튜토리얼 등 전역 구독자 통지 — 존 게이트 통과 후 실제 진입 시에만 (B키만 누른 게 아님)
        GameEvents.RaiseBuildModeEntered();

        // 대기 중인 해금 슬롯 '팡' 연출 재생
        PlayPendingUnlocks();
        return true;
    }

    // 점프로 BuildZone 트리거를 벗어나도(Y축) 건축 진입이 막히던 #21 방지.
    // buildZoneCollider 가 있으면 플레이어 XZ 위치만으로 판정(Y 무시), 없으면 트리거 폴백.
    private bool IsPlayerInBuildZoneXZ()
    {
        var prb = PlayerRigidbody;
        if (buildZoneCollider != null && prb != null)
        {
            Vector3 p = prb.position;
            Bounds b = buildZoneCollider.bounds;
            const float eps = 0.01f;
            return p.x >= b.min.x - eps && p.x <= b.max.x + eps
                && p.z >= b.min.z - eps && p.z <= b.max.z + eps;
        }
        return zoneChecker == null || zoneChecker.IsInBuildZone;
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

    // 탑뷰 진입 시 카메라가 맞출 앵커. topViewStartTarget 지정 시 그쪽(고정) 우선,
    // 없으면 플레이어 현재 위치(태그로 1회 탐색 후 캐시) 기준.
    private Transform _topViewPlayerCache;
    private Transform GetTopViewAnchor()
    {
        if (topViewStartTarget != null) return topViewStartTarget;
        if (_topViewPlayerCache == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _topViewPlayerCache = p.transform;
        }
        return _topViewPlayerCache;
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

                // 진입 시 플레이어 현재 위치 위에서 시작 (topViewStartTarget 지정 시 그쪽 우선)
                Transform anchor = GetTopViewAnchor();
                if (anchor != null)
                    startPos = anchor.position + topViewStartOffset;

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

    // 탑뷰(빌드) 카메라에 포스트프로세싱을 적용한다 (#6).
    // URP는 카메라별로 포스트프로세싱을 켜는데, TopViewCamera는 꺼져 있어 빌드모드 진입 시
    // 색보정/블룸/비네트 등이 빠져 보였다. 메인 카메라 설정을 그대로 복사해 화면을 일치시킨다.
    private void EnsureTopViewPostProcessing()
    {
        if (topViewCamera == null) return;

        var topData = topViewCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (topData == null) return;

        var gameData = gameplayCamera != null
            ? gameplayCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()
            : null;

        if (gameData != null)
        {
            topData.renderPostProcessing = gameData.renderPostProcessing;
            topData.volumeLayerMask = gameData.volumeLayerMask;
            topData.volumeTrigger = gameData.volumeTrigger;
            topData.antialiasing = gameData.antialiasing;
            topData.antialiasingQuality = gameData.antialiasingQuality;
        }
        else
        {
            topData.renderPostProcessing = true;
        }
    }

    private void HandleRotateInput()
    {
        if (IsRailSubMode)
            return;

        if (!Input.GetKeyDown(KeyCode.R))
            return;

        // 선택된 설비가 없으면 무시. 설비는 있는데 회전 불가면 토스트.
        FacilityDataSheetData data = GetCurrentFacilityData();
        if (data == null)
            return;
        if (!data.canRotate)
        {
            ToastManager.Info("회전할 수 없는 설비입니다");
            return;
        }

        currentRotationY += 90;
        if (currentRotationY >= 360)
            currentRotationY = 0;
    }

    private void HandleBuild()
    {
        if (IsRailSubMode)
            return;
        if (GameUIController.Instance != null && GameUIController.Instance.IsTutorialCoachActive)
            return;   // 튜토리얼 코치(투어) 중엔 설치 클릭 무시

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
        int facilityId = GetCurrentFacilityId();
        if (facilityId == 0) yield break;

        // 홀로그램 연출(1.2초) 동안엔 PlacedBuilding 이 아직 없어서 설치 상한을 연타로 뚫을 수 있음
        // -> 진행 중 배치도 개수에 포함(finally 라 코루틴 강제 종료에도 안 샌다).
        _pendingPlaceCounts.TryGetValue(facilityId, out int pending);
        _pendingPlaceCounts[facilityId] = pending + 1;
        try
        {
            yield return placer.PlaceRoutine(facilityId, position, rotation, footprintCells);
        }
        finally
        {
            if (_pendingPlaceCounts.TryGetValue(facilityId, out int p))
                _pendingPlaceCounts[facilityId] = Mathf.Max(0, p - 1);
        }
    }

    // ── 테두리 설비 + 설치 상한 ──────────────────────────────────────────

    [Header("테두리 설비 (창고 출력 포트)")]
    [Tooltip("테두리 설비의 방향 보정(도). 포트가 구역 반대편을 보면 180으로. 0/180만 사용(90은 크기축이 어긋남).")]
    public int edgeFacilityYawOffset = 0;

    // 배치 연출 진행 중(아직 PlacedBuilding 없음)인 설비 수. 상한 판정에 합산.
    private readonly Dictionary<int, int> _pendingPlaceCounts = new Dictionary<int, int>();

    // 테두리 설비 판정 = 프리팹에 StorageExtractor 가 있으면. (마커 역할 - 확장 시 별도 마커로 승격)
    private bool IsEdgeFacility(GameObject prefab)
        => prefab != null && prefab.GetComponentInChildren<TIMEKOV.Factory.StorageExtractor>(true) != null;

    /// <summary>현재 배치된(연출 진행 중 포함) 특정 설비 개수. 설치 상한 판정용.</summary>
    public int CountPlacedFacilities(int facilityId)
    {
        int n = 0;
        if (buildParent != null)
            foreach (var pb in buildParent.GetComponentsInChildren<PlacedBuilding>())
                if (pb != null && pb.facilityId == facilityId) n++;
        if (_pendingPlaceCounts.TryGetValue(facilityId, out int pending)) n += pending;
        return n;
    }

    // 구역 AABB -> "완전히 안에 들어가는" 셀 범위 (validator.IsCellInBuildZone 과 동일 기준).
    private bool TryGetZoneCellRect(out int minCx, out int maxCx, out int minCz, out int maxCz)
    {
        minCx = maxCx = minCz = maxCz = 0;
        if (buildZoneCollider == null) return false;
        Bounds zb = buildZoneCollider.bounds;
        Vector3 o = GridOriginPos;
        const float eps = 0.01f;
        minCx = Mathf.CeilToInt((zb.min.x - o.x) / cellSize - eps);
        maxCx = Mathf.FloorToInt((zb.max.x - o.x) / cellSize + eps) - 1;
        minCz = Mathf.CeilToInt((zb.min.z - o.z) / cellSize - eps);
        maxCz = Mathf.FloorToInt((zb.max.z - o.z) / cellSize + eps) - 1;
        return maxCx >= minCx && maxCz >= minCz;
    }

    // 커서 -> 가장 가까운 구역 변의 "안쪽" 테두리 줄에 딱 붙는 배치 계산(종욱: 바깥 배치 폐기).
    // 변을 따라 슬라이드, 회전은 변 방향 자동(포트가 존 안쪽을 바라봄 = 벨트는 존 안에서 연결).
    private bool TryGetEdgeSnap(Vector3 cursor, Vector2Int rawSize,
        out Vector2Int startCell, out Vector3 snappedPos, out Quaternion rotation, out Vector2Int rotatedSize)
    {
        startCell = default; snappedPos = default;
        rotation = Quaternion.identity; rotatedSize = rawSize;

        if (!TryGetZoneCellRect(out int minCx, out int maxCx, out int minCz, out int maxCz))
            return false;

        Bounds zb = buildZoneCollider.bounds;
        float dW = Mathf.Abs(cursor.x - zb.min.x);
        float dE = Mathf.Abs(cursor.x - zb.max.x);
        float dS = Mathf.Abs(cursor.z - zb.min.z);
        float dN = Mathf.Abs(cursor.z - zb.max.z);
        float best = Mathf.Min(dW, Mathf.Min(dE, Mathf.Min(dS, dN)));

        // 변별 기본 방향 = 포트가 존 안쪽(내부)을 바라보게. 모델 정면 축이 다르면 edgeFacilityYawOffset(0/180)로 보정.
        int yaw;
        if      (best == dS) yaw = 0;
        else if (best == dN) yaw = 180;
        else if (best == dW) yaw = 90;
        else                 yaw = 270;
        yaw = (yaw + edgeFacilityYawOffset + 360) % 360;

        rotatedSize = GetRotatedSize(rawSize, yaw);
        rotation = Quaternion.Euler(0f, yaw, 0f);

        Vector3 o = GridOriginPos;
        if (best == dS || best == dN)
        {
            int startX = Mathf.RoundToInt((cursor.x - o.x) / cellSize - rotatedSize.x * 0.5f);
            startX = Mathf.Clamp(startX, minCx, Mathf.Max(minCx, maxCx - rotatedSize.x + 1));
            int startZ = (best == dS) ? minCz : maxCz - rotatedSize.y + 1;   // 존 안쪽 첫/마지막 줄
            startCell = new Vector2Int(startX, startZ);
        }
        else
        {
            int startZ = Mathf.RoundToInt((cursor.z - o.z) / cellSize - rotatedSize.y * 0.5f);
            startZ = Mathf.Clamp(startZ, minCz, Mathf.Max(minCz, maxCz - rotatedSize.y + 1));
            int startX = (best == dW) ? minCx : maxCx - rotatedSize.x + 1;   // 존 안쪽 첫/마지막 열
            startCell = new Vector2Int(startX, startZ);
        }

        snappedPos = StartCellToWorldCenter(startCell, rotatedSize);
        return true;
    }

    // ===== 외부(레일/일반 배치)에서 쓰는 Public Helper =====

    public Vector3 GridOriginPos => gridOrigin != null ? gridOrigin.position : Vector3.zero;

    private bool IsFootprintInBuildZone(List<Vector2Int> footprintCells) => validator.IsFootprintInBuildZone(footprintCells);

    // 레일(RailBuildManager) 등 외부에서 1칸 단위 존 판정에 쓰는 public 래퍼.
    public bool IsCellInBuildZone(Vector2Int cell) => validator.IsCellInBuildZone(cell);

    // 설비 점유 셀 판정 (RailBuildManager 가 레일의 설비 관통 방지에 사용)
    public bool IsCellOccupied(Vector2Int cell) => occupancy.IsOccupied(cell);

    // ===== end Public Helper =====

    private Vector2Int WorldToStartCellCentered(Vector3 worldPos, Vector2Int size) => GridMath.WorldToStartCellCentered(worldPos, size, GridOriginPos, cellSize);

    private Vector3 StartCellToWorldCenter(Vector2Int startCell, Vector2Int size) => GridMath.StartCellToWorldCenter(startCell, size, GridOriginPos, cellSize, fixedY);

    private List<Vector2Int> GetFootprintCellsFromStartCell(Vector2Int startCell, Vector2Int size) => GridMath.Footprint(startCell, size);

    private Vector2Int GetRotatedSize(Vector2Int originalSize, int rotationY) => GridMath.RotatedSize(originalSize, rotationY);

    private bool IsAnyCellOccupied(List<Vector2Int> cells) => occupancy.IsAnyOccupied(cells);

    private bool IsAnyCellOnRail(List<Vector2Int> cells) => validator.IsAnyCellOnRail(cells);

    private bool IsBlockedByPhysics(Vector3 centerPos, Vector2Int size, Quaternion rotation) => validator.IsBlocked(centerPos, size, rotation);

    private void UpdatePreview(Vector3 position, Quaternion rotation, bool canBuild)
    {
        if (previewMarker == null)
            return;

        previewMarker.SetActive(true);
        previewMarker.transform.position = position;
        previewMarker.transform.rotation = rotation;

        Color tint = canBuild ? Color.green : Color.red;
        // 시설 렌더러만 틴트(화살표 제외 - 캐시는 화살표 추가 전에 잡음). 폴백으로 캐시 없으면 재쿼리.
        Renderer[] renderers = previewRenderers ?? previewMarker.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Material mat = renderers[i].material;
            // URP 는 _BaseColor, 빌트인은 _Color 를 쓴다. 셰이더에 있는 쪽을 칠해야 색이 먹는다.
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
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

        FacilityPlacer.DisableGhostComponents(previewMarker, this);

        // 시설 렌더러만 캐시(틴트 대상). 화살표는 이 다음에 추가 -> 틴트 제외(고유색 유지).
        previewRenderers = previewMarker.GetComponentsInChildren<Renderer>();

        // 프리뷰에 입구/출구 화살표 표시 - 놓기 전에 방향을 보고 맞춰 놓게(잘못 놓고 지우는 과정 제거).
        // 프리뷰엔 연결 판정이 없어 X는 안 뜸. 화살표는 고스트 자식이라 이동/회전을 따라감.
        railBuildManager?.ShowGhostPortArrows(previewMarker, GetCurrentFacilitySize());

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
        if (GameUIController.Instance != null && GameUIController.Instance.IsTutorialCoachActive)
            return;   // 튜토리얼 코치(투어) 중엔 X 해제토글 무시
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

        // 테두리 설비(창고 출력 포트): 구역 안쪽 테두리 줄에 딱 붙여 설치.
        // 커서에 가장 가까운 변으로 스냅해 변을 따라 슬라이드, 회전은 변에 맞춰 자동.
        if (IsEdgeFacility(currentPrefab))
        {
            if (!TryGetEdgeSnap(hit.point, GetCurrentFacilitySize(),
                    out startCell, out snappedPos, out rotation, out rotatedSize))
            {
                SetPreviewActive(false);
                return false;
            }
            footprintCells = GetFootprintCellsFromStartCell(startCell, rotatedSize);

            int fid = GetCurrentFacilityId();
            bool limitOk = !FacilityBuildLimit.HasLimit(fid)
                        || CountPlacedFacilities(fid) < FacilityBuildLimit.GetMax(fid);
            canBuild = limitOk
                    && IsFootprintInBuildZone(footprintCells)
                    && !IsAnyCellOccupied(footprintCells)
                    && !IsAnyCellOnRail(footprintCells)
                    && !IsBlockedByPhysics(snappedPos, rotatedSize, rotation);

            UpdatePreview(snappedPos, rotation, canBuild);
            return true;
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
                if (slotEffects[i] == null) continue;

                bool isThisSelected = IsBuildMode && hasSelectedSlot
                                   && currentIndex == i
                                   && CurrentSubMode == BuildSubMode.Facility;

                string name = "";
                if (isThisSelected && buildSlots != null && i < buildSlots.Length)
                    name = GetFacilityNameById(buildSlots[i].facilityId);

                slotEffects[i].SetSelected(isThisSelected, name);
            }
        }

        if (railSlotEffect != null)
        {
            bool isRailSelected = IsBuildMode && CurrentSubMode == BuildSubMode.Rail;
            railSlotEffect.SetSelected(isRailSelected, isRailSelected ? "레일 설치" : "");
        }

        // 새 퀵슬롯 스킨 선택 글로우 (slotEffects가 씬 미연결이라 직접 구동)
        if (slotIconUIs != null)
        {
            for (int i = 0; i < slotIconUIs.Length; i++)
            {
                if (slotIconUIs[i] != null)
                {
                    bool sel = (IsBuildMode && hasSelectedSlot && currentIndex == i && CurrentSubMode == BuildSubMode.Facility);
                    slotIconUIs[i].SetSelected(sel);
                }
            }
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

    private string GetFacilityNameById(int facilityId)
    {
        if (facilityId <= 0) return "";
        var data = GetFacilityData(facilityId);
        return data != null ? data.facilityName : "";
    }

    // =====================================================================
    // 동적 해금 슬롯 시스템
    // FacilityUnlockPickup 획득 시 FacilityUnlockManager가 이벤트를 발생시키고
    // 해당 슬롯에 facilityId를 등록 + 아이콘 UI를 갱신한다.
    // =====================================================================

    private void InitDynamicUnlockSlots()
    {
        // 슬롯 배열을 MaxSlots(9)개로 초기화 — 모두 facilityId=0(비어있음)
        int slotCount = FacilityUnlockManager.MaxSlots;
        buildSlots = new BuildSlot[slotCount];
        for (int i = 0; i < slotCount; i++)
            buildSlots[i] = new BuildSlot { facilityId = 0 };

        // 아이콘 UI 초기화 — 잠긴 슬롯은 자물쇠, 이미 해금된 슬롯은 설비 표시
        if (slotIconUIs != null)
        {
            for (int i = 0; i < slotIconUIs.Length; i++)
            {
                var slot = slotIconUIs[i];
                if (slot == null) continue;
                slot.SetSlotNumber(i + 1);
                int facilityId = i + 1;
                bool unlocked = FacilityUnlockManager.Instance != null && FacilityUnlockManager.Instance.IsUnlocked(facilityId);
                if (unlocked)
                {
                    buildSlots[i].facilityId = facilityId;
                    slot.SetFacility(facilityId);
                }
                else
                {
                    slot.Clear();
                }
            }
        }

        // 레일(E) 슬롯 스킨 적용 — 항상 활성, 기존 레일 아이콘 유지
        if (railSlotIcon != null)
        {
            var rq = railSlotIcon.GetComponent<QuickSlotIconUI>();
            if (rq == null) rq = railSlotIcon.gameObject.AddComponent<QuickSlotIconUI>();
            rq.SetRail();
        }

        // 해금 이벤트 구독
        if (FacilityUnlockManager.Instance != null)
            FacilityUnlockManager.Instance.OnFacilityUnlocked += HandleFacilityUnlocked;
        else
            Debug.LogWarning("[BuildManager] FacilityUnlockManager가 씬에 없음. 설비 해금 기능 비활성화.");
    }

    // 설비 해금 시 콜백 — 해당 슬롯에 facilityId 등록 + 아이콘 표시
    private void HandleFacilityUnlocked(int facilityId, int slotIndex)
    {
        if (buildSlots == null || slotIndex < 0 || slotIndex >= buildSlots.Length) return;

        buildSlots[slotIndex].facilityId = facilityId;

        // 즉시 표시하지 않고 '대기' — 다음 건설모드 진입 시 강한 해금 연출로 공개
        if (slotIconUIs != null && slotIndex < slotIconUIs.Length)
            slotIconUIs[slotIndex]?.SetUnlockPending(facilityId);
    }

    // 건설모드 진입 시 대기 중인 해금 슬롯들의 '팡' 연출을 순차 재생
    private void PlayPendingUnlocks()
    {
        if (slotIconUIs == null) return;
        int order = 0;
        for (int i = 0; i < slotIconUIs.Length; i++)
        {
            var slot = slotIconUIs[i];
            if (slot != null && slot.HasPendingUnlock)
                slot.PlayUnlockSequence(order++ * 0.26f);
        }
    }
}