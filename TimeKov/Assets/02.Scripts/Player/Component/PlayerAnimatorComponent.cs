using UnityEngine;

public class PlayerAnimatorComponent : MonoBehaviour
{
    private Player _player;
    private Animator _anim;

    private static readonly int NormalHash = Animator.StringToHash("----normal");
    private static readonly int Attack1Hash = Animator.StringToHash("ATTACK1");
    private static readonly int Attack2Hash = Animator.StringToHash("ATTACK2");
    private static readonly int Attack3Hash = Animator.StringToHash("ATTACK3");
    private static readonly int Skill1Hash = Animator.StringToHash("SP SKILL 1");
    private static readonly int Skill2Hash = Animator.StringToHash("SP SKILL 2");
    private static readonly int Skill3Hash = Animator.StringToHash("SP SKILL 3");
    private static readonly int DashFHash = Animator.StringToHash("QUICK SHIFT F");
    private static readonly int DashBHash = Animator.StringToHash("QUICK SHIFT B");
    private static readonly int DashRHash = Animator.StringToHash("QUICK SHIFT R");
    private static readonly int DashLHash = Animator.StringToHash("QUICK SHIFT L");
    private static readonly int HitLHash = Animator.StringToHash("Hit L");
    private static readonly int HitRHash = Animator.StringToHash("Hit R");
    private static readonly int DieHash = Animator.StringToHash("Die");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    // 피격 회복 직후 애니메이션 즉시 동기화용 타이머
    private bool  _prevIsHurt;
    private float _hurtRecoveryTimer;
    private const float HURT_RECOVERY_ANIM = 0.15f;

    // ── 점프 하체 레이어 ────────────────────────────────────────────────
    // 이 캐릭터의 기본 자세는 등 뒤로 대검을 든 모습이다. 점프 클립이 몸 전체를 덮어쓰면
    //   뛰는 순간 팔이 풀려 검이 엉뚱한 데로 간다. 그래서 점프는 하체만 재생하는 별도
    //   레이어로 빼 두고 상체는 Base Layer 를 그대로 쓴다.
    //   레이어 구성은 Tools/TIMEKOV/애니메이션 메뉴(JumpLayerBuilder)가 만든다.
    //   ★레이어가 없으면(도구 미적용) 아무 일도 하지 않는다 — 예전 동작 그대로.
    [Header("점프 하체 레이어")]
    [Tooltip("점프할 때 검을 안 든 팔이 따라 올라가는 정도.\n" +
             "1 = 클립 그대로(과하게 번쩍 듦), 0 = 전혀 안 움직임(뻣뻣함).\n" +
             "플레이 중에 바꿔가며 맞춰도 된다.")]
    [Range(0f, 1f)] [SerializeField] private float jumpArmWeight = 0.35f;

    [Tooltip("점프할 때 하체를 좌우로 트는 각도(도). 양수 = 오른쪽.\n" +
             "골반을 돌리고 척추에서 같은 만큼 되돌리기 때문에 다리만 틀어지고 상체는 그대로다.\n" +
             "0 이면 아무것도 하지 않는다.")]
    [Range(-45f, 45f)] [SerializeField] private float jumpLowerBodyYaw = 0f;

    [Tooltip("점프 시작 시 하체가 걷기 자세에서 점프 자세로 넘어가는 시간(초).\n" +
             "짧을수록 반응이 빠르고, 길수록 부드럽다. 너무 길면 도약 도입부가 묻힌다.")]
    [Range(0.01f, 0.3f)] [SerializeField] private float jumpBlendIn = 0.06f;

    [Tooltip("착지하며 하체가 걷기/대기 자세로 돌아오는 시간(초).\n" +
             "★들어갈 때보다 길게 잡는다. 착지는 자세 차이가 커서 짧으면 다리가 툭 끊긴다.")]
    [Range(0.01f, 0.5f)] [SerializeField] private float jumpBlendOut = 0.2f;

    [Tooltip("점프 클립의 어디에서 멈출지. 클립 전체를 1 로 봤을 때의 위치.\n" +
             "다리를 접어 올린 체공 자세가 나오는 지점으로 맞춘다.\n" +
             "여기서 멈춰 있다가, 착지 직전에 나머지(다리를 펴고 내리는 동작)를 재생한다.\n" +
             "플레이 중에 바꿔가며 눈으로 맞춰도 된다.")]
    [Range(0.05f, 0.95f)] [SerializeField] private float jumpHoldAt = 0.3f;

