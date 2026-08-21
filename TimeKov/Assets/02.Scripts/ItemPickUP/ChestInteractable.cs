// =====================================================================
// ChestInteractable.cs
// 파밍 상자 — F로 "걸어두면" openTime초 뒤 '수령 가능'(자리 비워도 카운트),
// 돌아와서 F로 수령(전리품 패널). 기다리기 싫으면 G로 즉시완료(HP=시간 소모).
// 한 번 연 상자는 전리품이 남아있는 동안 F로 쿨 없이 즉시 다시 열린다.
// 다 비우면 다시 걸어둬야(쿨) 채워진다.
// =====================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChestInteractable : MonoBehaviour, IInstantInteractable, ISaveable
{
    [Header("드롭 설정")]
    [Tooltip("DropTable의 sourceId (예: LC_LOOT). sourceType=Chest 행과 매칭됨")]
    [SerializeField] private string sourceId = "LC_LOOT";

    [Header("비주얼 (선택)")]
    [SerializeField] private GameObject closedVisual;
    [SerializeField] private GameObject openedVisual;

    [Header("인터랙션")]
    [Tooltip("기지 결계 밖에서도 열 수 있는지")]
    [SerializeField] private bool requireBase = false;

    [Tooltip("★기믹(레이저 결계 등)으로 여는 상자용. 체크하면 처음부터 '잠금 해제' 상태로 시작 —\n" +
             "걸어두기(F 대기)/즉시완료(G, HP 소모) 없이, 다가가 F 한 번으로 바로 전리품을 연다.\n" +
             "결계/기믹이 실질적 잠금 역할을 하므로 상자 자체는 즉시 열리게 둔다.")]
    [SerializeField] private bool startUnlocked = false;

    [Tooltip("★기믹(스위치/에너지 노드 등)으로 '상자 자체'를 여는 잠금. 체크하면 다가가도 F·힌트가 전혀\n" +
             "안 뜨는 완전 잠금으로 시작 → GimmickChestLock 타깃이 GimmickUnlock() 을 호출하면 풀린다.\n" +
             "풀리면 startUnlocked 처럼 F 한 번에 바로 전리품을 연다.")]
    [SerializeField] private bool gimmickLocked = false;
    private bool _gimmickUnlocked = false;   // gimmickLocked 상자가 스위치 등으로 풀렸는지

    [Header("열기 / 즉시완료")]
    [Tooltip("F로 걸어두면 이 시간(초) 뒤 '수령 가능'이 된다. 자리를 비워도 카운트됨.")]
    [SerializeField] private float openTimeSeconds = 20f;
    [Tooltip("즉시완료(G) 시 소모 HP = 해금까지 '남은 시간' * 이 배율. 배율 2면 20초 남았을 때 40HP(=40초) 소모, 시간이 줄면 비용도 함께 줄어든다.")]
    [SerializeField] private float instantHpCostMultiplier = 2f;

    [Header("리젠 (파밍 후 사라졌다 재등장)")]
    [Tooltip("다 비운 상자가 사라진 뒤 이 시간(초) 후 새 상자로 재등장(Idle 복귀 - 처음부터 다시 까야 함).\n" +
             "★위의 '기믹 잠금'을 켠 상자에는 적용되지 않는다 - 퍼즐 보상은 한 번뿐이라 리젠하지 않는다.")]
    [SerializeField] private float respawnSeconds = 300f;

    [Header("세이브")]
    [Tooltip("세이브에서 이 상자를 구분하는 id. 비우면 계층 경로로 자동 생성한다(대부분 비워두면 된다).\n" +
             "★오브젝트를 옮기거나 이름을 바꾸면 자동 id 가 바뀌어 상태가 초기화된다. 그게 곤란하면 여기에 직접 적는다.")]
    [SerializeField] private string saveId = "";

    /// <summary>다 비운 뒤 다시 생기는가.
    ///
    /// ★퍼즐(기믹)로 여는 상자는 안 생긴다. 퍼즐은 한 번 풀면 영구히 풀린 채라
    ///   (스위치가 계속 켜져 있고 세이브에도 남는다) 리젠하면 리젠된 상자도 F 한 번에 바로
    ///   열린다 — 같은 보상을 시간만 기다리면 무한히 퍼 나르는 자리가 된다.
    ///   '한 번 푸는 퍼즐'과 '반복 파밍'은 같이 둘 수 없으므로 보상 쪽을 1회로 막는다.
    ///
    /// 일반 상자(잠금 따서 여는 것)는 반복 파밍이 원래 의도라 그대로 리젠한다.</summary>
    private bool CanRespawn => !gimmickLocked;

    // 세이브 키. 기믹(GimmickSave)과 같은 저장소를 쓴다 — id → 값 하나짜리 공용 목록이라
    //   상자 전용 목록을 따로 만들 필요가 없다. 종류 접두어가 달라 서로 안 겹친다.
    private string LootedKey   => GimmickSave.Key("chest.looted", this, saveId);   // 퍼즐 상자: 먹었나(1/0)
    private string CooldownKey => GimmickSave.Key("chest.cd",     this, saveId);   // 일반 상자: 리젠까지 남은 초
    [Tooltip("다 비운 상자가 서서히 흐려져 사라지는 시간(초). 리젠으로 다시 나타날 때도 같은 시간에 걸쳐 진해진다.\n" +
             "0이면 사라짐·나타남 둘 다 즉시.")]
    [SerializeField] private float vanishDuration = 0.5f;

    [Header("프롬프트")]
    [SerializeField] private float promptRange = 2.6f;
    [Tooltip("범위 경계에서 들어왔다 나갔다 할 때 깜빡이는 것을 막기 위한 여유 거리(m). 한 번 '근접'으로 판정되면 promptRange + 이 값을 벗어나야 '근접 해제'로 바뀜.")]
    [SerializeField] private float nearExitBuffer = 0.4f;

    // ── 상태 ──────────────────────────────────────────────────────────
    // Idle=잠김(걸어두기 전) / Opening=자물쇠 따는 중 / Ready=수령 가능 / Opened=잠금해제(자유 개폐, 재굴림 안 함) / Depleted=다 비워 사라짐(리젠 대기)
    private enum State { Idle, Opening, Ready, Opened, Depleted }
    private State _state = State.Idle;
    private float _timer;
    private Collider[] _colliders;   // 리젠 동안 사라진 자리 통과 가능하게 토글
    private Renderer[] _renderers;   // 소멸 시 메시 숨김(closedVisual/openedVisual 미할당이어도 확실히 사라지게)

    // 공용 상자 인벤(ChestInstance)을 현재 점유한 상자. 다른 상자를 열면 소유권이 넘어간다.
    private static ChestInteractable _activeChest;
    // 이 상자가 1회 굴린 전리품(가져간 만큼 빠짐). null=아직 안 엶(잠김). 한 번 열면 재굴림 없이 이걸 표시.
    private System.Collections.Generic.List<(int itemId, int count)> _contents;

    // 즉시완료까지 '남은' 대기 시간(초). Idle=아직 안 걸어둠 → 전체 시간, Opening=현재 타이머 잔여.
    // 매초 단위(올림)로 계산 — 0.5초 같은 소수 단위가 아니라 정수 초 기준으로만 비용이 바뀐다.
    private float RemainingSeconds =>
        Mathf.Ceil(_state == State.Opening ? _timer : openTimeSeconds);
    // 즉시완료 비용 = 남은 시간 * 배율. 타이머가 줄면 비용도 비례해서 줄어든다(매 프레임 재계산).
    private float InstantCost => Mathf.Max(0f, RemainingSeconds * instantHpCostMultiplier);

    private Player  _player;

    [Header("강조 발광 (가까이 가면 상자가 노랗게 맥동 — 큰 오브젝트용)")]
    [SerializeField] private Color glowColor        = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private float glowMinIntensity = 0.12f;
    [SerializeField] private float glowMaxIntensity = 0.9f;
    [SerializeField] private float glowPulseSpeed   = 3.5f;

    private Material[] _glowMats;
    private Color[]    _glowOrigEmission;
    private bool       _glowOn;

    // 소멸 페이드(투명도) — 머티리얼 인스턴스를 투명 모드로 바꿔 알파를 0까지 낮췄다가 리젠 때 복원.
    private static readonly int ID_BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ID_Color     = Shader.PropertyToID("_Color");
    private static readonly int ID_Surface   = Shader.PropertyToID("_Surface");
    private static readonly int ID_SrcBlend  = Shader.PropertyToID("_SrcBlend");
    private static readonly int ID_DstBlend  = Shader.PropertyToID("_DstBlend");
    private static readonly int ID_ZWrite    = Shader.PropertyToID("_ZWrite");

    // 페이드할 색 프로퍼티 1개(머티리얼+프로퍼티id+원본색). rgb=true 면 rgb·a 모두 낮춤(발광/커스텀), false 면 알파만(불투명 몸체).
    private struct FadeCol { public Material mat; public int id; public Color orig; public bool rgb; }
    private readonly List<FadeCol> _fadeCols = new();
    private readonly List<Material> _fadeTransparented = new();   // 투명 전환한 몸체 머티리얼(리젠 때 불투명 복구)
    private Light[]    _fadeLights;       // 상자에 붙은 Light 컴포넌트(있으면 밝기도 같이 페이드)
    private float[]    _fadeLightBase;

    // ── 기믹 잠금 표시등 (빨강=잠김 / 파랑=해제) ──────────────────────────
    [Header("기믹 잠금 표시등 (gimmickLocked 상자만)")]
    [Tooltip("잠겨 있을 때 표시등 색(빨강).")]
    [SerializeField] private Color lockedLightColor   = new Color(1f, 0.12f, 0.08f);
    [Tooltip("기믹으로 풀렸을 때 표시등 색(파랑).")]
    [SerializeField] private Color unlockedLightColor = new Color(0.18f, 0.55f, 1f);
    [Tooltip("색을 바꿀 표시등 발광 메시. 비우면 이름에 'light'가 들어간 자식 렌더러를 자동으로 쓴다.")]
    [SerializeField] private Renderer[] lockIndicatorRenderers;
    [Tooltip("색을 바꿀 Light 컴포넌트. 비우면 자식 Light 전체를 자동으로 쓴다.")]
    [SerializeField] private Light[] lockIndicatorLights;

    // 표시등 발광 머티리얼 1개(색 프로퍼티 + 원래 밝기). mag = 원래 HDR 밝기(색만 갈아끼우고 밝기는 유지).
    private struct LockMatCol { public Material mat; public int id; public float mag; }
    private readonly List<LockMatCol> _lockMatCols = new();
    private Light[] _lockLights;
    private bool    _lockIndicatorInit;

    private void Awake()
    {
        _colliders = GetComponentsInChildren<Collider>(true);
        _renderers = GetComponentsInChildren<Renderer>(true);

        // 퍼즐 상자는 '먹었나', 일반 상자는 '리젠까지 남은 시간'을 저장한다 → 둘 다 참여한다.
        SaveSlotManager.Instance?.Register(this);
    }

    private void OnDestroy()
    {
        SaveSlotManager.Instance?.Unregister(this);
    }

    private void Start()
    {
        _player = FindAnyObjectByType<Player>();
        SetupGlow();
        // 기믹으로 여는 상자: 잠금/대기 없이 '수령 가능'으로 시작 → 첫 F 에 바로 전리품을 굴려 연다.
        if (startUnlocked) _state = State.Ready;
        // 기믹 잠금 상자: 표시등을 빨강(잠김)으로. 이미 풀린 상태면 파랑.
        //   ApplyLockIndicator(locked) — 시작 시엔 아직 안 풀렸으니 locked=true(빨강).
        if (gimmickLocked) ApplyLockIndicator(!_gimmickUnlocked);

        RestoreSaved();
    }

    // ── 세이브 ────────────────────────────────────────────────────────────
    // 두 가지를 저장한다.
    //
    //   퍼즐 상자 : '먹었나' 한 번뿐. 퍼즐은 한 번 풀면 세이브에 남아 영구히 풀린 채라,
    //               안 막으면 껐다 켤 때마다 잠금이 풀린 상자가 새로 채워져서 재접속만
    //               반복해도 같은 보상을 계속 먹는다.
    //
    //   일반 상자 : '리젠까지 남은 시간'. 안 남기면 메인메뉴에 갔다 오는 것만으로 방금 턴
    //               상자가 멀쩡하게 되살아난다 — 쿨타임이 사실상 없는 것과 같다.
    //
    //   ★남은 '시간'을 남기지, 시각(시계)을 남기지 않는다. 지금 쿨타임은 게임 안 시간으로
    //     흐르므로(설정창을 열어 멈추면 같이 멈춘다) 같은 기준으로 이어져야 앞뒤가 맞고,
    //     시스템 시계를 돌려 쿨타임을 건너뛰는 것도 막힌다.
    //     '접속 안 한 동안에도 쿨이 돌길' 원하면 여기만 시각 비교로 바꾸면 된다.

    /// <summary>세이브에 남은 상태를 지금 상자에 반영한다.
    /// ★Start 에서 부른다 — 콜라이더/렌더러 캐시(Awake)와 발광 준비(SetupGlow)가 끝난 뒤여야
    ///   확실히 숨길 수 있다.
    /// ★GimmickChestLock 이 Awake/Start 에서 GimmickUnlock() 을 부를 수 있는데, 그건 _state 를
    ///   Ready 로 올릴 뿐이라 여기서 덮어써도 문제 없다.</summary>
    private void RestoreSaved()
    {
        if (gimmickLocked)
        {
            if (GimmickSave.GetBool(LootedKey)) HideAsDepleted(permanent: true);
            return;
        }

        float remain = GimmickSave.TryGet(CooldownKey, out float v) ? v : 0f;
        if (remain <= 0f) return;

        HideAsDepleted(permanent: false);
        _timer = remain;   // 남은 쿨타임부터 이어서 센다
    }

    public void Capture(GameSaveData data)
    {
        if (data == null) return;

        // 다 비워서 사라진 상태만 기록한다. 열어두고 아직 안 비운 상자는 다음에 마저 먹어야 한다.
        if (gimmickLocked)
        {
            if (_state == State.Depleted) GimmickSave.SetDeferred(LootedKey, 1f);
            return;
        }

        // 리젠 대기 중이 아니면 0 — 되살아난 상자가 다음 로드에서 또 숨는 것을 막는다.
        GimmickSave.SetDeferred(CooldownKey,
            _state == State.Depleted ? Mathf.Max(0f, _timer) : 0f);
    }

    /// <summary>연출 없이 즉시 '사라진' 모습으로. 세이브 복원 전용.</summary>
    /// <param name="permanent">true 면 다시 안 생긴다(퍼즐 보상) → 매 프레임 도는 것도 멈춘다.
    /// false 면 리젠 타이머가 돌아야 하므로 컴포넌트는 켜 둔다.</param>
    private void HideAsDepleted(bool permanent)
    {
        _state = State.Depleted;
        _contents = null;
        if (_activeChest == this) _activeChest = null;

        if (_colliders != null)
            foreach (var c in _colliders) if (c != null) c.enabled = false;
        if (_renderers != null)
            foreach (var r in _renderers) if (r != null) r.enabled = false;
        if (closedVisual != null) closedVisual.SetActive(false);
        if (openedVisual != null) openedVisual.SetActive(false);

        UpdateGlow(false);
        ChestPromptUI.Instance?.HideIfOwner(this);
        if (permanent) enabled = false;   // 할 일이 없다 — 매 프레임 도는 것을 멈춘다
    }

    // ── 기믹 잠금 해제 (GimmickChestLock 타깃이 스위치/에너지 노드 충족 시 호출) ──────
    // gimmickLocked 상자를 열 수 있게 푼다. 풀리면 startUnlocked 처럼 '수령 가능'이 되어
    //   다가가 F 한 번에 바로 전리품을 연다. 이미 풀렸으면 무시.
    public void GimmickUnlock()
    {
        if (!gimmickLocked || _gimmickUnlocked) return;
        _gimmickUnlocked = true;
        if (_state == State.Idle) _state = State.Ready;   // 잠금이 실질 역할 → 풀리면 즉시 개방
        GameSfx.Play(SfxId.ChestOpenComplete, transform.position);
        ApplyLockIndicator(false);   // 표시등 파랑(해제)
    }

    // ── 잠금 표시등 ──────────────────────────────────────────────────────
    // 표시등 발광 메시/Light 를 모아 색을 갈아끼운다. 원래 밝기(HDR)는 유지하고 색만 교체.
    private void SetupLockIndicator()
    {
        if (_lockIndicatorInit) return;
        _lockIndicatorInit = true;

        // 대상 Light — 지정 없으면 자식 전체.
        _lockLights = (lockIndicatorLights != null && lockIndicatorLights.Length > 0)
            ? lockIndicatorLights
            : GetComponentsInChildren<Light>(true);

        // 대상 렌더러 — 지정 없으면 이름에 'light' 들어간 자식 렌더러(발광 메시).
        Renderer[] rends = lockIndicatorRenderers;
        if (rends == null || rends.Length == 0)
        {
            var list = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                if (r.gameObject.name.ToLowerInvariant().Contains("light")) list.Add(r);
            }
            rends = list.ToArray();
        }

        _lockMatCols.Clear();
        foreach (var r in rends)
        {
            if (r == null) continue;
            foreach (var m in r.materials)   // 인스턴스 — 다른 상자에 영향 없음
            {
                if (m == null) continue;
                var sh = m.shader;
                int count = sh != null ? sh.GetPropertyCount() : 0;
                for (int i = 0; i < count; i++)
                {
                    if (sh.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Color) continue;
                    int id = sh.GetPropertyNameId(i);
                    if (!m.HasProperty(id)) continue;
                    Color o = m.GetColor(id);
                    float mag = Mathf.Max(o.r, o.g, o.b, 1f);   // 원래 밝기 유지용(HDR 발광 보존)
                    _lockMatCols.Add(new LockMatCol { mat = m, id = id, mag = mag });
                }
            }
        }
    }

    private void ApplyLockIndicator(bool locked)
    {
        SetupLockIndicator();
        Color tint = locked ? lockedLightColor : unlockedLightColor;

        for (int i = 0; i < _lockMatCols.Count; i++)   // 발광 메시: 밝기 유지, 색만 교체
        {
            var f = _lockMatCols[i];
            if (f.mat == null) continue;
            Color c = tint * f.mag; c.a = 1f;
            f.mat.SetColor(f.id, c);
        }
        if (_lockLights != null)                        // 실제 Light: 색만 교체(세기 유지)
            foreach (var l in _lockLights)
                if (l != null) l.color = tint;
    }

    // 인벤토리 UI가 열려있거나 promptRange 밖이면 차단. 그 외엔 항시 F 가능(걸어두기/수령/즉시 재오픈).
    public bool CanInteract
    {
        get
        {
            // 기믹 잠금(스위치 등) — 아직 안 풀렸으면 상호작용/힌트 자체를 막는다.
            if (gimmickLocked && !_gimmickUnlocked) return false;
            var inv = InventoryUIController.Instance;
            if (inv != null && inv.IsOpen) return false;
            // 프롬프트 UI가 뜨는 거리와 F가 통하는 거리를 일치시킨다(물리 콜라이더 형태 때문에
            // OverlapSphere가 promptRange 밖에서도 감지하는 경우가 있어, 여기서 최종 거리 검증).
            return IsPlayerNear();
        }
    }

    // ── F 상호작용 ──────────────────────────────────────────────────────
    public void Interact(Player player)
    {
        if (player == null) return;

        switch (_state)
        {
            case State.Idle:
                if (requireBase && !player.Stat.IsInBase)
                {
                    return;
                }
                _state = State.Opening;       // F로 걸어두기
                _timer = openTimeSeconds;
                SetVisual(false);
                GameSfx.Play(SfxId.ChestOpenStart, transform.position);   // 상자 열기 시작음(3D)
                break;

            case State.Opening:
                break;   // 여는 중 — 대기

            case State.Ready:
                Collect(player);             // 수령(롤 + 패널 열기)
                break;

            case State.Opened:
                if (HasItems())
                    ReopenPanel();           // 아이템 남아있을 때만 다시 열기
                break;
        }
        RefreshIndicator(IsPlayerNear());
    }

    // ── G 즉시완료 (IInstantInteractable) ───────────────────────────────
    public bool CanInstantComplete(Player player)
    {
        if (player == null) return false;
        if (_state != State.Idle && _state != State.Opening) return false;   // 걸어두기 전/도중만 즉시완료
        return player.Stat.CurrentHp > InstantCost; // 비용보다 많아야 (즉시완료로 죽지 않게)
    }

    public void OnInstantComplete(Player player)
    {
        if (player == null || !CanInstantComplete(player))
        {
            return;
        }
        if (requireBase && !player.Stat.IsInBase) return;
        player.Stat.SpendHp(InstantCost);
        Collect(player);
    }

    // ── 매 프레임 ─────────────────────────────────────────────────────────
    private void Update()
    {
        if (_state == State.Opening)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) { _timer = 0f; _state = State.Ready; GameSfx.Play(SfxId.ChestOpenComplete, transform.position); }   // 상자 열기 완료음(잠금 풀림, 3D)
        }
        else if (_state == State.Opened && !HasItems()
                 && !(InventoryUIController.IsChestOpen && _activeChest == this))
        {
            // 다 비운 상자 -> 패널 닫은 뒤 사라지고 리젠 대기(한 번 판 상자는 그 자리서 소멸).
            EnterDepleted();
        }
        else if (_state == State.Depleted && CanRespawn)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) Respawn();   // 새 상자로 재등장(Idle)
        }

        bool near = IsPlayerNear();
        RefreshIndicator(near);
        // 기믹 상자는 노란 근접 발광을 쓰지 않는다(빨강/파랑 잠금 표시등이 그 역할).
        bool highlight = !gimmickLocked && near && _state != State.Depleted && !(_state == State.Opened && !HasItems());
        // 해금 중엔 범위 밖이어도 연한 펄스로 표시 유지.
        bool openingRemote = _state == State.Opening && !near;
        UpdateGlow(highlight, openingRemote);
    }

    // ── 수령: 자물쇠 따서 전리품 1회만 굴림 + 패널. 이후 이 상자는 잠금해제(재굴림 없음). ──
    private void Collect(Player player)
    {
        if (InventoryUIController.IsChestOpen)
            InventoryUIController.IsChestOpen = false;

        var items = Roll();
        if (items.Count == 0)
            Debug.LogWarning($"[Chest] sourceId='{sourceId}' — DropTable에 Chest 항목 없음");
        _contents = items;   // 이 상자 전리품 저장(딱 한 번만 굴림. 이후 재굴림 X)

        _state = State.Opened;
        SetVisual(true);
        OpenWithMyContents();
    }

    // 이미 연(잠금해제) 상자를 재굴림 없이 다시 열기 (껐다 켜도 같은 내용, 가져간 만큼 빠진 채로)
    private void ReopenPanel()
    {
        OpenWithMyContents();
    }

    // 현재 표시중 내용을 그 상자에 보존 -> 내 전리품을 ChestInstance에 로드 -> 패널 열기
    private void OpenWithMyContents()
    {
        SaveActiveChestContents();   // 직전 활성 상자(또는 나 자신)의 가져간-반영 상태 보존

        _activeChest = this;
        InventoryUIController.IsChestOpen = true;   // 아이템 로드 전에 먼저 설정 (이벤트 타이밍 대비)

        var chestInv = InventoryManager.ChestInstance;
        if (chestInv != null)
        {
            chestInv.ClearAllItems();
            if (_contents != null)
                foreach (var (itemId, count) in _contents)
                    chestInv.AddItem(itemId, count);
        }

        InventoryUIController.Instance?.Open();
    }

    // ChestInstance의 현재 내용을 활성 상자의 _contents에 저장(플레이어가 가져간 만큼 반영). 재오픈 시 복제 방지.
    private static void SaveActiveChestContents()
    {
        if (_activeChest == null) return;
        var c = InventoryManager.ChestInstance;
        if (c == null) return;
        var list = new System.Collections.Generic.List<(int itemId, int count)>();
        foreach (var slot in c.GetSlots())
            if (!slot.IsEmpty) list.Add((slot.itemId, slot.amount));
        _activeChest._contents = list;
    }

    private void SetVisual(bool opened)
    {
        if (closedVisual != null) closedVisual.SetActive(!opened);
        if (openedVisual != null) openedVisual.SetActive(opened);
    }

    // 다 비운 상자: 사라짐(비주얼 둘 다 숨김 + 콜라이더 off로 자리 통과 가능) + 리젠 타이머 시작.
    private void EnterDepleted()
    {
        _state = State.Depleted;
        _timer = respawnSeconds;
        _contents = null;
        if (_activeChest == this) _activeChest = null;
        // 콜라이더는 즉시 꺼서 사라지는 동안 통과 가능하게. 비주얼은 스케일이 줄며 서서히 사라진다.
        if (_colliders != null)
            foreach (var c in _colliders) if (c != null) c.enabled = false;
        UpdateGlow(false);
        ChestPromptUI.Instance?.HideIfOwner(this);

        // ★즉시 기록한다. 다음 저장까지 기다리면 그 사이에 게임을 끈 플레이어는
        //   퍼즐 상자를 또 먹거나, 방금 턴 상자를 쿨타임 없이 다시 턴다.
        if (gimmickLocked) GimmickSave.Set(LootedKey, true);
        else               GimmickSave.Set(CooldownKey, respawnSeconds);

        StartCoroutine(VanishRoutine());
    }

    // 투명도를 낮추며 형체가 서서히 흐려져 소멸 → 끝나면 메시 숨김(리젠 때 Respawn 이 복원).
    private IEnumerator VanishRoutine()
    {
        CollectFadeMaterials();   // 머티리얼 인스턴스 수집 + 투명 모드 전환 + 원본 색 기억

        for (float t = 0f; t < vanishDuration; t += Time.deltaTime)
        {
            float a = 1f - t / vanishDuration;   // 1 → 0
            ApplyFadeAlpha(a);
            yield return null;
        }
        ApplyFadeAlpha(0f);

        if (closedVisual != null) closedVisual.SetActive(false);
        if (openedVisual != null) openedVisual.SetActive(false);
        if (_renderers != null)
            foreach (var r in _renderers) if (r != null) r.enabled = false;   // 확실히 숨김(비주얼 미할당 대비)

        // 영영 다시 안 생기는 상자(퍼즐 보상)는 여기서 할 일이 끝났다. 매 프레임 도는 것을 멈춘다.
        //   ★반드시 페이드가 끝난 뒤에 끈다. 컴포넌트를 끄면 코루틴도 같이 멈춰서,
        //     도중에 끄면 반쯤 투명한 상자가 그대로 남는다.
        if (!CanRespawn) enabled = false;
    }

    // 렌더러 머티리얼 인스턴스를 모아 페이드할 색 프로퍼티를 등록한다.
    //   • URP Lit 몸체(_Surface 보유): 투명 전환 후 base 색은 '알파만' 낮춤(자연스러운 페이드).
    //   • 그 외(커스텀 셰이더그래프/발광/파티클): 노출된 '모든 Color 프로퍼티'를 rgb·a 로 낮춤 →
    //     Box_Light 같은 Emisson 셰이더도 프로퍼티 이름 몰라도 무조건 흐려진다.
    private void CollectFadeMaterials()
    {
        _fadeCols.Clear();
        _fadeTransparented.Clear();
        if (_renderers != null)
        {
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                foreach (var m in r.materials)   // 인스턴스(공유 재질/다른 상자에 영향 없음)
                {
                    if (m == null) continue;

                    if (m.HasProperty(ID_Surface))
                    {
                        // URP Lit 몸체: 투명 전환 + base 알파만 페이드 + 발광 있으면 rgb 페이드.
                        SetTransparent(m);
                        _fadeTransparented.Add(m);
                        if (m.HasProperty(ID_BaseColor))
                            _fadeCols.Add(new FadeCol { mat = m, id = ID_BaseColor, orig = m.GetColor(ID_BaseColor), rgb = false });
                        else if (m.HasProperty(ID_Color))
                            _fadeCols.Add(new FadeCol { mat = m, id = ID_Color, orig = m.GetColor(ID_Color), rgb = false });
                    }
                    else
                    {
                        // 커스텀/발광/파티클 셰이더: 노출된 모든 Color 프로퍼티를 rgb·a 로 낮춘다(이름 몰라도 됨).
                        var sh = m.shader;
                        int count = sh != null ? sh.GetPropertyCount() : 0;
                        for (int i = 0; i < count; i++)
                        {
                            if (sh.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Color) continue;
                            int id = sh.GetPropertyNameId(i);
                            if (!m.HasProperty(id)) continue;
                            _fadeCols.Add(new FadeCol { mat = m, id = id, orig = m.GetColor(id), rgb = true });
                        }
                    }
                }
            }
        }

        // 상자에 붙은 실제 Light 컴포넌트(있으면 밝기도 같이 페이드)
        _fadeLights = GetComponentsInChildren<Light>(true);
        _fadeLightBase = new float[_fadeLights.Length];
        for (int i = 0; i < _fadeLights.Length; i++)
            _fadeLightBase[i] = _fadeLights[i] != null ? _fadeLights[i].intensity : 0f;
    }

    private void ApplyFadeAlpha(float mul)
    {
        // 발광은 HDR(밝기>1, 블룸용)이라 선형으로 낮추면 끝까지 밝게 보인다 → 세제곱 곡선으로 초반에 빨리 어두워지게.
        float glow = mul * mul * mul;
        for (int i = 0; i < _fadeCols.Count; i++)
        {
            var f = _fadeCols[i];
            if (f.mat == null) continue;
            Color c = f.orig;
            if (f.rgb) { c.r *= glow; c.g *= glow; c.b *= glow; c.a *= mul; }   // 발광/커스텀: 밝기(rgb)는 급격히
            else       { c.a = f.orig.a * mul; }                               // 불투명 몸체: 알파만(선형)
            f.mat.SetColor(f.id, c);
        }
        if (_fadeLights != null)
            for (int i = 0; i < _fadeLights.Length; i++)
                if (_fadeLights[i] != null) _fadeLights[i].intensity = _fadeLightBase[i] * glow;   // 라이트 밝기도 급격히
    }

    // URP Lit 를 런타임에 투명(Alpha) 표면으로 전환. 없는 프로퍼티는 건너뜀(다른 셰이더 안전).
    private static void SetTransparent(Material m)
    {
        if (m.HasProperty(ID_Surface))  m.SetFloat(ID_Surface, 1f);      // 0=Opaque, 1=Transparent
        if (m.HasProperty(ID_SrcBlend)) m.SetFloat(ID_SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty(ID_DstBlend)) m.SetFloat(ID_DstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty(ID_ZWrite))   m.SetFloat(ID_ZWrite, 0f);
        m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        // ★반사/스페큘러 OFF — 투명해질수록 하늘(스카이박스)이 표면에 반사돼 파랗게 비치는 것 방지.
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    // 리젠 시 원래 색/불투명으로 되돌린다.
    private void RestoreOpaqueMaterials()
    {
        for (int i = 0; i < _fadeCols.Count; i++)
        {
            var f = _fadeCols[i];
            if (f.mat != null) f.mat.SetColor(f.id, f.orig);   // 색·알파 원복
        }
        foreach (var m in _fadeTransparented)
        {
            if (m == null) continue;
            if (m.HasProperty(ID_Surface))  m.SetFloat(ID_Surface, 0f);
            if (m.HasProperty(ID_SrcBlend)) m.SetFloat(ID_SrcBlend, (float)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty(ID_DstBlend)) m.SetFloat(ID_DstBlend, (float)UnityEngine.Rendering.BlendMode.Zero);
            if (m.HasProperty(ID_ZWrite))   m.SetFloat(ID_ZWrite, 1f);
            m.EnableKeyword("_SURFACE_TYPE_OPAQUE");
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.renderQueue = -1;   // 셰이더 기본 큐로 복귀
        }
        if (_fadeLights != null)
            for (int i = 0; i < _fadeLights.Length; i++)
                if (_fadeLights[i] != null) _fadeLights[i].intensity = _fadeLightBase[i];   // 밝기 원복

        _fadeCols.Clear();
        _fadeTransparented.Clear();
        _fadeLights = null; _fadeLightBase = null;
    }

    // 리젠: 새 상자로 다시 등장(Idle = 잠김). 처음부터 다시 까야 함 = 새로운 상자 개념.
    private void Respawn()
    {
        // startUnlocked, 또는 이미 기믹으로 풀린 상자는 리젠 후에도 바로 열림(스위치는 계속 켜진 상태).
        _state = (startUnlocked || (gimmickLocked && _gimmickUnlocked)) ? State.Ready : State.Idle;
        _timer = 0f;
        _contents = null;
        // 쿨이 끝났음을 세이브에서도 지운다. 안 지우면 여기서 되살아난 직후 껐을 때
        //   다음 로드에서 옛 남은 시간으로 또 숨는다.
        GimmickSave.SetDeferred(CooldownKey, 0f);
        RestoreOpaqueMaterials();   // 소멸 페이드로 투명해졌던 머티리얼을 원래 불투명/색으로 복원
        if (_colliders != null)
            foreach (var c in _colliders) if (c != null) c.enabled = true;
        if (_renderers != null)
            foreach (var r in _renderers) if (r != null) r.enabled = true;   // 메시 복귀
        SetVisual(false);   // closedVisual on(잠긴 새 상자), openedVisual off

        // 사라질 때와 같은 연출을 거꾸로 — 형체가 서서히 진해지며 나타난다.
        //   ★그냥 켜면 눈앞에서 상자가 툭 튀어나온다. 사라질 땐 스르륵 사라지는데 나타날 때만
        //     즉시면 짝이 안 맞는다.
        if (vanishDuration > 0f) StartCoroutine(AppearRoutine());
    }

    // 투명도를 올리며 형체가 서서히 진해져 등장. 소멸(VanishRoutine)의 역방향이다.
    //   ★StartCoroutine 은 첫 yield 까지 그 자리에서 실행된다 — 아래 두 줄이 같은 프레임에
    //     돌아서, 켠 뒤 한 프레임 불투명하게 번쩍이는 일이 없다.
    private IEnumerator AppearRoutine()
    {
        CollectFadeMaterials();   // 투명 모드 전환 + '지금(불투명) 색'을 원본으로 기억
        ApplyFadeAlpha(0f);       // 완전히 투명한 상태에서 시작

        for (float t = 0f; t < vanishDuration; t += Time.deltaTime)
        {
            ApplyFadeAlpha(t / vanishDuration);   // 0 → 1
            yield return null;
        }
        ApplyFadeAlpha(1f);

        // ★반드시 불투명으로 되돌린다. 투명 모드로 남겨두면 그림자와 그리는 순서가 계속 어긋난다.
        RestoreOpaqueMaterials();
    }

    private void OnDisable()
    {
        // 씬 종료/비활성 중엔 GetOrCreate 로 새로 만들지 말 것(정리 안 됨 에러). 있을 때만 숨김.
        ChestPromptUI.Instance?.HideIfOwner(this);
        if (_glowOn) UpdateGlow(false);   // 발광 켜진 채 남지 않도록 원복
    }

    // ── 프롬프트 / 상태 표시 ─────────────────────────────────────────────
    private void RefreshIndicator(bool near)
    {
        // GameUIController.IsUIBlocking() = 설정/인벤/퀘스트/팩토리/빌드 등 모든 UI 상태 포함
        bool anyUIOpen = (GameUIController.Instance != null && GameUIController.Instance.IsUIBlocking())
                      || (InventoryUIController.Instance != null && InventoryUIController.Instance.IsOpen);
        var ui = ChestPromptUI.GetOrCreate();
        if (ui == null) return;   // 씬에 프롬프트가 없으면 조용히 스킵(매 프레임 NRE 방지)

        if (anyUIOpen) { ui.HideIfOwner(this); return; }

        // 기믹 잠금 중: 가까이 가면 '잠긴 상자' 표시(F 로 못 엶 — 스위치로 풀림). 멀면 숨김.
        if (gimmickLocked && !_gimmickUnlocked)
        {
            if (!near) { ui.HideIfOwner(this); return; }
            ui.ShowGimmickLocked(this);
            return;
        }

        switch (_state)
        {
            case State.Opening:
                if (!near) { ui.HideIfOwner(this); return; }
                float prog = openTimeSeconds > 0f ? 1f - _timer / openTimeSeconds : 1f;
                ui.ShowOpening(this, prog, _timer, InstantCost, () => OnInstantComplete(_player));
                break;
            case State.Ready:
                if (!near) { ui.HideIfOwner(this); return; }
                ui.ShowReady(this, () => Interact(_player));
                break;
            case State.Idle:
                if (!near) { ui.HideIfOwner(this); return; }
                ui.ShowIdle(this, InstantCost,
                    () => Interact(_player),
                    () => OnInstantComplete(_player));
                break;
            case State.Opened:
                if (!near || !HasItems()) { ui.HideIfOwner(this); return; }
                ui.ShowOpened(this, () => Interact(_player));
                break;
            default:
                ui.HideIfOwner(this);
                break;
        }
    }

    // ── 강조 발광 (B 방안) ────────────────────────────────────────────────
    // 가까이 가면 상자 메시가 노란빛으로 은은하게 맥동 → 큰 오브젝트에서 확실히 보임.

    private void SetupGlow()
    {
        var rends = GetComponentsInChildren<Renderer>(true);
        var mats  = new List<Material>();
        var orig  = new List<Color>();
        foreach (var r in rends)
        {
            if (r is ParticleSystemRenderer) continue;
            foreach (var m in r.materials)   // 인스턴스화 — 공유 재질/다른 상자에 영향 없음
            {
                if (m == null) continue;
                m.EnableKeyword("_EMISSION");
                mats.Add(m);
                orig.Add(m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black);
            }
        }
        _glowMats         = mats.ToArray();
        _glowOrigEmission = orig.ToArray();
    }

    private void UpdateGlow(bool show, bool pale = false)
    {
        if (_glowMats == null || _glowMats.Length == 0) return;

        if (show)
        {
            float t         = (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f;   // 0~1 맥동
            float intensity = Mathf.Lerp(glowMinIntensity, glowMaxIntensity, t);
            Color add       = glowColor * intensity;
            for (int i = 0; i < _glowMats.Length; i++)
                _glowMats[i].SetColor("_EmissionColor", _glowOrigEmission[i] + add);
            _glowOn = true;
        }
        else if (pale)
        {
            // 해금 중 범위 밖 — 맥동 없이 연하게 고정 발광.
            Color add = glowColor * (glowMinIntensity * 0.6f);
            for (int i = 0; i < _glowMats.Length; i++)
                _glowMats[i].SetColor("_EmissionColor", _glowOrigEmission[i] + add);
            _glowOn = true;
        }
        else if (_glowOn)
        {
            for (int i = 0; i < _glowMats.Length; i++)
                _glowMats[i].SetColor("_EmissionColor", _glowOrigEmission[i]);
            _glowOn = false;
        }
    }

    // 경계에서 미세하게 들어왔다 나갔다 해도 깜빡이지 않도록 히스테리시스 적용.
    // 이미 근접 상태면 promptRange + nearExitBuffer를 벗어나야 비로소 해제된다.
    private bool _near;

    private bool IsPlayerNear()
    {
        if (_player == null) { _near = false; return false; }
        float range = _near ? promptRange + nearExitBuffer : promptRange;
        _near = (_player.transform.position - transform.position).sqrMagnitude <= range * range;
        return _near;
    }

    // 현재 이 상자에 남아있는 아이템이 있는지 확인
    // _activeChest == this면 ChestInstance(실시간)를 직접 확인, 아니면 저장된 _contents 사용
    private bool HasItems()
    {
        if (_activeChest == this)
        {
            var ci = InventoryManager.ChestInstance;
            if (ci == null) return false;
            foreach (var slot in ci.GetSlots())
                if (!slot.IsEmpty) return true;
            return false;
        }
        return _contents != null && _contents.Count > 0;
    }

    // ── 드롭 롤 ───────────────────────────────────────────────────────
    private List<(int itemId, int count)> Roll()
    {
        var result  = new List<(int itemId, int count)>();
        string myId = (sourceId ?? "").Trim();
        if (myId.Length == 0) return result;

        // 각 행(아이템)을 독립적으로 굴린다 - dropChance% 로 드롭, 뜨면 개수 판정. 행끼리 서로 영향 없음.
        foreach (var row in GameDataHolder.I.DropTable.All)
        {
            string rowId = (row.sourceId ?? "").Trim();
            if (row.sourceType != SourceType.Chest || rowId != myId) continue;

            if (Random.Range(0, 100) >= row.dropChance) continue;   // 드롭 실패

            int itemId = ExtractItemId(row.SheetId);
            if (itemId <= 0) continue;
            if (GameDataUtility.GetItem(itemId) == null) continue;

            int count = GameDataUtility.RollDropCount(row.countDist);
            if (count > 0) result.Add((itemId, count));
        }
        return result;
    }

    private int ExtractItemId(DropTableSheetId sheetId)
    {
        string s = sheetId;
        int u = s.LastIndexOf('_');
        if (u < 0 || u + 1 >= s.Length) return 0;
        return int.TryParse(s.Substring(u + 1), out int id) ? id : 0;
    }
}
