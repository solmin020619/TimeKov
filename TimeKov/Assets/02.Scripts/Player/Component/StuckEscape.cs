using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ── 탈출하기 ─────────────────────────────────────────────────────────────────
// 지형에 끼거나 갇혀서 못 나올 때, 조금 전 지나온 안전한 자리로 되돌려 준다.
// 설정창(ESC) 하단 '탈출하기' 버튼이 이 컴포넌트를 부른다.
//
// ★"끼었는지"를 코드가 판정하지 않는다.
//   지형이 어떻게 생겼는지를 물리로 추측하는 방식은 오탐/미탐이 반드시 생기고,
//   정작 필요한 순간에 버튼이 안 먹는 일이 벌어진다. 판단은 플레이어에게 맡긴다.
//
// ★쿨타임·전투 제한 같은 '사용 조건'도 두지 않는다.
//   목적지가 '조금 전 내가 서 있던 자리'뿐이라 멀쩡할 때 눌러 봐야 뒤로 조금 물러날 뿐,
//   이동 단축·전투 이탈 어디에도 쓸 수가 없다. 악용할 수 없는 기능에 제한을 걸면
//   정작 필요할 때 막히기만 한다.
//
// ★우리 맵은 바닥이 터레인 하나이고 일부 구역을 콜라이더로 막아 뒀다.
//   그래서 NavMesh 에 의존하지 않는다 — 목적지는 '실제로 서 있던 좌표'이고,
//   지금 갈 수 있는지는 캡슐이 들어가는지로 직접 확인한다.
[RequireComponent(typeof(Player))]
public class StuckEscape : MonoBehaviour
{
    [Header("안전 좌표 기록")]
    [Tooltip("좌표를 기록하는 주기(초).")]
    [SerializeField] private float recordInterval = 0.4f;
    [Tooltip("보관할 좌표 개수. ★시간이 아니라 개수로 보관한다 —\n" +
             "시간으로 자르면 낀 채로 그 시간을 넘기는 순간 이력이 비어 돌아갈 곳이 없어진다.\n" +
             "넉넉히 잡아야 구덩이 안에서 한참 맴돌아도 '들어오기 전' 좌표가 밀려나지 않는다\n" +
             "(개수 × 기록 간격 = 보관하는 이동 거리. 120 × 2.5m = 300m).")]
    [SerializeField] private int historyCapacity = 120;
    [Tooltip("직전 기록에서 이만큼 실제로 움직여야 새로 기록한다(m).\n" +
             "★'실제로 움직였는가'가 기록 조건이라, 낀 상태에서는 저절로 기록이 멈춘다.\n" +
             " 그래서 끼기 전의 자리가 그대로 남는다.")]
    [SerializeField] private float recordSpacing = 2.5f;

    [Header("복귀 지점 선택")]
    [Tooltip("가능하면 이만큼 떨어진 자리로 보낸다(m).\n" +
             "★지형이 그릇처럼 파여 갇힌 경우를 위한 값이다. 그 안에서도 걸어다니면 좌표가 계속\n" +
             " 기록되므로, 가장 최신 좌표를 고르면 구덩이 안으로 되돌아간다.\n" +
             " 구덩이 지름보다 넉넉히 크게 잡아 두면 자연히 바깥 좌표가 뽑힌다.")]
    [SerializeField] private float preferEscapeDistance = 15f;
    [Tooltip("이 거리(m) 이상 떨어진 자리가 하나도 없을 때 허용하는 최소 거리.\n" +
             "너무 가까우면 다시 낀다.")]
    [SerializeField] private float minEscapeDistance = 1.2f;
    [Tooltip("이보다 먼 자리로는 보내지 않는다(m). 워프로 건너온 이전 지역 좌표를 걸러내는 상한이기도 하다.")]
    [SerializeField] private float maxEscapeDistance = 60f;

    [Header("제한")]
    [Tooltip("최근 이 시간(초) 안에 피격했거나 공격/스킬을 썼으면 사용할 수 없다.\n" +
             "불리한 전투에서 빠져나가는 용도로 쓰이는 것을 막는 유일한 제한이다. 0이면 제한 없음.")]
    [SerializeField] private float combatLockSeconds = 8f;