    [Tooltip("체공 중 몸이 기우뚱거리는 각도(도). 0 이면 완전히 멈춘다.\n" +
             "3~6 도면 떠 있는 느낌이 살고, 10 도를 넘기면 버둥거리는 것처럼 보인다.\n" +
             "★클립 재생 위치를 오가는 방식은 이 점프 클립(18프레임)에선 1프레임 남짓이라\n" +
             "  눈에 안 보였다. 그래서 뼈를 직접 흔든다.")]
    [Range(0f, 20f)] [SerializeField] private float jumpHoldSway = 8f;

    [Tooltip("체공 중 몸이 위아래로 뜨는 폭(m). 0 이면 안 움직인다.\n" +
             "0.02~0.05 정도가 자연스럽다. 크게 주면 발이 지면을 파고들 수 있다.")]
    [Range(0f, 0.2f)] [SerializeField] private float jumpHoldBob = 0.06f;

    [Tooltip("체공 흔들림의 빠르기(Hz). 1 이면 1초에 한 번 왕복한다.\n" +
             "느릴수록 떠 있는 느낌, 빠를수록 버둥거리는 느낌이 난다.")]
    [Range(0.1f, 2f)] [SerializeField] private float jumpHoldSwayHz = 0.55f;

    private const float JumpStateGrace = 0.15f;     // 점프 직후 접지가 아직 안 풀린 구간의 유예

    // 이미 땅을 밟은 뒤에는 점프 레이어를 이 비율만큼 빨리 뺀다.
    // 지면을 달리는데 점프 자세가 겹쳐 보이면 0.2 초도 눈에 확 띄기 때문.
    private const float LandedFadeScale = 0.25f;

    private const string JumpStateName = "Jump";
    private static readonly int JumpSpeedHash = Animator.StringToHash("JumpSpeed");

    private int _jumpLayer = -1;
    private int _jumpArmLayer = -1;
    private bool _hasJumpSpeed;      // JumpSpeed 파라미터가 컨트롤러에 있는가(도구 적용 여부)
    private float _jumpClipLength;   // 점프 클립 길이(초) — 한 번 찾으면 캐시
    private bool _holding;           // 지금 체공 자세로 멈춰(흔들리며) 있는가
    private float _jumpGrace;
    private bool _jumpHeld;   // 점프로 레이어를 붙잡고 있는가(착지하면 놓는다)
    private bool _leftGround; // 이번 점프에서 실제로 발이 땅에서 떨어진 적이 있는가
    private float _airTime;   // 점프 레이어를 붙잡은 채 공중에 떠 있은 시간(초). 흔들림 위상의 기준.

    private const float SwayRampIn = 0.2f;   // 흔들림이 최대 세기에 이르는 시간(초)

    // 발밑이 이 거리 안이면 '땅에 닿았다' 로 본다(m). 접지 플래그가 늦게 켜지는 구간을 메운다.
    private const float JumpLandDistance = 0.12f;

    // 착지 구간을 최대 이 배속까지 감는다. 더 올리면 다리가 파닥거리는 것처럼 보인다.
    private const float JumpLandMaxRate = 3f;
    private bool _jumpLayerActive;   // 이번 프레임에 점프 레이어가 하체를 맡고 있는가

    // 하체 각도 보정용 뼈. 골반을 돌리고 척추에서 되돌리면 다리만 틀어진다.
    private Transform _hipsBone, _spineBone;

    void Awake()
    {
        _player = GetComponent<Player>();
        _anim = GetComponentInChildren<Animator>();

        if (_anim != null)
        {
            _jumpLayer    = _anim.GetLayerIndex("Jump Layer");
            _jumpArmLayer = _anim.GetLayerIndex("Jump Arm Layer");

            // 없는 파라미터에 SetFloat 를 하면 에디터가 매 프레임 경고를 뱉는다 — 미리 확인해 둔다.
            foreach (var p in _anim.parameters)
                if (p.nameHash == JumpSpeedHash) { _hasJumpSpeed = true; break; }

            if (_anim.isHuman)
            {
                _hipsBone  = _anim.GetBoneTransform(HumanBodyBones.Hips);
                _spineBone = _anim.GetBoneTransform(HumanBodyBones.Spine);
            }
        }
    }

