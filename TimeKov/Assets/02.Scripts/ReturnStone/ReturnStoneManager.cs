using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 귀환석 — 테마 맵 어디서든 사용해 기지로 긴급 복귀하는 개인용 수단.
//   흐름: 사용 → 귀환 준비(채널링, 취소 가능) → 검은 페이드 → 기지로 텔레포트.
//   맵이 단일 World.unity 안 지역이라 씬 로드 없이 트랜스폼만 이동한다(워프와 동일).
//
//   레벨(1~3)에 따라 쿨타임 15/10/5분. 쿨타임은 '결계 밖'(PlayerStatComponent.IsInBase == false)에서
//   보낸 시간으로만 차감된다 — 기지 안에 있는 동안은 멈춘다.
//
//   ※ 레벨 획득은 추후 시간에너지 보상과 연동. 지금은 인스펙터/공개 API(SetLevel)로 설정하는 시스템만.
public class ReturnStoneManager : MonoBehaviour, ISaveable
{
    public static ReturnStoneManager Instance { get; private set; }

    [Header("보유 레벨 (0=미보유, 1~3). 추후 시간에너지 보상으로 설정")]
    [SerializeField] private int level = 0;

    [Header("쿨타임 (레벨별, 분) — 결계 밖 시간으로만 차감")]
    [SerializeField] private float cooldownLv1Min = 15f;
    [SerializeField] private float cooldownLv2Min = 10f;
    [SerializeField] private float cooldownLv3Min = 5f;

    [Header("귀환 준비 (채널링)")]
    [Tooltip("사용 후 실제 귀환까지 대기 시간(초).")]
    [SerializeField] private float channelDuration = 5f;
    [Tooltip("준비 중 피격되면 취소.")]
    [SerializeField] private bool cancelOnDamage = true;
    [Tooltip("준비 중 이동/공격/대시/스킬/점프 등 행동하면 취소(사용 중 정지 상태 유지).")]
    [SerializeField] private bool cancelOnAction = true;

    [Header("도착 / 페이드")]
    [Tooltip("기지 복귀 지점. 비우면 RespawnManager.RespawnPoint 사용.")]
    [SerializeField] private Transform baseReturnPoint;
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private float blackHold = 0.15f;

    [Header("연출 (선택)")]
    [Tooltip("준비 중 플레이어 발밑(지면)에 재생할 이펙트(플레이어 자식으로 붙음). 예) Sci-fi buff sphere")]
    [SerializeField] private GameObject channelVfx;
    [Tooltip("채널 VFX 높이 미세조정(+위/-아래). 기본은 발밑 지면.")]
    [SerializeField] private float channelVfxYOffset = 0f;
    [Tooltip("기지 도착 시 도착 지점에 재생할 이펙트. 예) Spawn_06")]
    [SerializeField] private GameObject arriveVfx;
    // 사운드는 GameSfxConfig 로 통합 관리 — 채널 루프(SfxId.ReturnStoneChannelLoop) / 도착(SfxId.ReturnStoneArrive).

    [Header("입력")]
    [Tooltip("이 키로도 귀환석 사용(HUD 버튼과 별개, 선택). G는 즉시완료로 이미 쓰이므로 다른 키 권장.")]
    [SerializeField] private KeyCode useKey = KeyCode.H;

    [Header("HUD (스킬바에 통합)")]
    [Tooltip("실행 시 스킬바 왼쪽(V 옆)에 슬롯을 자동 생성.")]
    [SerializeField] private bool autoBuildHud = true;
    [Tooltip("HUD 아이콘. 비우면 절차적 크리스탈 젬.")]
    [SerializeField] private Sprite hudIcon;
    [Tooltip("슬롯 지름. V/RMB(78)보다 약간 작게 해 차별점(기본 68).")]
    [SerializeField] private float hudSize = 68f;

    private float _cooldownRemaining;   // 남은 쿨타임(초). 결계 밖에서만 감소.
    private bool _channeling;
    private CanvasGroup _fadeCg;
    private Player _player;
    private RespawnManager _respawn;
    private ReturnStoneHudUI _hud;   // 스킬바 슬롯(잠금 오버레이 흔들기용)
    private bool _revealPending;      // 첫 해금 연출 대기 — 전송 UI(풀스크린) 닫히면 발동