    [Header("연출")]
    [Tooltip("검은 화면으로 넘어가는 시간(초).")]
    [SerializeField] private float fadeTime = 0.35f;
    [Tooltip("완전히 검은 상태를 유지하는 시간(초). 이 사이에 이동한다.")]
    [SerializeField] private float blackHold = 0.15f;

    // ── 상태 ────────────────────────────────────────────────────────────
    private Player _player;
    private Rigidbody _rb;
    private CapsuleCollider _capsule;

    private readonly List<Vector3> _history = new List<Vector3>();
    private readonly Collider[] _overlap = new Collider[16];
    private int _blockMask;

    private float _recordTimer;
    private float _lastCombatTime = -999f;
    private bool _busy;
    private CanvasGroup _fadeCg;

    public bool IsBusy => _busy;

    // ── 수명 ────────────────────────────────────────────────────────────
    private void Awake()
    {
        _player  = GetComponent<Player>();
        _rb      = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>();

        // 도착 자리가 비었는지 볼 때 쓸 레이어. 자기 자신·적·물·UI 는 장애물로 보지 않는다.
        int m = ~0;
        m &= ~LayerBit("Player");
        m &= ~LayerBit("Enemy");
        m &= ~LayerBit("Water");
        m &= ~LayerBit("Ignore Raycast");
        m &= ~LayerBit("UI");
        m &= ~(1 << gameObject.layer);
        _blockMask = m;
    }

    private static int LayerBit(string name)
    {
        int l = LayerMask.NameToLayer(name);
        return l < 0 ? 0 : (1 << l);
    }

    private void OnEnable()
    {
        if (_player?.Stat != null) _player.Stat.OnHurt += MarkCombat;
    }

    private void OnDisable()
    {
        if (_player?.Stat != null) _player.Stat.OnHurt -= MarkCombat;
    }

    private void MarkCombat() => _lastCombatTime = Time.time;

    private void Update()
    {
        // 전투 판정은 항상 갱신한다(아래 early return 보다 위).
        //   피격 이벤트만으로는 선공 상황을 못 잡으므로, 내가 때리는 중인 것도 전투로 본다.
        if (_player.Skill != null && _player.Skill.IsExecuting) _lastCombatTime = Time.time;

        if (_busy) return;
        if (_player.Stat == null || _player.Stat.IsDead) return;
        if (PlayerInputComponent.IsBlocked) return;   // UI 중에는 기록만 멈춘다

        RecordHistory();
    }

    /// 최근 전투 중인가. 설정창을 여는 사이에 시간이 흐르지 않도록 판정은 누른 시점에 한다.
    private bool InCombat =>
        combatLockSeconds > 0f && Time.time - _lastCombatTime < combatLockSeconds;

    // ── 안전 좌표 이력 ──────────────────────────────────────────────────
    // 조건은 단 두 가지 — '설 수 있는 땅' 위이고, 직전 기록에서 실제로 움직였을 것.
    //   ★'실제로 움직였는가'로 판단하는 것이 핵심이다. 속도 값(CurrentSpeed)은 벽에 막혀
    //     한 발도 못 나가는 중에도 살아 있어서, 그걸 쓰면 낀 자리를 계속 기록해 버린다.
    private void RecordHistory()
    {
        _recordTimer += Time.deltaTime;
        if (_recordTimer < recordInterval) return;
        _recordTimer = 0f;

        var mv = _player.Movement;
        if (mv == null || !mv.IsGrounded || mv.OnSteepSlope) return;

        Vector3 p = transform.position;

        if (_history.Count > 0)
        {
            float moved = Flat2(p, _history[_history.Count - 1]);
            if (moved < recordSpacing) return;   // 안 움직였다 → 기록하지 않는다

            // 워프·귀환석으로 건너뛴 경우. 이전 지역 좌표를 남겨두면 그쪽으로 되돌려 보낸다.
            if (moved > maxEscapeDistance) _history.Clear();
        }

        _history.Add(p);
        if (_history.Count > historyCapacity)
            _history.RemoveRange(0, _history.Count - historyCapacity);
    }