    /// <summary>점프 중 하체만 좌우로 튼다.
    ///
    /// 골반을 돌리면 그 아래(다리)가 전부 따라 돌지만 상체도 같이 돈다. 그래서 척추에서
    /// 같은 각도를 되돌려 상체는 원래 방향으로 남긴다 — 결과적으로 다리만 틀어진다.
    ///
    /// ★LateUpdate 에서 한다. 애니메이터가 이 프레임의 자세를 다 쓴 뒤라야 그 위에 덧씌울 수 있다.
    ///   Update 에서 하면 바로 덮어써져 아무 일도 안 일어난다.
    /// ★레이어 가중치를 곱한다. 점프가 시작·끝날 때 각도가 뚝 생겼다 사라지지 않고 같이 섞인다.</summary>
    void LateUpdate()
    {
        if (_jumpLayer < 0 || _anim == null) return;
        if (_hipsBone == null || _spineBone == null) return;

        float w = _anim.GetLayerWeight(_jumpLayer);
        if (w <= 0.001f) return;

        if (!Mathf.Approximately(jumpLowerBodyYaw, 0f))
        {
            // 캐릭터가 선 축을 기준으로 돈다(경사면에서도 어긋나지 않게).
            var yaw = Quaternion.AngleAxis(jumpLowerBodyYaw * w, transform.up);
            _hipsBone.rotation  = yaw * _hipsBone.rotation;
            _spineBone.rotation = Quaternion.Inverse(yaw) * _spineBone.rotation;
        }

        ApplyHoldSway(w);
    }

    /// <summary>체공 자세로 멈춰 있는 동안 몸을 아주 조금 흔들어 '살아 있게' 만든다.
    ///
    /// 클립은 JumpSpeed = 0 으로 세워 두고, 눈에 보이는 움직임만 여기서 얹는다.
    ///   ★원래는 클립의 재생 위치를 앞뒤로 오가게 했는데, 이 점프 클립이 18프레임밖에 안 돼서
    ///     흔들 수 있는 폭이 1프레임 남짓이었다 — 화면에선 그대로 정지로 보였다.
    ///
    /// 좌우 기울기·앞뒤 기울기·위아래 뜨기를 서로 다른 주기로 겹친다. 주기가 어긋나 있어야
    /// 같은 동작이 반복되는 티가 안 나고 떠 있는 것처럼 보인다.
    /// 레이어 가중치를 곱해서, 도약·착지에서 흔들림이 뚝 생겼다 사라지지 않게 한다.</summary>
    private void ApplyHoldSway(float weight)
    {
        // ★공중에 떠 있는 시간만 보고 판단한다. 예전엔 '클립을 멈춘 상태(_holding)' 일 때만
        //   흔들었는데, 그러면 JumpSpeed 파라미터가 없는 컨트롤러(도구 미적용)에서는 _holding 이
        //   영영 켜지지 않아 흔들림이 통째로 죽는다. 흔들림은 클립을 멈췄든 아니든 얹으면 된다.
        if (_airTime <= 0f) return;
        if (jumpHoldSway <= 0.01f && jumpHoldBob <= 0.001f) return;

        // 도약 직후에는 살짝 눌러 둔다 — 뛰어오르는 순간부터 흔들리면 도약이 흐릿해진다.
        float ramp = Mathf.Clamp01(_airTime / SwayRampIn);
        float amt  = weight * ramp;

        float p = _airTime * jumpHoldSwayHz * 2f * Mathf.PI;

        if (jumpHoldSway > 0.01f)
        {
            float roll  = Mathf.Sin(p)                * jumpHoldSway * amt;
            float pitch = Mathf.Sin(p * 0.73f + 1.1f) * jumpHoldSway * 0.6f * amt;

            // 골반만 돌린다 — 상체는 Base Layer 의 검 자세를 그대로 둬야 하므로 척추에서 되돌린다.
            var sway = Quaternion.AngleAxis(roll,  transform.forward)
                     * Quaternion.AngleAxis(pitch, transform.right);
            _hipsBone.rotation  = sway * _hipsBone.rotation;
            _spineBone.rotation = Quaternion.Inverse(sway) * _spineBone.rotation;
        }

        if (jumpHoldBob > 0.001f)
            _hipsBone.position += transform.up * (Mathf.Sin(p * 1.31f) * jumpHoldBob * amt);
    }

    void Update()
    {
        // ★점프 레이어를 먼저 갱신한다. UpdateMovement 가 그 결과(_jumpLayerActive)를 보고
        //   상체를 Idle 로 둘지 정하기 때문에, 순서가 바뀌면 한 프레임 묵은 값을 쓰게 된다.
        UpdateJumpLayer();
        UpdateMovement();
    }

