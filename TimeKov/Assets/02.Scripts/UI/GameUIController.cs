// =====================================================================
// GameUIController.cs
// UI 상태 통합 관리
// - ESC → 설정창 직접 열기
// - UI 열려있는 동안 다른 UI 열기 차단
// - 커서 / 게임플레이 입력 플래그 관리
// =====================================================================

using UnityEngine;

public class GameUIController : MonoBehaviour
{
    public static GameUIController Instance { get; private set; }

    /// <summary>게임플레이 입력이 활성화된 상태인지 (UI 미오픈 = true)</summary>
    public static bool GameplayInputEnabled { get; private set; } = true;

    public enum UIState
    {
        None,
        Settings,     // ESC로 열리는 설정·일시정지 통합창
        Factory,      // 설비 UI (MachineInteraction이 직접 관리)
        Build,        // 건설 모드 (BuildManager가 직접 관리)
        Quest,        // 퀘스트 수락/조회 팝업 (J키)
        Inventory,    // 인벤토리 (TAB키, InventoryUIController가 루트 가시성 관리)
        PlayerStat,   // 플레이어 스탯창 (C키)
        CoreUpgrade,  // 코어 강화 UI (터미널 상호작용)
        ChestOpen     // 상자 오픈 팝업
    }

    [Header("Settings Panel")]
    [Tooltip("ESC로 열리는 설정창 루트 패널")]
    public GameObject settingsPanel;

    [Header("Build Mode UI")]
    [Tooltip("건설 모드에서 표시되는 퀵슬롯 UI")]
    public GameObject quickSlotUI;

    [Header("Quest")]
    [Tooltip("J키로 여닫는 퀘스트 수락/조회 팝업 패널")]
    public GameObject questPanel;

    [Tooltip("항상 화면에 표시되는 퀘스트 HUD — CanvasGroup 필수 (SetActive 대신 alpha로 숨겨 QuestPanelUI 구독 유지)")]
    public GameObject questHud;

    [Tooltip("머신/인벤 등 큰 패널이 열렸을 때 퀘스트 HUD 알파 (덜 거슬리게). 0~1, 보이되 흐리게.")]
    [Range(0f, 1f)]
    public float questHudDimmedAlpha = 0.4f;

    [Header("Player Stat (C키)")]
    [Tooltip("C키로 여닫는 플레이어 스탯창 패널")]
    public GameObject statPanel;

    [Header("Player HUD")]
    [Tooltip("HP·스태미나 등 플레이어 상태 HUD — 다른 UI가 열리면 숨겨짐")]
    public GameObject playerHud;

    [Header("Player Info Set (좌하단 HUD + C키 스탯창)")]
    [Tooltip("좌하단 플레이어 정보 그룹(PlayerHud). HP/시간/DECAY 창과 C키 스탯창(Character_stat)을\n" +
             "모두 자식으로 포함하는 부모 오브젝트를 연결.\n" +
             "설정창·건설(탑뷰) 모드에서만 통째로 숨겨지고, 그 외에는 항상 표시됨.\n" +
             "둘이 같은 부모라서 항상 같은 레이어에서 같이 뜨고 같이 숨겨짐(세트).")]
    public GameObject playerInfoRoot;

    private UIState _currentState = UIState.None;
    private BuildManager _buildManager;
    private CanvasGroup _questHudGroup;
    private bool _tutorialCoachActive;   // 튜토리얼 코치마크(오버레이) 표시 중 — 퀘스트 트래커 숨김

    // ── 초기화 ───────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _buildManager = FindAnyObjectByType<BuildManager>();

        if (questHud != null)
            _questHudGroup = questHud.GetComponent<CanvasGroup>();