    // ── 사용 가부 ───────────────────────────────────────────────────────
    /// 지금 탈출할 수 있는가. 설정창 버튼이 누르기 전에 물어본다.
    /// 여기 남은 검사는 전부 '기술적으로 불가능한 경우'뿐이다(게임적 제한 없음).
    public bool CanEscape(out string reason)
    {
        reason = null;
        if (_busy) return false;   // 이미 연출 중 — 조용히 무시

        if (_player == null || _player.Stat == null || _player.Stat.IsDead)
        {
            reason = Loc.Get("지금은 탈출할 수 없습니다.");
            return false;
        }
        if (InCombat)
        {
            reason = Loc.Get("전투 중에는 탈출할 수 없습니다.");
            return false;
        }
        if (!TryPickDestination(out _))
        {
            reason = Loc.Get("돌아갈 안전한 위치를 찾지 못했습니다.");
            return false;
        }
        return true;
    }

    /// 실제 탈출. 설정창이 닫힌 뒤에 호출된다.
    public void EscapeNow()
    {
        if (_busy) return;
        StartCoroutine(EscapeRoutine());
    }

    private IEnumerator EscapeRoutine()
    {
        _busy = true;
        EnsureFader();

        bool prevBlocked = PlayerInputComponent.IsBlocked;
        PlayerInputComponent.IsBlocked = true;

        // 진행 중이던 액션은 끊는다 — 모션만 끊기고 이펙트가 남는 것을 막는다.
        _player.Skill?.Interrupt();
        _player.Dash?.CancelDash();

        yield return Fade(0f, 1f);
        yield return new WaitForSecondsRealtime(blackHold);

        // 목적지는 '검은 화면이 된 뒤' 다시 고른다 — 설정창을 닫는 사이에 상황이 바뀌었을 수 있다.
        if (TryPickDestination(out Vector3 dest))
        {
            // 방금 빠져나온 자리 주변 좌표는 걷어낸다(다시 그 안으로 보내지 않으려고).
            // 이력을 통째로 비우지는 않는다 — 비우면 탈출 직후 다시 빠졌을 때 갈 곳이 없다.
            Vector3 from = transform.position;
            _history.RemoveAll(h => Flat2(h, from) < minEscapeDistance * 2f);

            Teleport(dest);
            yield return Fade(1f, 0f);
            ToastManager.Success(Loc.Get("안전한 위치로 이동했습니다."));
        }
        else
        {
            yield return Fade(1f, 0f);
            ToastManager.Warning(Loc.Get("돌아갈 안전한 위치를 찾지 못했습니다."));
        }

        PlayerInputComponent.IsBlocked = prevBlocked;
        _busy = false;
    }

    // ── 목적지 ──────────────────────────────────────────────────────────
    /// 이력에서 되돌아갈 자리를 고른다. 최신 것부터 훑어 첫 번째로 유효한 좌표.
    /// 하나도 없으면 마지막 접지 지점을 마지막 후보로 본다.
    private bool TryPickDestination(out Vector3 dest)
    {
        Vector3 here = transform.position;

        // 1순위: 충분히 떨어진 자리 중 가장 최신.
        //   구덩이에 갇힌 경우, 안에서 맴돌며 쌓인 좌표는 전부 가까워서 여기서 걸러진다.
        //   → 자연히 '구덩이에 들어오기 전' 좌표가 뽑힌다. 갇혔는지 판정할 필요가 없다.
        for (int i = _history.Count - 1; i >= 0; i--)
            if (IsValidDestination(_history[i], here, preferEscapeDistance, out dest)) return true;

        // 2순위: 그만큼 떨어진 자리가 없으면(막 출발했거나 좁은 통로) 가까운 자리라도.
        for (int i = _history.Count - 1; i >= 0; i--)
            if (IsValidDestination(_history[i], here, minEscapeDistance, out dest)) return true;

        var mv = _player.Movement;
        if (mv != null && IsValidDestination(mv.LastGroundedPosition, here, minEscapeDistance, out dest)) return true;

        dest = here;
        return false;
    }