    private void UpdateJumpLayer()
    {
        if (_jumpLayer < 0 || _anim == null) return;

        if (_jumpGrace > 0f) _jumpGrace -= Time.deltaTime;

        // 착지하면 놓는다. 유예 동안은 붙잡는다 — 뛴 직후 몇 프레임은 아직 접지로 잡혀서,
        //   그때 놓으면 점프 도입부가 통째로 사라진다.
        //   ★'공중인가'로만 판단하면 안 된다. 절벽에서 걸어 떨어질 때는 점프를 누른 적이 없어
        //     레이어가 Empty 인데, 그때 가중치를 올리면 모션 없는 Empty 가 하체를 덮어써
        //     다리가 통째로 망가진다. 그래서 '점프를 눌렀는가'만 본다.
        // ★접지 플래그(IsGrounded)만 믿지 않는다.
        //   도약 직후 일정 시간은 '점프로 올라가는 중' 으로 보려고 접지 판정을 일부러 꺼 두는데
        //   (GroundCheck 의 _jumpRiseGuard), 오르막에서는 지면이 마중 나와서 그 시간이 끝나기
        //   전에 이미 착지한다. 그러면 발은 땅에 붙어 달리는데 플래그는 아직 '공중' 이라,
        //   점프 클립이 끝까지 다 재생되고 나서야 이동 자세로 돌아왔다.
        //   -> 발밑 거리를 같이 본다. 이건 플래그와 달리 언제나 사실대로다.
        bool grounded = false;
        if (_player != null && _player.Movement != null)
        {
            grounded = _player.Movement.IsGrounded
                    || _player.Movement.GroundDistance <= JumpLandDistance;
        }

        // ★유예는 '아직 땅에서 발이 안 떨어진 구간' 에만 쓴다.
        //   한 번이라도 공중에 떴으면, 다시 땅에 닿는 순간 곧바로 놓는다. 안 그러면 경사로에서
        //   낮게 뛰었을 때(체공이 유예보다 짧다) 이미 지면을 달리고 있는데도 유예가 다 흐를
        //   때까지 점프 자세가 남아 있는다.
        if (!grounded) _leftGround = true;
        if (grounded && (_leftGround || _jumpGrace <= 0f))
        {
            _jumpHeld = false;
            _leftGround = false;
        }

        // ★죽으면 즉시 내린다. 사망 모션은 Action Layer(아래 레이어)에 있어서, 공중에서
        //   죽으면 위에 있는 점프 레이어가 다리를 계속 덮어써 '점프한 채 쓰러지는' 모양이 된다.
        bool dead = _player != null && _player.Stat != null && _player.Stat.IsDead;

        // ★낙하 전용 모션은 두지 않는다. 점프 클립은 반복 재생이 아니라서, 다 재생되고 나면
        //   그 상태에 머문 채 마지막 자세(공중 자세)를 그대로 유지한다. 착지하면 레이어
        //   가중치가 빠지며 이동 자세로 돌아온다 — 공중에 있는 동안은 점프 자세로 멈춰 있는 셈.
        //   그래서 여기서 볼 것은 '점프했는가' 하나뿐이다.
        //
        //   ★점프하지 않고 떨어지는 경우(절벽에서 걸어 나감)는 레이어를 올리지 않는다.
        //     그때는 이 레이어가 점프 자세를 잡고 있지 않아, 가중치만 올리면 빈 상태가
        //     하체를 덮어써 다리가 굳는다. 그냥 Base Layer 의 이동 자세를 그대로 둔다.
        bool active = _jumpHeld && !dead;
        _jumpLayerActive = active;

        // 공중에 떠 있은 시간 — 흔들림의 위상 기준. 땅에 닿으면 0 으로 되돌려,
        // 다음 점프가 항상 같은 지점(흔들림 없음)에서 시작하게 한다.
        if (active && !grounded) _airTime += Time.deltaTime;
        else                     _airTime = 0f;

        // 이미 땅을 밟고 있으면 훨씬 빠르게 뺀다. 지면을 달리는데 점프 자세가 겹쳐 보이면
        // 그 짧은 시간도 눈에 확 띈다. 공중에서 내리는 경우(사망 등)는 원래 시간을 쓴다.
        float outSeconds = grounded ? jumpBlendOut * LandedFadeScale : jumpBlendOut;

        FadeLayer(_jumpLayer, active ? 1f : 0f, outSeconds);
        // 팔은 같은 타이밍으로, 목표치만 낮춰 동작을 그만큼 옅게 섞는다.
        FadeLayer(_jumpArmLayer, active ? jumpArmWeight : 0f, outSeconds);

        UpdateJumpHold(active && !grounded);
    }

