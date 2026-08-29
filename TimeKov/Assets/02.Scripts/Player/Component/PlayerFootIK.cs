using UnityEngine;

/// <summary>
/// 발을 실제 지면 높이·기울기에 맞춰 놓는다(Foot IK).
///
/// [왜 필요한가]
///   이 게임의 지형은 통짜 Terrain 이라 경사가 어디에나 있다. 그런데 걷기/달리기 클립은
///   평지 기준으로 만들어져 있어서, 경사에 서면 산 쪽 발은 땅에 파묻히고 골짜기 쪽 발은
///   허공에 뜬다. 캐릭터가 지면 위에 '떠 있는' 것처럼 보이는 가장 큰 원인이다.
///
/// [어떻게]
///   1) 각 발 아래로 레이를 쏴 실제 지면 높이를 찾는다.
///   2) 더 많이 내려가야 하는 발에 맞춰 골반을 내린다.
///      ★골반을 먼저 내려야 한다. 안 내리고 발만 끌어내리면 다리가 쭉 펴져 '찢어진다'.
///   3) 두 발을 각자 지면 위에 놓고, 지면 기울기에 맞춰 발목을 돌린다.
///
/// [붙이는 곳]
///   ★Animator 와 '같은' 게임오브젝트여야 한다. OnAnimatorIK 는 Animator 가 붙은
///     오브젝트의 스크립트에만 호출된다. 이 프로젝트는 Animator 가 플레이어의 자식에 있다.
///     Tools/TIMEKOV/애니메이션/발 IK 켜기 메뉴가 알아서 붙여 준다.
///
/// [애니메이터 설정]
///   해당 레이어의 IK Pass 가 켜져 있어야 OnAnimatorIK 자체가 안 불린다. 위 메뉴가 같이 켠다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerFootIK : MonoBehaviour
{
    [Header("지면 탐지")]
    [Tooltip("발 위 이 높이에서부터 아래로 훑는다(m). 발이 지형에 파묻힌 상태에서도 찾으려면 필요.")]
    [Range(0.1f, 2f)] public float probeUp = 0.7f;

    [Tooltip("발밑으로 이만큼까지 지면을 찾는다(m). 이보다 낮으면 '발 디딜 곳 없음'으로 보고 보정하지 않는다.")]
    [Range(0.1f, 3f)] public float probeDown = 1.6f;

    [Tooltip("지면으로 인정할 레이어. 플레이어 이동의 Ground Mask 와 같게 맞춘다.")]
    public LayerMask groundMask = ~0;

    [Header("보정")]
    [Tooltip("발목뼈가 발바닥보다 얼마나 위에 있는지(m). 신발 두께까지 포함해 맞춘다.\n" +
             "발이 땅에 묻히면 올리고, 떠 보이면 내린다. 이 값 하나가 제일 중요하다.")]
    [Range(0f, 0.3f)] public float ankleHeight = 0.1f;

    [Tooltip("골반을 내릴 수 있는 최대 깊이(m). 낙차가 큰 곳에서 다리가 찢어지지 않게 막는다.")]
    [Range(0f, 1.2f)] public float maxPelvisDrop = 0.6f;

    [Header("동작 조건")]
    [Tooltip("이 속도(m/s) 이하일 때만 보정을 건다. 넘어서면 꺼진다.\n" +
             "★걷고 뛸 때는 끄는 게 맞다. 이동 중 보정을 걸려면 어느 발이 땅을 딛는 중인지\n" +
             "  알아야 하는데, 그 정보는 애니메이션 클립에 IK 커브로 들어 있어야 한다.\n" +
             "  없으면 발 높이로 추측할 수밖에 없고, 그러면 뒷발이 끌리거나 다리가 튄다.")]
    [Range(0f, 2f)] public float standSpeed = 0.2f;

    [Tooltip("위 속도를 넘어서면 이 구간(m/s)에 걸쳐 보정이 완전히 꺼진다.\n" +
             "짧으면 걷기 시작할 때 발이 뚝 풀리고, 길면 걷는 동안에도 조금 남는다.")]
    [Range(0.1f, 4f)] public float moveFade = 1f;

    [Header("보정 한계")]
    [Tooltip("한계를 '플레이어가 설 수 있는 최대 경사'에서 자동으로 계산한다(권장).\n" +
             "PlayerMovementComponent 의 Max Slope Angle 과 지금 두 발 간격으로,\n" +
             "그 경사에서 필요한 만큼을 매 프레임 구한다.\n" +
             "→ 설 수 있는 경사면 어디서든 발이 반드시 지면에 닿는다.\n" +
             "끄면 아래 수동 값을 쓴다.")]
    public bool autoLimits = true;

    [Tooltip("자동 계산에 곱하는 여유. 1 = 딱 필요한 만큼, 1.5 = 50% 여유(권장).\n" +
             "울퉁불퉁한 지형에서는 한 발이 돌부리를 밟아 계산보다 더 벌어질 수 있다.")]
    [Range(1f, 3f)] public float limitMargin = 1.5f;

    [Tooltip("발을 원래 자리에서 이만큼까지만 옮긴다(m). 넘어서면 보정이 서서히 빠진다.\n" +
             "Auto Limits 가 꺼져 있을 때만 쓰인다.\n" +
             "★무리하게 끌어당기면 무릎이 다 펴져 다리가 뒤로 꺾이거나 휙 돌아간다.")]
    [Range(0.05f, 1.2f)] public float maxFootReach = 0.55f;

    [Tooltip("보정이 붙고 빠지는 빠르기. 클수록 즉각적, 작을수록 부드럽다.\n" +
             "낮으면 계단에서 발이 늦게 따라오고, 높으면 요철에서 다리가 떤다.")]
    [Range(1f, 30f)] public float smooth = 12f;

    [Header("발 각도")]
    [Tooltip("발을 지면 기울기에 맞춰 눕힌다. 끄면 위치만 맞춘다.")]
    public bool alignRotation = true;

    [Tooltip("이 각도를 넘는 면에서는 발을 안 눕힌다(도). 벽이나 절벽에서 발이 뒤집히는 것 방지.")]
    [Range(0f, 70f)] public float maxSurfaceAngle = 45f;

    [Header("진단")]
    [Tooltip("씬 뷰에 발마다 탐침·지면·목표를 그린다. 발이 뜨거나 이상할 때 켜서 원인을 눈으로 본다.\n" +
             "  노란 선 = 탐침 범위,  초록 점 = 찾은 지면,  파란 점 = 발을 놓을 자리\n" +
             "  빨간 X  = 지면을 못 찾음(= 이 발은 보정 안 됨. 탐침 거리를 늘려야 한다)\n" +
             "  선 굵기 = 보정 세기(가늘수록 약하게 걸린다)")]
    public bool debugDraw;

    private Animator _anim;
    private PlayerMovementComponent _movement;
    private PlayerStatComponent _stat;

    // 0~1 로 서서히 오가는 전체 세기. 공중에서는 0 이 된다.
    private float _weight;

    // 골반 내림량(m, 음수). 급변을 막으려고 따로 들고 부드럽게 따라가게 한다.
    private float _pelvisDrop;

    // 발 높이도 부드럽게 따라가야 계단·턱에서 툭 튀지 않는다.
    private float _leftY, _rightY;
    private bool  _leftInit, _rightInit;

    // 공격·스킬이 재생되는 레이어. 그 동안은 보정을 끈다.
    private int _actionLayer = -1;

    // 이번 프레임에 실제로 쓰는 한계값(자동 계산 또는 수동값).
    private float _reachLimit, _pelvisLimit;

    private void Awake()
    {
        _anim     = GetComponent<Animator>();
        _movement = GetComponentInParent<PlayerMovementComponent>();
        _stat     = GetComponentInParent<PlayerStatComponent>();
        _actionLayer = _anim != null ? _anim.GetLayerIndex("Action Layer") : -1;

        // 이동 쪽 지면 마스크를 그대로 물려받는다 — 둘이 다른 바닥을 보면 발이 엉뚱한 데 붙는다.
        if (_movement != null && _movement.GroundMask.value != 0)
            groundMask = _movement.GroundMask;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        // Base Layer 에서만 건다. 공격·점프 레이어까지 각자 부르면 같은 보정이 여러 번 겹친다.
        if (_anim == null || layerIndex != 0) return;
        if (!_anim.isHuman) return;

        // 공중에서는 끈다. 떠 있는데 발을 땅에 붙이면 다리가 늘어나거나 몸이 끌려 내려간다.
        // 죽었을 때도 끈다 — 쓰러지는 자세에서 발만 서 있으려 하면 우스워진다.
        bool on = (_movement == null || _movement.IsGrounded)
               && (_stat == null || !_stat.IsDead)
               && !InAction()
               && (_movement == null || !_movement.OnSteepSlope);   // 못 서는 경사에서는 안 나온다

        // ★그리고 '제자리일 때만' 건다.
        //   이동 중 보정을 걸려면 매 순간 어느 발이 땅을 딛는 중인지 알아야 하는데, 그건
        //   애니메이션 클립에 IK 커브로 authoring 돼 있어야 알 수 있다. 이 프로젝트 클립엔
        //   없어서 발 높이로 추측해야 했고, 어떤 기준을 잡아도 한쪽으로 어긋났다 —
        //   뒷발이 바닥에 끌리거나, 경사에서 다리가 튀거나, 걷기/달리기 자세가 무너졌다.
        //   경사에 서 있을 때가 발 뜸이 제일 눈에 띄는 상황이니 거기에만 쓰고, 이동 중에는
        //   원래 애니메이션을 그대로 둔다. 나중에 클립에 IK 커브가 생기면 그때 다시 켜면 된다.
        float target = on ? 1f - MoveFactor() : 0f;

        // 프레임레이트에 안정적인 지수 감쇠(1-e^-kt). 어느 프레임에서도 같은 속도로 붙고 빠진다.
        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        _weight = Mathf.Lerp(_weight, target, t);

        if (_weight <= 0.001f)
        {
            // 완전히 꺼진 동안은 다음에 켜질 때 튀지 않게 상태를 비워 둔다.
            _leftInit = _rightInit = false;
            _dbgL = default; _dbgR = default;   // 기즈모에 지난 프레임이 남지 않게
            _pelvisDrop = 0f;
            ClearGoal(AvatarIKGoal.LeftFoot);
            ClearGoal(AvatarIKGoal.RightFoot);
            return;
        }

        FootProbe l = Probe(AvatarIKGoal.LeftFoot);
        FootProbe r = Probe(AvatarIKGoal.RightFoot);

        UpdateLimits(l, r);

        // ── 1. 어느 발에 걸지 ─────────────────────────────────────────────
        // 여기까지 왔다는 건 '제자리에 서 있다'는 뜻이다. 서 있으면 두 발 모두 땅을 딛고 있는
        // 게 맞으니, 지면을 찾은 발에는 전부 건다. 어느 발이 디딤인지 고를 이유가 없다.
        //   ★'턱 너머'에 있는 발은 아예 뺀다(hasRoot 일 때). 골반 계산에서만 빼고 발은 절반쯤
        //     끌어당기면, 낭떠러지 쪽 다리가 허공으로 어정쩡하게 늘어난다. 뺄 거면 둘 다 빼야 한다.
        bool hasRoot = RootGroundY(out float rootY);
        l.weight = Plantable(l, hasRoot, rootY) ? 1f : 0f;
        r.weight = Plantable(r, hasRoot, rootY) ? 1f : 0f;

        // ── 2. 골반 내리기 ────────────────────────────────────────────────
        // '몸 밑 지면' 보다 '발밑 지면' 이 더 낮으면 그 차이만큼 내린다. 순수하게 지형만 본다.
        //
        // ★애니메이션의 발 높이를 계산에 넣으면 안 된다. 두 번 그렇게 만들었다가 두 번 다
        //   런지 자세가 나왔다. 걸을 때는 한쪽 발이 늘 들려 있어서, 그 발을 조금이라도 세면
        //   골반이 계속 내려간 채로 유지되기 때문이다. 세기(weight)를 곱해 걸러 보려 해도
        //   발이 오르내리는 중간 구간에서는 세기가 0 이 아니라 결국 새어 나온다.
        //   지형 높이만 보면 평지에서는 어떤 자세로 걷든 차이가 0 이라 골반이 절대 안 내려간다.
        float want = 0f;
        if (hasRoot)
        {
            // 디딜 수 있다고 판정된 발만 본다(위 Plantable 로 이미 걸러졌다).
            if (l.weight > 0f) want = Mathf.Min(want, l.groundY - rootY);
            if (r.weight > 0f) want = Mathf.Min(want, r.groundY - rootY);
        }
        want = Mathf.Max(want, -_pelvisLimit);

        _pelvisDrop = Mathf.Lerp(_pelvisDrop, want, t);
        _anim.bodyPosition += Vector3.up * (_pelvisDrop * _weight);

        // ── 3. 과신전 방지 ────────────────────────────────────────────────
        // ★골반을 내린 '뒤'의 실제 뻗음으로 판단한다. 내려가기 전 값으로 재면 골반이 흡수해 줄
        //   몫까지 세어서, 멀쩡히 닿을 수 있는 발까지 보정을 잘라 버린다.
        l.weight *= ReachWeight(l);
        r.weight *= ReachWeight(r);

        // ── 4. 발 놓기 ────────────────────────────────────────────────────
        // 골반을 옮긴 뒤에 건다. 목표 높이는 월드 기준(지면 위)이라 골반이 움직여도 그대로다.
        ApplyFoot(AvatarIKGoal.LeftFoot,  l, ref _leftY,  ref _leftInit,  t);
        ApplyFoot(AvatarIKGoal.RightFoot, r, ref _rightY, ref _rightInit, t);


        _dbgL = l; _dbgR = r;
    }

    // ── 진단용 ────────────────────────────────────────────────────────────
    private FootProbe _dbgL, _dbgR;

    private void OnDrawGizmos()
    {
        if (!debugDraw || !Application.isPlaying) return;
        DrawFoot(_dbgL);
        DrawFoot(_dbgR);
    }

    private void DrawFoot(FootProbe p)
    {
        if (p.curY == 0f) return;   // 아직 한 번도 안 잰 상태

        // 탐침이 어디서 어디까지 훑었는지. 원래 발 자리를 X,Z 기준으로 쓴다.
        Vector3 foot = new Vector3(p.pos.x, p.curY, p.pos.z);
        Vector3 top  = foot + Vector3.up * probeUp;
        Vector3 bot  = foot - Vector3.up * probeDown;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(top, bot);

        if (!p.hit)
        {
            // 지면을 못 찾았다 = 이 발은 보정이 안 걸린다. 탐침 거리를 늘려야 하는 상황.
            Gizmos.color = Color.red;
            Gizmos.DrawLine(bot + Vector3.left * 0.1f + Vector3.forward * 0.1f,
                            bot + Vector3.right * 0.1f + Vector3.back * 0.1f);
            Gizmos.DrawLine(bot + Vector3.left * 0.1f + Vector3.back * 0.1f,
                            bot + Vector3.right * 0.1f + Vector3.forward * 0.1f);
            return;
        }

        Gizmos.color = Color.green;                                  // 찾은 지면
        Gizmos.DrawSphere(new Vector3(p.pos.x, p.groundY, p.pos.z), 0.03f);

        Gizmos.color = Color.cyan;                                   // 발을 놓을 자리
        Gizmos.DrawSphere(p.pos, 0.03f);

        // 세기를 선 길이로 보여 준다(짧으면 약하게 걸린 것).
        Gizmos.color = Color.Lerp(Color.red, Color.green, p.weight);
        Gizmos.DrawLine(p.pos, p.pos + Vector3.up * (0.3f * p.weight));
    }

    /// <summary>발 한 짝의 탐지 결과.</summary>
    private struct FootProbe
    {
        public bool       hit;      // 발밑에서 지면을 찾았는가
        public float      groundY;  // 지면 높이(월드). 골반 계산의 기준.
        public float      curY;     // 애니메이션이 놓은 원래 발 높이. 디딤 판정의 기준.
        public Vector3    pos;      // 발이 놓일 자리
        public Quaternion rot;      // 지면 기울기에 맞춘 발 회전
        public float      weight;   // 이 발에 보정을 얼마나 걸지(0~1)
    }

    /// <summary>이번 프레임의 보정 한계를 정한다.
    ///
    /// ★자동 모드에서는 '플레이어가 설 수 있는 최대 경사'에서 필요한 값을 그대로 계산한다.
    ///   두 발이 s 만큼 벌어져 있고 경사가 θ 면, 두 발밑 지면의 높이 차는 s·tanθ 다.
    ///   그만큼(+여유)을 허용하면 설 수 있는 경사에서는 한계에 걸릴 일이 없다 —
    ///   즉 '설 수 있는 경사면 어디서든 발이 지면에 닿는다'가 보장된다.
    ///   숫자를 손으로 맞추면 경사 각도가 어느 선을 넘는 순간 발이 뜨는데, 그 선이 어디인지
    ///   눈으로 찾아야 한다. 기준을 이동 쪽 설정에서 끌어오면 그 선이 저절로 맞는다.</summary>
    private void UpdateLimits(FootProbe l, FootProbe r)
    {
        if (!autoLimits)
        {
            _reachLimit  = maxFootReach;
            _pelvisLimit = maxPelvisDrop;
            return;
        }

        // 두 발 사이 수평 거리. 자세에 따라 매 프레임 달라지므로 그때그때 잰다.
        float spread = new Vector2(l.pos.x - r.pos.x, l.pos.z - r.pos.z).magnitude;

        float slope = _movement != null ? _movement.MaxSlopeAngle : 45f;
        float worst = spread * Mathf.Tan(Mathf.Clamp(slope, 1f, 80f) * Mathf.Deg2Rad);

        // 최소 바닥값을 둔다 — 두 발이 거의 붙는 자세에서는 계산값이 0 에 가까워지는데,
        // 그러면 작은 돌부리 하나도 못 넘는다.
        _reachLimit  = Mathf.Max(0.2f, worst * limitMargin);
        _pelvisLimit = Mathf.Max(0.2f, worst * limitMargin);
    }

    /// <summary>공격·스킬·대시·채널링처럼 Action Layer 가 잡고 있는 동작 중인가.
    ///
    /// ★이때는 보정을 꺼야 한다. 이 동작들은 제자리에서 나오지만(=이동 속도 0 이라 위 조건은
    ///   통과한다) 발을 떼거나 몸을 던지는 구간이 있다. 발을 땅에 묶어 두면 그 구간이 뭉개지고,
    ///   심하면 다리가 끌리거나 꺾인다.
    /// Action Layer 가 Empty 면 아무 동작도 안 하는 상태다.</summary>
    private bool InAction()
    {
        if (_actionLayer < 0) return false;
        return !_anim.GetCurrentAnimatorStateInfo(_actionLayer).IsName("Empty")
            || _anim.IsInTransition(_actionLayer);
    }

    /// <summary>지금 얼마나 '이동 중'인가(0=제자리, 1=완전히 이동 중).
    /// 이 값만큼 보정을 뺀다 — 제자리에서만 걸고, 걸음이 붙으면 꺼진다.</summary>
    private float MoveFactor()
    {
        if (_movement == null) return 1f;
        return Mathf.Clamp01((_movement.CurrentSpeed - standSpeed) / Mathf.Max(0.01f, moveFade));
    }


    /// <summary>다리가 무리하게 펴지지 않도록 세기를 깎는 계수(0~1).
    /// 골반을 내린 뒤 남은 뻗음으로 잰다 — 골반이 흡수해 줄 몫까지 세면 안 되기 때문이다.</summary>
    private float ReachWeight(FootProbe p)
    {
        if (!p.hit) return 0f;

        float reach = Mathf.Abs(p.pos.y - (p.curY + _pelvisDrop));
        if (reach <= _reachLimit) return 1f;

        return Mathf.Clamp01(1f - (reach - _reachLimit) / Mathf.Max(0.01f, _reachLimit));
    }

    /// <summary>이 발을 지면에 붙여도 되는가.
    ///
    /// ★'설 수 있는 경사에서 나올 수 있는 낙차' 를 넘으면 0 으로 친다. 그보다 깊다는 건 그 발이
    ///   비탈이 아니라 '턱 너머'(절벽 가장자리·구덩이)에 있다는 뜻이다. 그 깊이를 몸에 반영하면
    ///   절벽 끝에 설 때마다 골반이 한계까지 푹 꺼지고, 그 바람에 단단한 땅을 밟은 발은 지면에
    ///   파묻히고 반대쪽 발은 허공에 남는다 — 경사도 아닌데 발이 뜨던 원인이다.
    ///   이런 발은 보정에서 빼고 애니메이션 자세 그대로 두는 게 맞다.</summary>
    private bool Plantable(FootProbe p, bool hasRoot, float rootY)
    {
        if (!p.hit) return false;
        if (!hasRoot) return true;                   // 기준선을 못 구하면 판단 보류 — 그냥 건다

        float drop = p.groundY - rootY;              // 음수 = 발밑이 몸보다 낮다
        return drop >= -_pelvisLimit;                // 너무 깊으면 턱 너머 — 손대지 않는다
    }

    /// <summary>몸 바로 밑의 지면 높이. 골반을 얼마나 내릴지 재는 기준선이다.
    /// 발이 아니라 몸을 기준으로 잡아야 걷는 동작(발이 오르내리는 것)에 흔들리지 않는다.</summary>
    private bool RootGroundY(out float y)
    {
        y = 0f;
        Vector3 origin = transform.position + Vector3.up * probeUp;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                             probeUp + probeDown, groundMask, QueryTriggerInteraction.Ignore))
            return false;

        y = hit.point.y;
        return true;
    }

    /// <summary>발 아래 지면을 찾고, 이 발에 보정을 얼마나 걸지까지 정한다.</summary>
    private FootProbe Probe(AvatarIKGoal goal)
    {
        var p = new FootProbe();
        Vector3 cur = _anim.GetIKPosition(goal);
        p.curY = cur.y;      // 지면을 못 찾아도 채워 둔다 — 반대쪽 발의 디딤 판정 기준이 된다
        p.pos  = cur;
        p.rot  = _anim.GetIKRotation(goal);

        Vector3 origin = cur + Vector3.up * probeUp;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                             probeUp + probeDown, groundMask, QueryTriggerInteraction.Ignore))
            return p;   // hit=false, weight=0

        p.hit     = true;
        p.groundY = hit.point.y;
        p.pos     = new Vector3(cur.x, hit.point.y + ankleHeight, cur.z);
        // 세기(weight)는 호출한 쪽에서 정한다.

        if (alignRotation)
        {
            // 너무 가파른 면에서는 안 눕힌다 — 절벽에 발을 붙이면 발목이 꺾인 것처럼 보인다.
            Vector3 n = Vector3.Angle(Vector3.up, hit.normal) <= maxSurfaceAngle ? hit.normal : Vector3.up;
            p.rot = Quaternion.FromToRotation(Vector3.up, n) * p.rot;
        }
        return p;
    }

    /// <summary>발 하나에 IK 를 건다. 높이는 부드럽게 따라가게 해 계단·턱에서 툭 튀지 않게 한다.</summary>
    private void ApplyFoot(AvatarIKGoal goal, FootProbe p, ref float smoothedY, ref bool init, float t)
    {
        float w = _weight * p.weight;
        if (!p.hit || w <= 0.001f) { ClearGoal(goal); init = false; return; }

        // 처음 켜지는 프레임은 보간하지 않는다. 지난 값에서 출발하면 발이 엉뚱한 데서 날아온다.
        //   ★발마다 따로 기억한다. 하나로 묶어 두면, 한쪽 발만 지면을 놓쳤다가 되찾을 때
        //     그 발이 아주 오래된 높이에서부터 미끄러져 들어온다.
        smoothedY = init ? Mathf.Lerp(smoothedY, p.pos.y, t) : p.pos.y;
        init = true;

        Vector3 pos = p.pos;
        pos.y = smoothedY;

        _anim.SetIKPositionWeight(goal, w);
        _anim.SetIKPosition(goal, pos);

        // ★회전 세기는 끄는 경우에도 매 프레임 0 으로 눌러 준다. IK 값은 안 건드리면 지난 값이
        //   그대로 남아서, alignRotation 을 플레이 중에 끄면 마지막 각도로 발이 굳는다.
        _anim.SetIKRotationWeight(goal, alignRotation ? w : 0f);
        if (alignRotation) _anim.SetIKRotation(goal, p.rot);
    }

    private void ClearGoal(AvatarIKGoal goal)
    {
        _anim.SetIKPositionWeight(goal, 0f);
        _anim.SetIKRotationWeight(goal, 0f);
    }
}