        // 튜토리얼 스포트라이트 "status_panel" 타깃 = C 스탯창의 보이는 배경(StatBG).
        // statPanel(Character_stat)은 100x100 앵커 노드라 그대로 쓰면 박스가 작게 잡힌다.
        // 실제 창 크기인 StatBG(600x350)를 우선 등록하고, 못 찾으면 statPanel로 폴백.
        if (statPanel != null)
        {
            var statRect = statPanel.transform.Find("StatBG") as RectTransform
                        ?? statPanel.GetComponent<RectTransform>();
            if (statRect != null) TutorialOverlay.RegisterTarget("status_panel", statRect);
        }
    }

    protected virtual void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            HandleEscape();

        if (Input.GetKeyDown(KeyCode.J))
            ToggleQuest();

        if (Input.GetKeyDown(KeyCode.C))
            TogglePlayerStat();
    }

    // LateUpdate에서 커서 상태를 강제 적용
    // — ESC 키를 누르면 Unity 에디터가 Update() 이후 커서를 강제 해제하므로
    //   LateUpdate에서 덮어써야 ESC로 닫아도 커서가 정상적으로 사라짐
    protected virtual void LateUpdate()
    {
        RefreshCursorState();
    }

    // ── 공개 쿼리 ────────────────────────────────────────────────────

    public UIState GetCurrentState() => _currentState;

    /// <summary>어떤 UI든 열려있으면 true — 카메라 줌, 다른 UI 열기 차단에 사용</summary>
    public bool IsUIBlocking() => _currentState != UIState.None;

    // ── ESC 처리 ─────────────────────────────────────────────────────

    public void HandleEscape()
    {
        // 인벤 내부 팝업이 우선 (인벤 폴더 비침습 약속 — 외부에서 처리)
        if (_currentState == UIState.Inventory)
        {
            var inv = InventoryUIController.Instance;
            if (inv != null && inv.TryCloseTopPopup()) return;
        }

        // WindowManager가 부착되어 있으면 통합 디스패치 위임
        // (Modal top → 최근 Window → defaultEscapeWindowId 순으로 처리)
        // 처리할 게 있었으면 true 반환 → 폴백 건너뜀
        var wm = TimeKov.UI.WindowManager.I;
        if (wm != null && wm.HandleEscape())
            return;

        // ── 폴백 (WindowManager 미부착 OR 등록된 창이 없을 때 기존 로직) ──
        if (_buildManager == null)
            _buildManager = FindAnyObjectByType<BuildManager>();

        // 건설 모드 ESC는 BuildManager가 처리
        if (_buildManager != null && _buildManager.IsBuildMode)
        {
            _buildManager.ExitBuildMode();
            return;
        }

        if (_currentState == UIState.Inventory)
        {
            InventoryUIController.Instance?.Close();
            return;
        }

        if (_currentState == UIState.None)
        {
            OpenSettings();
            return;
        }

        CloseAll();
    }

    // ── 전체 닫기 ────────────────────────────────────────────────────

    public void CloseAll()
    {
        // PlayerStat도 ESC로 같이 닫음 (단, _currentState와 독립이라 SetState 밖에서 처리)
        if (statPanel != null) statPanel.SetActive(false);

        // 코어 강화 UI — SetState 전에 패널 먼저 숨김 (순환 호출 방지)
        CoreUpgradeUI.Instance?.HidePanel();
        // 상자 오픈 UI
        ChestOpenUI.Instance?.HidePanel();

        // SetState가 WindowManager mirror도 함께 처리
        SetState(UIState.None);

        // PlayerStat은 별도 채널이라 명시적 mirror
        TimeKov.UI.WindowManager.I?.Close("PlayerStat");
    }

    /// <summary>이전 API 호환 — CloseAll()과 동일</summary>
    public void CloseAllUI() => CloseAll();

    // ── 설정창 ───────────────────────────────────────────────────────

    public void OpenSettings()
    {
        if (_currentState != UIState.None && _currentState != UIState.Settings) return;
        SetState(UIState.Settings);
    }

    public void CloseSettings()
    {
        if (_currentState != UIState.Settings) return;
        SetState(UIState.None);
    }

    // ── 코어 강화 UI ─────────────────────────────────────────────────

    public void OpenCoreUpgradeUI()
    {
        if (_currentState != UIState.None) return;
        SetState(UIState.CoreUpgrade);
    }

    public void CloseCoreUpgradeUI()
    {
        if (_currentState != UIState.CoreUpgrade) return;
        SetState(UIState.None);
    }

    // ── 상자 오픈 UI ──────────────────────────────────────────────────

    public void OpenChestUI()
    {
        if (_currentState != UIState.None) return;
        SetState(UIState.ChestOpen);
    }

    public void CloseChestUI()
    {
        if (_currentState != UIState.ChestOpen) return;
        SetState(UIState.None);
    }

    // ── 설비 UI ──────────────────────────────────────────────────────

    public void OpenFactoryUI()
    {
        if (_currentState != UIState.None) return;
        SetState(UIState.Factory);
    }

    public void CloseFactoryUI()
    {
        if (_currentState != UIState.Factory) return;
        SetState(UIState.None);
    }

    // ── 퀘스트 팝업 ──────────────────────────────────────────────────

    public void ToggleQuest()
    {
        if (_currentState == UIState.Quest) { CloseAll(); return; }
        if (_currentState != UIState.None) return;
        SetState(UIState.Quest);
    }

    // ── 플레이어 스탯창 ──────────────────────────────────────────────
    // PlayerStat은 정보 표시용 — _currentState와 독립으로 동작.
    // 다른 UI(인벤/팩토리/빌드 등) 열려있어도 같이 켜질 수 있음.

    public void TogglePlayerStat()
    {
        if (statPanel == null) return;

        // 설정창/건설(탑뷰) 모드에선 플레이어 정보 세트(playerInfoRoot)가 통째로 숨겨지므로
        // C키 입력을 무시한다. (숨겨진 상태에서 토글하면 복귀 시 의도치 않게 켜져 보이는 문제 방지)
        if (_currentState == UIState.Settings || _currentState == UIState.Build) return;

        statPanel.SetActive(!statPanel.activeSelf);

        // WindowManager mirror — PlayerStat은 _currentState와 독립이라 SetState 안 거침
        var wm = TimeKov.UI.WindowManager.I;
        if (wm != null)
        {
            if (statPanel.activeSelf) wm.Open("PlayerStat");
            else                       wm.Close("PlayerStat");
        }
    }

    // ── 상태 직접 설정 (BuildManager 연동용) ─────────────────────────
    // 모든 _currentState 변경의 단일 진입점.
    // 여기서 WindowManager에 mirror 통보 — 어댑터가 등록되어 있으면 자동 반영, 없으면 no-op.

    public void SetState(UIState newState)
    {
        var oldState = _currentState;
        _currentState = newState;
        ApplyState();

        // WindowManager mirror
        var wm = TimeKov.UI.WindowManager.I;
        if (wm != null)
        {
            var oldId = MapStateToWindowId(oldState);
            var newId = MapStateToWindowId(newState);
            if (oldId != newId)
            {
                if (!string.IsNullOrEmpty(oldId)) wm.Close(oldId);
                if (!string.IsNullOrEmpty(newId)) wm.Open(newId);
            }
        }
    }

    // UIState → WindowManager.WindowId 매핑. None은 등록 안 됨(null 반환).
    static string MapStateToWindowId(UIState s)
    {
        switch (s)
        {
            case UIState.Settings:    return "Settings";
            case UIState.Factory:     return "Factory";
            case UIState.Build:       return "BuildMode";
            case UIState.Quest:       return "QuestPopup";
            case UIState.Inventory:   return "Inventory";
            case UIState.CoreUpgrade: return "CoreUpgrade";
            case UIState.ChestOpen:   return "ChestOpen";
            default:                  return null;   // None, PlayerStat은 별도 채널
        }
    }

    // ── 호환 래퍼 (구 Pause 시스템) ──────────────────────────────────

    /// <summary>구 PauseMenuManager 호환용</summary>
    public void OpenPauseSettings()  => OpenSettings();
    /// <summary>구 PauseMenuManager 호환용</summary>
    public void ClosePauseSettings() => CloseSettings();

    // ── 퀘스트 HUD / 튜토리얼 코치마크 ───────────────────────────────

    /// <summary>튜토리얼 코치마크(오버레이) 표시 중 퀘스트 트래커 숨김. TutorialOverlay가 호출.</summary>
    public void SetTutorialCoachActive(bool active)
    {
        _tutorialCoachActive = active;
        RefreshQuestHudAlpha();
        // 코치마크 동안 이동/공격/대시/스킬/카메라 차단 (C키·오버레이 진행은 raw Input이라 영향 없음).
        // 넘기면 Hide -> SetTutorialCoachActive(false)로 복귀.
        SetGameplayInputEnabled(_currentState == UIState.None && !_tutorialCoachActive);
    }

    // 퀘스트 HUD 알파 — 코치마크 우선, 그다음 _currentState 기준 (SetActive 금지: QuestPanelUI 구독 유지).
    void RefreshQuestHudAlpha()
    {
        if (_questHudGroup == null) return;

        if (_tutorialCoachActive)
            _questHudGroup.alpha = 0f;   // 코치마크 중 — 트래커 숨김(배너와 중복 방지)
        else if (_currentState == UIState.None || _currentState == UIState.Build || _currentState == UIState.CoreUpgrade)
            _questHudGroup.alpha = 1f;   // 게임플레이·건설·코어강화(좌상단 안 가림) 안내
        else if (_currentState == UIState.Inventory || _currentState == UIState.Factory)
            _questHudGroup.alpha = questHudDimmedAlpha;   // 큰 패널 겹침 — 흐리게
        else
            _questHudGroup.alpha = 0f;

        // HUD는 정보 표시 전용 — 클릭 절대 가로채지 않음 (공장 드래그가 뒤로 빠지는 문제 방지)
        _questHudGroup.interactable = false;
        _questHudGroup.blocksRaycasts = false;
    }

    // ── 내부 상태 적용 ───────────────────────────────────────────────

    protected virtual void ApplyState()
    {
        // 설정창
        SetPanelActive(settingsPanel, _currentState == UIState.Settings);

        // 퀵슬롯 UI — 건설 모드에서만 표시
        SetPanelActive(quickSlotUI, _currentState == UIState.Build);

        // 퀘스트 팝업 — Quest 상태에서만 표시
        SetPanelActive(questPanel, _currentState == UIState.Quest);

        // 플레이어 스탯창은 _currentState와 독립이라 여기서 안 건드림 (TogglePlayerStat이 직접 관리)

        // 퀘스트 HUD 알파 (코치마크/상태 기준) — Build 모드에서도 퀘스트는 계속 보임(튜토 안내용)
        RefreshQuestHudAlpha();

        // 플레이어 HUD — 다른 UI가 열리면 숨김 (PlayerStat은 _currentState와 독립이라 영향 없음)
        if (playerHud != null)
            playerHud.SetActive(_currentState == UIState.None);

        // 플레이어 정보 세트(좌하단 HP/시간 창 + C키 스탯창) — 설정창/건설(탑뷰)에서만 통째로 숨김.
        // playerInfoRoot(PlayerHud 그룹)가 좌하단 창과 Character_stat(C키)을 모두 자식으로 가지므로
        // 이 그룹 하나만 켜고 끄면 둘이 항상 같은 레이어에서 같이 뜨고 같이 숨겨짐(세트 보장).
        // C키 스탯창은 자식이라 부모가 꺼지면 자동으로 함께 숨겨진다.
        if (playerInfoRoot != null)
        {
            bool showPlayerInfo = _currentState != UIState.Settings
                               && _currentState != UIState.Build;
            playerInfoRoot.SetActive(showPlayerInfo);
        }

        // 커서 + 입력 플래그
        // None 상태 + 코치마크(튜토 오버레이) 아님일 때만 게임플레이 입력 활성화
        bool gameplay = _currentState == UIState.None && !_tutorialCoachActive;
        SetGameplayInputEnabled(gameplay);

        // 설정창이 열릴 때만 시간 정지
        Time.timeScale = (_currentState == UIState.Settings) ? 0f : 1f;
    }

    // ── 패널 활성/비활성 헬퍼 ────────────────────────────────────────

    /// <summary>
    /// UIUnfoldEffect가 있으면 Close() 애니메이션을 거쳐 비활성화.
    /// 없으면 바로 SetActive() 처리.
    /// </summary>
    protected void SetPanelActive(GameObject panel, bool active)
    {
        if (panel == null) return;

        if (active)
        {
            // 열기: SetActive(true) → UIUnfoldEffect.OnEnable()이 열기 애니메이션 실행
            panel.SetActive(true);
        }
        else
        {
            // 닫기: UIUnfoldEffect가 있으면 애니메이션 후 SetActive(false)
            var unfold = panel.GetComponent<UIUnfoldEffect>();
            if (unfold != null && panel.activeInHierarchy)
                unfold.Close();
            else
                panel.SetActive(false);
        }
    }

    // ── 커서 / 입력 관리 ─────────────────────────────────────────────

    private void SetGameplayInputEnabled(bool enabled)
    {
        GameplayInputEnabled = enabled;
        PlayerInputComponent.IsBlocked = !enabled;  // 인벤토리 등 UI 오픈 시 플레이어 입력 차단
        Cursor.visible = !enabled;
        Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
    }

    private void RefreshCursorState()
    {
        // 사망 오버레이가 열려있으면 커서 강제 표시 (부활 버튼 클릭 필요)
        if (DeathOverlayUI.IsOpen)
        {
            if (!Cursor.visible) Cursor.visible = true;
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            return;
        }

        // 탑뷰(건설) 모드는 항상 커서 표시 — TopViewPanCamera 우선
        if (_buildManager != null && _buildManager.IsTopViewMode)
        {
            if (!Cursor.visible) Cursor.visible = true;
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            return;
        }

        bool showCursor = _currentState != UIState.None;
        if (Cursor.visible != showCursor)
            Cursor.visible = showCursor;

        CursorLockMode target = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
        if (Cursor.lockState != target)
            Cursor.lockState = target;
    }
}