    /// <summary>점프 클립을 '도약 → 체공 자세에서 정지 → 착지 직전에 나머지 재생' 으로 나눠 돌린다.
    ///
    /// 클립을 그냥 끝까지 틀면 다리를 펴고 내리는 착지 동작이 아직 공중일 때 다 끝나 버려서,
    /// 남은 체공 내내 착지 자세로 떠 있게 된다. 반대로 마지막 프레임에서 멈춰 두면 착지 순간에
    /// 그 자세로 툭 닿는다. 그래서 가운데(다리를 접은 자세)에서 멈췄다가, 착지에 필요한 만큼만
    /// 시간이 남았을 때 나머지를 돌린다 — 발이 닿는 순간 착지 동작이 딱 끝난다.
    ///
    /// 재생 속도는 JumpSpeed 파라미터로 준다(JumpLayerBuilder 가 Jump 상태에 연결해 둔다).
    /// 두 레이어(다리·팔)의 Jump 가 같은 파라미터를 보므로 항상 같은 프레임에서 같이 멈춘다.
    /// ★파라미터가 없으면(도구 미적용) 아무 일도 하지 않는다 — 클립이 그냥 끝까지 재생된다.</summary>
    private void UpdateJumpHold(bool airborne)
    {
        if (!_hasJumpSpeed) return;

        // 공중이 아니면(착지했거나 점프 중이 아니면) 무조건 정상 재생으로 돌려놓는다.
        // 안 그러면 멈춘 채로 굳어서 다음 점프가 그 자세에서 시작한다.
        if (!airborne) { ResumeJumpClip(); return; }

        var st = _anim.GetCurrentAnimatorStateInfo(_jumpLayer);
        if (!st.IsName(JumpStateName)) { ResumeJumpClip(); return; }

        float t = st.normalizedTime;
        if (t >= 1f) { ResumeJumpClip(); return; }

        // 아직 멈출 지점에 못 왔으면 그냥 재생한다(도약 구간).
        //   ★'멈춰 있는 중이면 건너뛴다' 같은 예외를 절대 두지 말 것. 재생 위치를 앞뒤로 오가게
        //     하던 시절엔 그 예외가 필요했지만, 지금은 흔들림이 뼈에만 얹히므로 t 는 앞으로만 간다.
        //     예외를 두면 이렇게 깨진다 — 이어서 점프하면 멈춤 상태(JumpSpeed=0)가 남아 있는 채로
        //     클립이 0 프레임부터 다시 시작하고, 이 검사를 건너뛰어 0 프레임에 그대로 얼어붙는다.
        //     결과: 점프는 하는데 애니메이션은 선 자세 그대로. (경사로에서 자주 재현됐다)
        if (t < jumpHoldAt) { ResumeJumpClip(); return; }

        // 여기서부터가 '멈출 수 있는 구간'. 남은 재생 시간과 착지까지 남은 시간을 견준다.
        //   ★clip.length 를 쓴다. stateInfo.length 는 재생 속도가 반영된 값이라, 멈춰 있는 동안
        //     무한대가 되어 계산이 통째로 무너진다.
        float clipLen = JumpClipLength();
        if (clipLen <= 0f) { ResumeJumpClip(); return; }

        float remain = (1f - t) * clipLen;                       // 착지 동작에 필요한 시간
        float toLand = _player != null && _player.Movement != null
                     ? _player.Movement.TimeToLand : 0f;         // 땅에 닿기까지 남은 시간

        // 착지 구간 — 남은 클립을 '남은 시간에 딱 맞도록' 속도를 맞춰 재생한다.
        //
        // ★그냥 1 배속으로 틀면 안 된다. 이 점프 클립은 0.6 초쯤인데 평범한 점프의 체공도
        //   0.7 초 남짓이고, 오르막에서는 지면이 마중 나와 그보다 더 짧다. 그러면 멈춰 있을
        //   틈이 아예 없어서 클립이 늘 제 길이대로 다 재생된다 — 높이가 달라도 똑같아 보이고,
        //   착지한 뒤에도 클립이 끝날 때까지 이동 자세로 못 돌아간다.
        //   남은 시간에 맞춰 감으면 낮은 점프는 빠르게, 높은 점프는 느긋하게 마무리된다.
        if (toLand <= remain)
        {
            float rate = remain / Mathf.Max(toLand, 0.01f);
            _holding = false;
            _anim.SetFloat(JumpSpeedHash, Mathf.Clamp(rate, 1f, JumpLandMaxRate));
            return;
        }

        // 체공 자세로 정지. 눈에 보이는 흔들림은 LateUpdate 가 뼈에 직접 준다(ApplyHoldSway).
        _holding = true;
        _anim.SetFloat(JumpSpeedHash, 0f);
    }