    // ── 공개 API (보상/HUD 연동용) ────────────────────────────────────
    public int Level => level;
    public bool IsOwned => level > 0;
    public float CooldownRemaining => _cooldownRemaining;
    public float CooldownTotal => CooldownSeconds();
    public bool IsChanneling => _channeling;
    public bool IsReady => IsOwned && _cooldownRemaining <= 0f && !_channeling;
    public KeyCode UseKey => useKey;
    public Sprite HudIcon => hudIcon;

    /// <summary>레벨 설정(시간에너지 보상에서 호출). 0~3. 실제 상승 시 발견팝업/HUD용 이벤트 발화.</summary>
    public void SetLevel(int lv)
    {
        int prev = level;
        level = Mathf.Clamp(lv, 0, 3);
        if (level > prev)
        {
            GameEvents.RaiseReturnStoneChanged(level);   // 첫 획득/상승 순간만(복원은 level 직접 세팅이라 무발화)
            if (prev == 0) _revealPending = true;        // 첫 해금 = 팡 연출 + 토스트 + 소개 영상(전송 UI 닫는 순간)
        }
    }
    public void Upgrade() => SetLevel(level + 1);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SaveSlotManager.Instance?.Register(this);
        RestoreFromSave();
        BuildFadeOverlay();
    }

    private void Start()
    {
        if (autoBuildHud) StartCoroutine(BuildHudRoutine());
    }

    // 스킬바(PlayerHudUI가 런타임 생성)가 준비되면 그 왼쪽(V 옆)에 슬롯을 붙인다.
    private IEnumerator BuildHudRoutine()
    {
        float timeout = 5f;
        SkillBarUI bar;
        while ((bar = FindFirstObjectByType<SkillBarUI>()) == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        _hud = ReturnStoneHudUI.Build(this, bar, hudSize);
    }

    private void OnDestroy()
    {
        SaveSlotManager.Instance?.Unregister(this);
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // 쿨타임은 결계 밖에서 보낸 시간으로만 차감.
        if (_cooldownRemaining > 0f && PlayerOutsideBarrier())
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Time.deltaTime);

        // 첫 해금 연출 — 보상은 전송 단말(풀스크린 UI) 안에서 지급되므로, 그 UI가 닫혀
        //   스킬바가 보이는 순간에 팡 -> 토스트 -> 영상을 순서대로 낸다(전송 UI에 묻히지 않게).
        if (_revealPending)
        {
            var gui = GameUIController.Instance;
            bool uiOpen = gui != null && gui.GetCurrentState() != GameUIController.UIState.None;
            if (!uiOpen)
            {
                _revealPending = false;
                StartCoroutine(UnlockRevealRoutine());
            }
        }

        if (useKey != KeyCode.None && Input.GetKeyDown(useKey) && !PlayerInputComponent.IsBlocked)
            TryUse();
    }

    // 해금 연출 순서: 자물쇠 팡(HUD) + 토스트 -> 잠깐 뒤 소개 영상. 영상이 팡/토스트를 덮지 않게 간격을 둔다.
    private IEnumerator UnlockRevealRoutine()
    {
        _hud?.PlayUnlockCelebration();
        ToastManager.Success("귀환석을 획득했습니다! H로 기지에 귀환할 수 있습니다.");
        yield return new WaitForSecondsRealtime(1.2f);
        DiscoveryCueManager.TryFire("returnstone");   // 소개 영상(이미 봤으면 무해하게 스킵)
    }

    // ── 사용 ──────────────────────────────────────────────────────────
    /// <summary>귀환석 사용 시도. 조건 충족 시 준비→귀환 시작. 실제 아이템/퀵슬롯 사용에서 호출하면 된다.</summary>
    public bool TryUse()
    {
        if (_channeling) return false;
        if (!IsOwned) { _hud?.NudgeLock(); ToastManager.Warning("귀환석을 보유하고 있지 않습니다."); return false; }

        var player = ResolvePlayer();
        if (player == null) return false;

        if (player.Stat != null && player.Stat.IsDead) return false;

        // 스킬/공격 모션 중에는 사용 불가 — 즉발기는 이펙트를 못 되돌리므로 모션이 끝난 뒤 사용.
        if (player.Skill != null && player.Skill.IsExecuting)
        {
            ToastManager.Warning("행동 중에는 귀환석을 사용할 수 없습니다.");
            return false;
        }

        // 이동 중에는 사용 불가 — 채널은 제자리에서만. (가만히 선 뒤 사용)
        if (player.Input != null && player.Input.MoveInputRaw.sqrMagnitude > 0.01f)
        {
            ToastManager.Warning("이동 중에는 귀환석을 사용할 수 없습니다.");
            return false;
        }

        if (player.Stat != null && player.Stat.IsInBase)
        {
            ToastManager.Info("기지 안에서는 귀환석이 필요 없습니다.");
            return false;
        }
        if (_cooldownRemaining > 0f)
        {
            ToastManager.Warning($"귀환석 재사용까지 약 {Mathf.CeilToInt(_cooldownRemaining / 60f)}분");
            return false;
        }

        StartCoroutine(UseRoutine(player));
        return true;
    }

    private IEnumerator UseRoutine(Player player)
    {
        _channeling = true;
        var stat = player.Stat;

        // 사용 중 정지: 시작 시 속도 제거(제자리 유지). 이후 이동 입력은 취소로 처리된다.
        FreezePlayer(player);
        // 진행 중이던 액션을 실제로 중단 — 애니만 끊기고 이펙트/사운드가 계속 나가는 것 방지.
        player.Skill?.Interrupt();    // 공격/스킬 코루틴 중단(미발생 히트/이펙트/스윙 사운드 정지)
        player.Dash?.CancelDash();    // 대시 중단(EndDash가 채널 모션 덮는 것 방지)
        player.Anim?.PlayChannel();   // 채널링 모션(웅크려 집중)

        bool cancelled = false;
        Action onHurt = () => { if (cancelOnDamage) cancelled = true; };
        Action onDead = () => cancelled = true;
        if (stat != null) { stat.OnHurt += onHurt; stat.OnDead += onDead; }

        GameObject vfx = SpawnChannelVfx(player);
        StartChannelLoop();   // 준비 중 도는 루프음(SfxId.ReturnStoneChannelLoop)
        CastGaugeUI.Ensure()?.Begin("귀환 중", HudIcon);   // 워프와 같은 상단 게이지("귀환 중" 표시 = 별도 토스트 불필요)

        float t = 0f;
        yield return null;   // 발동 입력이 같은 프레임에 '행동 취소'로 잡히지 않게 한 프레임 건너뜀
        while (t < channelDuration && !cancelled)
        {
            if (cancelOnAction && HasActionInput(player.Input)) { cancelled = true; break; }
            t += Time.deltaTime;
            CastGaugeUI.Instance?.Report(t / channelDuration, channelDuration - t);
            yield return null;
        }

        if (stat != null) { stat.OnHurt -= onHurt; stat.OnDead -= onDead; }
        if (vfx != null) Destroy(vfx);
        StopChannelLoop();   // 준비 종료(취소/완료 공통) — 루프음 정지
        CastGaugeUI.Instance?.Hide();   // 게이지 숨김(취소/완료 공통)
        // 사망 시엔 사망 모션에 맡기고, 그 외에만 채널 모션 해제(취소/완료 공통).
        if (stat == null || !stat.IsDead) player.Anim?.StopChannel();

        if (cancelled || (stat != null && stat.IsDead))
        {
            ToastManager.Warning("귀환이 취소되었습니다.");
            _channeling = false;
            yield break;
        }

        yield return WarpToBase(player);

        // 귀환 성공 → 쿨타임 시작(결계 밖에서만 감소).
        _cooldownRemaining = CooldownSeconds();
        SaveSlotManager.Instance?.SaveActive();
        _channeling = false;
    }

    private static void FreezePlayer(Player player)
    {
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
    }

    // 채널링 취소용 '행동' 입력 감지 — 이동/점프/공격/대시/스킬.
    private static bool HasActionInput(PlayerInputComponent i)
    {
        if (i == null) return false;
        return i.MoveInputRaw.sqrMagnitude > 0.01f
            || i.JumpPressed || i.AttackPressed || i.DashPressed
            || i.Skill1Pressed || i.Skill2Pressed || i.Skill3Pressed;
    }

    private IEnumerator WarpToBase(Player player)
    {
        PlayerInputComponent.IsBlocked = true;

        yield return Fade(0f, 1f);
        yield return new WaitForSecondsRealtime(blackHold);

        Vector3 dest = ReturnDest();
        TeleportPlayer(player.transform, dest);
        SpawnAt(arriveVfx, dest);
        GameSfx.Play(SfxId.ReturnStoneArrive);   // 기지 도착음(플레이어 개인 이벤트 → 2D)

        yield return Fade(1f, 0f);
        PlayerInputComponent.IsBlocked = false;
    }

    // ── 보조 ──────────────────────────────────────────────────────────
    private float CooldownSeconds()
    {
        float min = level switch
        {
            1 => cooldownLv1Min,
            2 => cooldownLv2Min,
            3 => cooldownLv3Min,
            _ => cooldownLv1Min,
        };
        return Mathf.Max(0f, min) * 60f;
    }

    private bool PlayerOutsideBarrier()
    {
        var p = ResolvePlayer();
        return p != null && p.Stat != null && !p.Stat.IsInBase;
    }

    private Player ResolvePlayer()
    {
        if (_player == null) _player = FindFirstObjectByType<Player>();
        return _player;
    }

    private Vector3 ReturnDest()
    {
        if (baseReturnPoint != null) return baseReturnPoint.position;
        if (_respawn == null) _respawn = FindFirstObjectByType<RespawnManager>();
        if (_respawn != null && _respawn.RespawnPoint != null) return _respawn.RespawnPoint.position;
        return transform.position;
    }

    private void TeleportPlayer(Transform player, Vector3 dest)
    {
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = dest;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        player.position = dest;
    }

    private GameObject SpawnChannelVfx(Player player)
    {
        if (channelVfx == null) return null;
        // 플레이어 트랜스폼 원점이 발이 아니라 골반쯤이라, 콜라이더 바닥(발밑=지면)에 스폰한다.
        Vector3 pos = FeetPosition(player) + Vector3.up * channelVfxYOffset;
        return Instantiate(channelVfx, pos, Quaternion.identity, player.transform);
    }

    private static Vector3 FeetPosition(Player player)
    {
        var p = player.transform.position;
        var col = player.GetComponentInChildren<Collider>();
        return col != null ? new Vector3(p.x, col.bounds.min.y, p.z) : p;
    }

    private void SpawnAt(GameObject prefab, Vector3 pos)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, pos, Quaternion.identity);
        Destroy(go, 5f);
    }

    // 채널 루프음 — 클립은 GameSfxConfig(SfxId.ReturnStoneChannelLoop)에서, 재생은 이 로컬 2D 소스에서.
    private AudioSource _channelLoop;

    private void StartChannelLoop()
    {
        if (!GameSfx.TryGet(SfxId.ReturnStoneChannelLoop, out var clip, out var vol) || clip == null) return;
        if (_channelLoop == null)
        {
            var go = new GameObject("[ReturnChannelLoop]");
            go.transform.SetParent(transform, false);
            _channelLoop = go.AddComponent<AudioSource>();
            _channelLoop.spatialBlend = 0f;   // 2D (플레이어 개인 연출)
            _channelLoop.loop = true;
            _channelLoop.playOnAwake = false;
        }
        _channelLoop.clip = clip;
        _channelLoop.volume = vol * GlobalSettingsManager.CurrentSFXVolume;
        _channelLoop.Play();
    }

    private void StopChannelLoop()
    {
        if (_channelLoop != null && _channelLoop.isPlaying) _channelLoop.Stop();
    }

    private IEnumerator Fade(float from, float to)
    {
        if (_fadeCg == null) yield break;
        _fadeCg.blocksRaycasts = true;
        yield return CoreUtilities.FadeUnscaled(_fadeCg, from, to, fadeDuration);
        _fadeCg.blocksRaycasts = _fadeCg.alpha > 0.99f;
    }

    // 전체 화면 검은 오버레이(런타임 생성, 씬 세팅 불필요).
    private void BuildFadeOverlay()
    {
        var go = new GameObject("[ReturnFade]");
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        var imgGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        imgGo.GetComponent<Image>().color = Color.black;
        _fadeCg = imgGo.AddComponent<CanvasGroup>();
        _fadeCg.alpha = 0f; _fadeCg.blocksRaycasts = false; _fadeCg.interactable = false;
    }

    // ── 저장 (ISaveable) ──────────────────────────────────────────────
    public void Capture(GameSaveData data)
    {
        if (data == null) return;
        data.returnStoneLevel = level;
        data.returnStoneCooldownRemaining = _cooldownRemaining;
    }

    private void RestoreFromSave()
    {
        var mgr = SaveSlotManager.Instance;
        if (mgr == null || !mgr.HasActiveSlot) return;
        // 레벨은 감소하지 않으므로 저장값이 있을 때만 덮어씀. 저장값 0이면 인스펙터 값 유지
        // → 보상 연동 전(레벨 지급 수단 없음)에도 인스펙터로 레벨 세팅해 테스트 가능.
        if (mgr.Data.returnStoneLevel > 0) level = mgr.Data.returnStoneLevel;
        _cooldownRemaining = mgr.Data.returnStoneCooldownRemaining;
    }
}