    /// 후보가 실제로 설 수 있는 자리인지 확인하고, 지면 높이에 맞춰 스냅한다.
    /// ★NavMesh 를 쓰지 않는다. 이 좌표는 플레이어가 실제로 서 있던 자리이고,
    ///   지금 갈 수 있는지는 캡슐이 들어가는지로 직접 확인하는 편이 확실하다.
    ///   (막아 둔 콜라이더가 그 사이에 생겨도 겹침 검사에서 걸러진다)
    private bool IsValidDestination(Vector3 candidate, Vector3 here, float minDistance, out Vector3 result)
    {
        result = candidate;

        float flat = Flat2(candidate, here);
        if (flat < minDistance || flat > maxEscapeDistance) return false;

        if (!SnapToGround(candidate, out Vector3 grounded)) return false;
        if (!IsClear(grounded)) return false;

        result = grounded;
        return true;
    }

    /// 그 자리에 원래 크기 캡슐이 들어가는가.
    private bool IsClear(Vector3 footPos)
    {
        if (_capsule == null) return true;

        float scaleXZ = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        float scaleY  = transform.lossyScale.y;
        float r    = _capsule.radius * scaleXZ * 0.95f;   // 지면·벽에 살짝 닿는 정도는 허용
        float half = _capsule.height * 0.5f * scaleY;
        float seg  = Mathf.Max(0f, half - r);

        Vector3 c  = footPos + transform.TransformVector(_capsule.center);
        int n = Physics.OverlapCapsuleNonAlloc(c + Vector3.up * seg, c - Vector3.up * seg, r,
                                               _overlap, _blockMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = _overlap[i];
            if (col == null) continue;
            if (col.transform.root == transform.root) continue;   // 자기 콜라이더
            return false;
        }
        return true;
    }

    /// 후보 좌표 위에서 아래로 쏴서 지면에 앉힌다.
    /// GroundMask 를 1순위로 쓰되, 못 맞히면 플레이어 외 아무 표면으로 폴백한다
    /// (새로 깐 터레인/오브젝트가 GroundMask 에 빠져 있어도 동작하게).
    private bool SnapToGround(Vector3 candidate, out Vector3 result)
    {
        result = candidate;

        Vector3 origin = candidate + Vector3.up * 3f;
        const float MaxDist = 12f;

        // ★h 를 조건식 안에서 out var 로 선언하면 단락 평가 때 '미할당'이 되어 컴파일이 막힌다.
        RaycastHit h = default;
        int mask = _player.Movement != null ? _player.Movement.GroundMask.value : 0;
        bool hit = mask != 0 && Physics.Raycast(origin, Vector3.down, out h, MaxDist,
                                                mask, QueryTriggerInteraction.Ignore);
        if (!hit)
        {
            int notPlayer = ~(1 << gameObject.layer);
            hit = Physics.Raycast(origin, Vector3.down, out h, MaxDist, notPlayer,
                                  QueryTriggerInteraction.Ignore);
        }
        if (!hit) return false;

        float bottomOffset = _capsule != null
            ? (_capsule.height * 0.5f - _capsule.center.y) * transform.lossyScale.y
            : 0f;
        result = h.point + Vector3.up * (bottomOffset + 0.05f);
        return true;
    }

    // WarpManager / ReturnStoneManager 와 같은 방식 — Rigidbody 위치를 직접 옮기고 속도를 지운다.
    private void Teleport(Vector3 dest)
    {
        if (_rb != null)
        {
            _rb.position = dest;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        transform.position = dest;
    }

    private static float Flat2(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    // ── 페이드 (코드로 만드는 전용 오버레이) ────────────────────────────
    private void EnsureFader()
    {
        if (_fadeCg != null) return;

        var go = new GameObject("StuckEscapeFade", typeof(Canvas), typeof(CanvasGroup));
        go.transform.SetParent(transform, false);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;   // 모든 UI 위

        var imgGo = new GameObject("Black", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        imgGo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = imgGo.GetComponent<UnityEngine.UI.Image>();
        img.color = Color.black;
        img.raycastTarget = false;

        _fadeCg = go.GetComponent<CanvasGroup>();
        _fadeCg.alpha = 0f;
        _fadeCg.blocksRaycasts = false;
        _fadeCg.interactable = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (_fadeCg == null || fadeTime <= 0f)
        {
            if (_fadeCg != null) _fadeCg.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;   // 정지 중에도 진행
            _fadeCg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeTime));
            yield return null;
        }
        _fadeCg.alpha = to;
    }
}