    /// <summary>점프 클립을 정상 재생으로 되돌린다(멈춤 상태 해제).</summary>
    private void ResumeJumpClip()
    {
        _holding = false;
        _anim.SetFloat(JumpSpeedHash, 1f);
    }


    /// <summary>지금 Jump 레이어가 물고 있는 클립의 길이(초). 못 찾으면 0.</summary>
    private float JumpClipLength()
    {
        if (_jumpClipLength > 0f) return _jumpClipLength;

        var infos = _anim.GetCurrentAnimatorClipInfo(_jumpLayer);
        if (infos != null && infos.Length > 0 && infos[0].clip != null)
            _jumpClipLength = infos[0].clip.length;
        return _jumpClipLength;
    }

    private void FadeLayer(int layer, float target, float outSeconds)
    {
        if (layer < 0) return;
        float w = _anim.GetLayerWeight(layer);
        if (Mathf.Approximately(w, target)) return;

        // 켜질 때와 꺼질 때 시간을 따로 쓴다 — 들어갈 땐 빨라야 도약이 안 묻히고,
        // 나올 땐 (아직 공중이라면) 느려야 다리가 안 끊긴다.
        float seconds = Mathf.Max(0.01f, target > w ? jumpBlendIn : outSeconds);
        _anim.SetLayerWeight(layer, Mathf.MoveTowards(w, target, Time.deltaTime / seconds));
    }

    /// <summary>점프 레이어를 즉시 내린다(사망/부활 등 상태를 통째로 되돌릴 때).</summary>
    private void ClearJumpLayers()
    {
        _jumpGrace = 0f;
        _jumpHeld = false;
        _leftGround = false;
        _holding = false;
        _jumpLayerActive = false;   // 안 지우면 다음 프레임까지 상체가 Idle 로 눌려 있는다
        if (_hasJumpSpeed) _anim.SetFloat(JumpSpeedHash, 1f);   // 멈춘 채로 굳지 않게
        if (_jumpLayer >= 0)    _anim.SetLayerWeight(_jumpLayer, 0f);
        if (_jumpArmLayer >= 0) _anim.SetLayerWeight(_jumpArmLayer, 0f);
    }

    void UpdateMovement()
    {
        // 피격 상태가 끝난 직후 감지 → 회복 타이머 시작
        bool nowHurt = _player.Stat.IsHurt;
        if (_prevIsHurt && !nowHurt)
        {
            _hurtRecoveryTimer = HURT_RECOVERY_ANIM;
            // 피격 종료 순간 이동 입력이 있으면 Blend Tree 즉시 강제 전환 (끌림 방지)
            if (_player.Input.MoveInput.magnitude > 0.1f)
                _anim.Play("Blend Tree", 0, 0f);
        }
        _prevIsHurt = nowHurt;

        if (_hurtRecoveryTimer > 0f)
            _hurtRecoveryTimer -= Time.deltaTime;

        // damping = 0 적용 조건:
        // 1) 공격·스킬 잠금 해제 직후 (IsPostLockTransition)
        // 2) 피격 중 (IsHurt) — 속도 0 즉시 반영
        // 3) 피격 회복 직후 0.15s — 이동 시작 시 애니메이션 즉시 전환 (끌림 방지)
        bool snapSync = _player.Movement.IsPostLockTransition
                     || _player.Stat.IsHurt
                     || _hurtRecoveryTimer > 0f;

        // ★점프 중에는 상체를 Idle 로 둔다.
        //   하체는 점프 레이어가 가져가지만 상체는 Base Layer 가 그대로 재생하는데,
        //   달리기 모션은 상체를 앞으로 숙이고 좌우로 흔들어서 제자리 점프 다리와 따로 논다
        //   (가만히 뛸 땐 멀쩡한데 달리다 뛰면 이상해 보이던 원인).
        //   ★상태를 갈아타지(Play) 않고 이동 파라미터만 0 으로 민다 — 상태를 바꾸면 착지할 때
        //     이동 애니메이션이 처음부터 다시 시작해 발이 튄다. 아래 damp 가 전환을 부드럽게 한다.
        float speed = _jumpLayerActive ? 0f : _player.Movement.CurrentSpeed;

        float damp = snapSync ? 0f : 0.15f;
        _anim.SetFloat(NormalHash, speed, damp, Time.deltaTime);
    }

