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
    [Tooltip("준비 중 플레이어 주변에 재생할 이펙트(플레이어 자식으로 붙음).")]
    [SerializeField] private GameObject channelVfx;
    [SerializeField] private AudioClip channelSound;
    [SerializeField] private AudioClip arriveSound;

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

    // ── 공개 API (보상/HUD 연동용) ────────────────────────────────────
    public int Level => level;
    public bool IsOwned => level > 0;
    public float CooldownRemaining => _cooldownRemaining;
    public float CooldownTotal => CooldownSeconds();
    public bool IsChanneling => _channeling;
    public bool IsReady => IsOwned && _cooldownRemaining <= 0f && !_channeling;
    public KeyCode UseKey => useKey;
    public Sprite HudIcon => hudIcon;

    /// <summary>레벨 설정(추후 시간에너지 보상에서 호출). 0~3.</summary>
    public void SetLevel(int lv) => level = Mathf.Clamp(lv, 0, 3);
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
        ReturnStoneHudUI.Build(this, bar, hudSize);
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

        if (useKey != KeyCode.None && Input.GetKeyDown(useKey) && !PlayerInputComponent.IsBlocked)
            TryUse();
    }

    // ── 사용 ──────────────────────────────────────────────────────────
    /// <summary>귀환석 사용 시도. 조건 충족 시 준비→귀환 시작. 실제 아이템/퀵슬롯 사용에서 호출하면 된다.</summary>
    public bool TryUse()
    {
        if (_channeling) return false;
        if (!IsOwned) { ToastManager.Warning("귀환석을 보유하고 있지 않습니다."); return false; }

        var player = ResolvePlayer();
        if (player == null) return false;

        if (player.Stat != null && player.Stat.IsDead) return false;

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

        bool cancelled = false;
        Action onHurt = () => { if (cancelOnDamage) cancelled = true; };
        Action onDead = () => cancelled = true;
        if (stat != null) { stat.OnHurt += onHurt; stat.OnDead += onDead; }

        GameObject vfx = SpawnChannelVfx(player);
        PlaySound(channelSound, player.transform.position);
        ToastManager.Info("귀환 준비 중...");

        float t = 0f;
        yield return null;   // 발동 입력이 같은 프레임에 '행동 취소'로 잡히지 않게 한 프레임 건너뜀
        while (t < channelDuration && !cancelled)
        {
            if (cancelOnAction && HasActionInput(player.Input)) { cancelled = true; break; }
            t += Time.deltaTime;
            yield return null;
        }

        if (stat != null) { stat.OnHurt -= onHurt; stat.OnDead -= onDead; }
        if (vfx != null) Destroy(vfx);

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

        TeleportPlayer(player.transform, ReturnDest());
        PlaySound(arriveSound, player.transform.position);

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
        return Instantiate(channelVfx, player.transform.position, Quaternion.identity, player.transform);
    }

    private void PlaySound(AudioClip clip, Vector3 pos)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, pos);
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