    public void PlayAttack(int comboIndex)
    {
        _anim.ResetTrigger(Attack1Hash);
        _anim.ResetTrigger(Attack2Hash);
        _anim.ResetTrigger(Attack3Hash);

        switch (comboIndex)
        {
            case 0: _anim.SetTrigger(Attack1Hash); break;
            case 1: _anim.SetTrigger(Attack2Hash); break;
            case 2: _anim.SetTrigger(Attack3Hash); break;
        }

        // 기본 공격 스윙 사운드
        _player.Audio?.PlayAttackSwing(comboIndex);
    }

    public void PlaySkill(int skillIndex)
    {
        switch (skillIndex)
        {
            case 0: _anim.SetTrigger(Skill1Hash); break;
            case 1: _anim.SetTrigger(Skill2Hash); break;
            case 2: _anim.SetTrigger(Skill3Hash); break;
        }
    }

    public void PlayDash(Vector3 dashDir)
    {
        Vector3 localDir = transform.InverseTransformDirection(dashDir);
        float forward = localDir.z;
        float right = localDir.x;

        if (Mathf.Abs(forward) >= Mathf.Abs(right))
        {
            if (forward >= 0) _anim.SetTrigger(DashFHash);
            else _anim.SetTrigger(DashBHash);
        }
        else
        {
            if (right >= 0) _anim.SetTrigger(DashRHash);
            else _anim.SetTrigger(DashLHash);
        }
    }

    public void PlayJump()
    {
        // 하체 레이어를 붙잡는다. 가중치는 UpdateJumpLayer 가 Jump Blend In 시간에 걸쳐 올린다.
        //   ★즉시 1 로 박지 않는다. 그 프레임에는 레이어 상태가 아직 Empty(모션 없음)라
        //     다리가 한 번 튀고, 걷기 자세에서 점프 자세로 넘어가는 블렌딩도 사라진다.
        if (_jumpLayer >= 0)
        {
            _jumpGrace = JumpStateGrace;
            _jumpHeld = true;
            _leftGround = false;
        }

        // 앞선 점프가 체공 자세에서 멈춘 채로 끝났을 수 있다 — 새 점프는 정상 재생으로 시작한다.
        _holding = false;
        if (_hasJumpSpeed) _anim.SetFloat(JumpSpeedHash, 1f);

        _anim.SetTrigger(JumpHash);
        _player.Audio?.PlayJump();
    }

    /// <summary>개발용(비행 등): 애니메이션 정지/재개. speed 토글이라 이동 파라미터가 바뀌어도 현재 포즈가 고정된다.</summary>
    public void SetFrozen(bool frozen)
    {
        if (_anim != null) _anim.speed = frozen ? 0f : 1f;
    }

    public void ResetToIdle()
    {
        // GS_Die는 Action Layer(레이어 1)에 있음 → 레이어 1도 Empty로 리셋해야 함
        // 레이어 1이 Override(weight=1)이라 리셋 안 하면 죽는 애니메이션이 계속 재생됨
        _anim.ResetTrigger(DieHash);
        _anim.ResetTrigger(JumpHash);     // 죽는 순간 눌린 점프가 부활 직후 튀어나오지 않게
        _anim.Play("Blend Tree", 0, 0f);  // Base Layer → Idle/Walk/Run
        _anim.Play("Empty",      1, 0f);  // Action Layer → Empty (GS_Die 종료)
        ClearJumpLayers();                // 공중에서 죽었으면 점프 자세가 남아 있다 → 즉시 해제
    }

    /// <summary>
    /// 대시·공격·스킬 등 액션 애니메이션이 재생 중이면 true.
    /// 전환(Transition) 중이거나 Blend Tree 상태가 아닐 때 true를 반환.
    /// DashRoutine / SkillFlow / ComboAttackBase 에서 이동 잠금 해제 타이밍 판단에 사용.
    /// </summary>
    public bool IsInActionAnim()
    {
        if (_anim == null) return false;
        if (_anim.IsInTransition(0)) return true;   // 전환 중 = 아직 액션 중
        return !_anim.GetCurrentAnimatorStateInfo(0).IsName("Blend Tree");
    }

    /// <summary>
    /// 액션(스킬/공격) 동작이 끝났을 때 이동(Blend Tree)으로 즉시 복귀.
    /// 스킬 클립이 딜 모션보다 훨씬 길어(후속 마무리 폼 포함), 딜 끝난 뒤 그 잔여 클립이
    /// Blend Tree로 천천히 블렌딩되며 미끄러지듯 보이던 문제 해결 — 즉시 컷으로 잘라낸다.
    /// </summary>
    public void ReturnToLocomotion()
    {
        if (_anim == null) return;
        _anim.ResetTrigger(Skill1Hash);
        _anim.ResetTrigger(Skill2Hash);
        _anim.ResetTrigger(Skill3Hash);
        _anim.ResetTrigger(Attack1Hash);
        _anim.ResetTrigger(Attack2Hash);
        _anim.ResetTrigger(Attack3Hash);

        // 공격/스킬 클립은 Action Layer(레이어 1)에서 재생된다.
        // 레이어 0 만 Play 하면 스킬 모션이 안 끊기므로 두 레이어 모두 처리:
        //   레이어 0(Base) -> Blend Tree(이동),  레이어 1(Action) -> Empty(동작 종료)
        _anim.Play("Blend Tree", 0, 0f);
        _anim.Play("Empty",      1, 0f);
    }

    /// <summary>
    /// 대시 물리 이동이 끝났을 때 이동(Blend Tree)으로 "부드럽게" 복귀.
    /// 스킬과 달리 대시는 이동 흐름이라 즉시 컷하면 뚝 끊겨 부자연스럽다.
    /// 대시 클립(QUICK SHIFT)은 Action Layer(1)에서 재생되므로, 그 레이어를 짧게
    /// CrossFade 로 빼서 "자세 낮춘 끝부분"이 길게 미끄러지는 것만 잘라낸다.
    /// </summary>
    public void EndDash(float fade = 0.2f)
    {
        if (_anim == null) return;
        _anim.ResetTrigger(DashFHash);
        _anim.ResetTrigger(DashBHash);
        _anim.ResetTrigger(DashRHash);
        _anim.ResetTrigger(DashLHash);
        // 레이어 1(Action)을 Empty 로 짧게 블렌딩 → 대시 끝자세에서 이동으로 매끄럽게 전환
        _anim.CrossFade("Empty", fade, 1);
    }

    public void PlayHit(bool isLeft) => _anim.SetTrigger(isLeft ? HitLHash : HitRHash);

    /// <summary>귀환석 채널링 모션 시작 — Action Layer(1)의 "Channel" 상태(GS_CrouchIdle, 루프)를 강제 재생.
    /// 진행 중이던 액션/대기 트리거를 먼저 제거해, 다른 모션 도중 눌러도 채널 상태가 튕기지 않게 한다.</summary>
    public void PlayChannel()
    {
        if (_anim == null) return;
        ResetActionTriggers();
        _anim.SetLayerWeight(1, 1f);          // 크라우치가 전신으로 보이게 Action 레이어 강제
        _anim.Play("Channel", 1, 0f);         // 트랜지션에 밀리지 않게 강제 진입(PlayDie와 동일 방식)
    }

    // 채널 진입/사망이 다른 액션 트리거에 밀려 안 나오는 것 방지 — 대기 중 트리거 일괄 제거.
    private void ResetActionTriggers()
    {
        _anim.ResetTrigger(Attack1Hash); _anim.ResetTrigger(Attack2Hash); _anim.ResetTrigger(Attack3Hash);
        _anim.ResetTrigger(Skill1Hash);  _anim.ResetTrigger(Skill2Hash);  _anim.ResetTrigger(Skill3Hash);
        _anim.ResetTrigger(DashFHash);   _anim.ResetTrigger(DashBHash);
        _anim.ResetTrigger(DashRHash);   _anim.ResetTrigger(DashLHash);
        _anim.ResetTrigger(HitLHash);    _anim.ResetTrigger(HitRHash);
        _anim.ResetTrigger(JumpHash);
    }

    /// <summary>채널링 종료 — Action Layer를 Empty 로 빼 이동/대기로 복귀.</summary>
    public void StopChannel(float fade = 0.15f)
    {
        if (_anim == null) return;
        _anim.CrossFade("Empty", fade, 1);
    }

    public void PlayDie()
    {
        // 죽는 애니가 확실히, 전신으로 나오게:
        //  - Action 레이어(1) weight=1 강제 (점프 등 베이스 레이어 포즈가 비쳐 스완다이브처럼 보이던 것 방지)
        //  - 트리거 + Play 둘 다 (전환 누락 시에도 강제 재생)
        _anim.ResetTrigger(DieHash);
        _anim.SetLayerWeight(1, 1f);
        _anim.SetTrigger(DieHash);
        _anim.Play("GS_Die", 1, 0f);
        _player.Audio?.PlayDie();
    }
}
