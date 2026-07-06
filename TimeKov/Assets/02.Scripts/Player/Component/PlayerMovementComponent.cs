using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerMovementComponent : MonoBehaviour
{
    [Header("Speed")]
    public float MoveSpeed = 5f;      // 기본 이동 속도
    public float SprintSpeed = 8f;      // 달리기 속도

    [Header("Jump")]
    public float JumpHeight = 2f;   // 점프 높이
    public float Gravity = -20f; // 중력 값
    public float FallMultiplier = 2.5f; // 하강 시 중력 배수
    public float JumpBufferTime = 0.1f; // 점프 입력 유예 시간

    [Header("Movement")]
    [Tooltip("몸이 이동 방향으로 도는 속도. 클수록 빠릿하게 돌고, 작을수록 크게 휘어 돈다")]
    public float RotSpeed = 12f;        // 회전 속도

    [Header("Ground Check")]
    public float GroundCheckRadius = 0.25f; // 지면 감지 구체 반경
    public float GroundCheckOffset = 0.05f; // 지면 감지 오프셋
    public LayerMask GroundMask;            // 지면 레이어 마스크

    [Header("Slope")]
    [Tooltip("이 각도(도) 초과 경사면은 올라갈 수 없고 미끄러짐")]
    public float MaxSlopeAngle   = 45f;   // 최대 등반 가능 경사각
    public float SlopeSlideSpeed = 4f;    // 가파른 경사에서 미끄러지는 속도

    [Header("Feel (가감속·공중·코요테)")]
    [Tooltip("지상 가속(목표 속도까지). 클수록 즉각적(빠릿), 작을수록 관성 크게(레이싱처럼 굼뜸). 방향전환이 굼뜨면 ↑")]
    public float MoveAccel = 120f;
    [Tooltip("지상 감속(정지까지). 클수록 딱 멈춤, 작을수록 미끄러지듯 정지. 방향전환 텀에도 영향")]
    public float MoveDecel = 130f;
    [Tooltip("공중 수평 제어 가속(작을수록 관성 유지)")]
    public float AirAccel  = 40f;
    [Tooltip("낭떠러지 이탈 후 이 시간 동안 점프 허용(코요테 타임)")]
    public float CoyoteTime = 0.12f;

    [Header("Ground Snap (언덕/내리막 밀착)")]
    [Tooltip("접지 상태에서 발밑 이 거리 내에 지면이 있으면 붙여, 언덕 꼭대기/내리막에서 뜨지 않게 한다. 이보다 큰 낙차는 진짜 낙하로 처리")]
    public float GroundSnapDistance = 0.5f;
    [Tooltip("접지/경사 접촉 중(점프 아님)에 허용되는 최대 상승속도. 경사 대쉬가 램프처럼 튕겨 슈퍼점프 되는 것을 막는 상한. 경사 오르기는 이 값 이하로 유지")]
    public float MaxGroundedRiseSpeed = 6f;
    [Tooltip("접지 시 표면에 수직으로 눌러 붙이는 세기(velocity 기반, 물리가 해결 → 관통 없음). 뜸 방지용. 경사 미끄러짐엔 영향 없음(법선 방향이라 접선=0)")]
    public float GroundStickForce = 2f;

    [Header("Step (턱·계단 자동 오르기)")]
    [Tooltip("자동으로 올라설 수 있는 최대 턱/계단 높이. 이보다 높으면 벽으로 취급해 막힘")]
    public float MaxStepHeight = 0.3f;

    private Player _player;
    private Rigidbody _rb;
    private CapsuleCollider _capsule;
    private ThirdPersonCamera _camera;

    private Vector3 _moveDir;
    private bool _isGrounded;
    private bool _wasGrounded;
    private Vector3 _groundNormal         = Vector3.up; // 현재 지면 법선 (SphereCast 원시값)
    private Vector3 _smoothedGroundNormal = Vector3.up; // 스무딩된 법선 (노이즈 제거)
    private bool    _onSteepSlope;                       // MaxSlopeAngle 초과 경사면
    private float   _steepSlopeTimer;                    // 연속 급경사 감지 시간 (오탐 방지 hysteresis)
    private float   _steepExitTimer;                     // 완만한 지면에 접지 지속 시간(급경사 해제용)
    private const float STEEP_SLOPE_ENTER_DELAY = 0.08f; // 이 시간 이상 급경사여야 슬라이딩 시작
    private const float STEEP_SLOPE_EXIT_DELAY  = 0.10f; // 완만 지면에 이 시간 이상 접지해야 급경사 해제(점프 우회 방지)
    private bool    _steepContact;                       // 이번 물리 스텝 급경사 표면과 접촉(접지/공중 무관, 콜리전 컨택트 기반)
    private Vector3 _steepContactNormal = Vector3.up;    // 접촉한 급경사면 법선(가장 가파른 것)
    private bool    _stepping;                            // 턱 오르기 진행 중(대각선 궤적 이동)
    private Vector3 _stepStart;                           // 턱 오르기 시작 위치
    private Vector3 _stepTarget;                          // 턱 오르기 목표 위치(위+앞)
    private float   _stepTimer;                           // 턱 오르기 진행 시간
    private const float STEP_UP_TIME = 0.09f;             // 턱 하나 오르는 데 걸리는 시간(짧게 → 매끄럽게)
    private bool _canJump = true;
    private float _jumpBufferCounter;
    private bool _jumpRequested;
    private float _currentSpeed;
    private bool _isJumping;

    private float   _coyoteCounter;   // 낭떠러지 이탈 후 남은 점프 허용 시간
    private Vector3 _planarVel;        // 의도한 수평 속도(가감속). 리지드바디 되읽기와 분리 → 경사 press와 안 엉킴
    private bool    _dashDriving;      // 대쉬 물리 구동 중(경사 투영 + 지면 스냅을 이동 컴포넌트가 담당)
    private Vector3 _dashDir;          // 대쉬 수평 방향(정규화)
    private float   _dashSpeed;        // 대쉬 속도
    private bool _movementLocked;
    private float _postUnlockPhysTimer;   // 물리 dead zone: 이 시간 동안 velocity=0 (공격 후 즉시 이동 차단)
    private float _postUnlockAnimTimer;   // 애니메이션 sync: IsPostLockTransition 유지 (damping=0)
    private const float POST_UNLOCK_PHYS = 0.15f; // 물리 dead zone 길이
    private const float POST_UNLOCK_ANIM = 0.20f; // anim sync 길이 (dead zone + 1프레임 여유)

    private bool _deadHandled;         // 사망 안착 트리거 1회 가드(OnDead 이벤트/FixedUpdate 어느 쪽이 먼저 불러도 한 번만)
    private Vector3 _deathPos;          // 죽은 자리(안착 위치). 사망 중 매 프레임 이 자리로 강제 고정 = 경사 미끄러짐/보간 밀림 방지

    // 사망 처리: 죽는 즉시 발밑 지면으로 스냅해 '박히듯' 안착시킨 뒤 물리 고정(BeginDeathSettle). 공중사망도 낙하 없이 그 자리 바로 아래 바닥에서 죽는다.

    // Slash
    private bool _isSlashing;
    private float _slashTimer;
    private float _slashForce;

    public float CurrentSpeed => _currentSpeed;  // 현재 이동 속도
    public bool IsGrounded => _isGrounded;     // 지면 여부
    public bool IsJumping => _isJumping;      // 점프 중 여부
    public bool IsSlashing => _isSlashing;     // 슬래시 중 여부
    public bool IsSprinting { get; private set; } // 스프린트 중 여부 (스태미나 재생 판단용)
    /// <summary>
    /// dead zone 또는 그 직후 애니메이션 sync 중: true
    /// PlayerAnimatorComponent가 damping=0으로 애니메이션을 물리에 즉시 동기화
    /// </summary>
    public bool IsPostLockTransition => _postUnlockAnimTimer > 0f;

    void Awake()
    {
        _player = GetComponent<Player>();
        _rb = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>();
        _camera = FindAnyObjectByType<ThirdPersonCamera>();

        if (_camera == null)
            Debug.LogError("[PlayerMovementComponent] ThirdPersonCamera를 찾을 수 없습니다. 씬에 카메라가 존재하는지 확인하세요.", this);

        _rb.freezeRotation = true;
        _rb.useGravity = false;
    }

    void OnEnable()
    {
        if (_player?.Stat != null)
        {
            _player.Stat.OnDead += BeginDeathSettle;
        }
    }

    void OnDisable()
    {
        if (_player?.Stat != null)
        {
            _player.Stat.OnDead -= BeginDeathSettle;
        }
    }

    /// <summary>
    /// 사망 처리: 죽는 즉시 발밑 지면으로 스냅해 '박히듯' 안착시킨 뒤 물리 고정한다.
    /// 공중사망(낙하 중 사망)이어도 떨어지며 사라지지 않고, 그 자리 바로 아래 바닥에서 죽는다.
    /// OnDead 이벤트 + FixedUpdate(IsDead) 양쪽에서 호출되며 _deadHandled 가드로 1회만 실행 = 이벤트 누락에 의존 안 함.
    /// - 지면 탐색은 GroundMask 1순위 + 아무 표면 폴백(SnapDownToGround) = 새 터레인이 GroundMask에 없어도 동작.
    /// - yaw만 유지(가로로 안 눕혀 맵 뚫림 방지). 지면을 전혀 못 찾으면(완전 허공) 현재 위치에서 고정.
    /// </summary>
    private void BeginDeathSettle()
    {
        if (_deadHandled) return;   // 이벤트/FixedUpdate 어느 쪽이 먼저 불러도 1회만
        _deadHandled = true;

        // 죽는 순간 즉시: 관성 제거 -> 발밑 지면으로 스냅 -> 똑바로 세움 -> kinematic 고정.
        // (예전의 '중력으로 떨어뜨린 뒤 안착' 방식을 없애 공중사망이 낙하 없이 바로 바닥에서 죽게 함)
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        SnapDownToGround();   // 현재 위치 바로 아래 지면으로 즉시 안착(폴백 포함 = 못 찾아도 안전)
        UprightYaw();
        _rb.isKinematic = true;
        _deathPos = transform.position;   // 죽은 자리 확정 → 사망 중 매 프레임 이 자리로 고정(경사 미끄러짐 방지)

        // 죽는 시점에 보고 있던 카메라 시점 그대로 고정 + 플레이어 카메라 조작 차단.
        ThirdPersonCamera.Instance?.LockForDeath();
    }

    // 똑바로 세우기(yaw만). 경사 법선 정렬 안 함 = 가로로 눕혀 맵 뚫리던 것 방지.
    private void UprightYaw()
    {
        Vector3 flatFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (flatFwd.sqrMagnitude < 1e-4f) flatFwd = Vector3.forward;
        transform.rotation = Quaternion.LookRotation(flatFwd.normalized, Vector3.up);
    }

    // 발밑으로 레이캐스트해서 캡슐 바닥이 지면에 닿게 Y 보정. 지면 못 찾으면 현재 위치 유지.
    private void SnapDownToGround()
    {
        Vector3 origin = transform.position + Vector3.up * Mathf.Max(2f, _capsule.height);
        float maxDist = origin.y - transform.position.y + 80f;

        // 1순위: 지정한 지면 레이어. 2순위(폴백): 플레이어 외 아무 표면.
        // GroundMask에 새 터레인 등 일부 레이어가 누락되면 1순위가 빗나가 공중에 떠버리므로(점프사망 부유 원인),
        // 마스크 설정에 의존하지 않게 폴백을 둔다.
        bool hit = Physics.Raycast(origin, Vector3.down, out RaycastHit groundHit, maxDist, GroundMask, QueryTriggerInteraction.Ignore);
        if (!hit)
        {
            int notPlayer = ~(1 << gameObject.layer);   // 자기 콜라이더 제외(Ignore Raycast 레이어는 어차피 제외됨)
            hit = Physics.Raycast(origin, Vector3.down, out groundHit, maxDist, notPlayer, QueryTriggerInteraction.Ignore);
        }
        if (!hit) return;   // 그래도 못 찾으면(완전 허공) 현재 위치 유지

        float capsuleBottomLocalY = _capsule.center.y - _capsule.height * 0.5f;
        float worldBottomY = transform.position.y + capsuleBottomLocalY * transform.lossyScale.y;
        float dy = groundHit.point.y - worldBottomY;
        Vector3 p = transform.position + Vector3.up * dy;
        _rb.position = p;
        transform.position = p;
    }

    /// <summary>부활 시 Rigidbody 복구 (PlayerStatComponent.Respawn에서 호출됨)</summary>
    public void UnfreezeOnRespawn()
    {
        _deadHandled = false;
        _rb.isKinematic = false;
        UprightYaw();

        // 사망 시점 시점 잠금 해제 → 마우스 카메라 제어 복구.
        ThirdPersonCamera.Instance?.UnlockAfterRespawn();
    }

    void Update()
    {
        // 공격·스킬 잠금 / 스킬 실행 중 / 피격 경직 / 사망 중에는 이동 방향 입력 무시
        // → 잠금 해제 직후 이전에 눌렸던 WASD 때문에 즉시 미끄러지는 현상 방지.
        // _movementLocked 와 IsExecuting 의 해제 순서가 1프레임 어긋나 WASD(축 입력)가
        // 새던 문제 — IsExecuting 을 직접 봐서 스킬이 완전히 끝날 때까지 이동 방향 0 유지.
        bool inputBlocked = _movementLocked
                         || (_player.Skill != null && _player.Skill.IsExecuting)
                         || _player.Stat.IsHurt
                         || _player.Stat.IsDead;

        _moveDir = inputBlocked
            ? Vector3.zero
            : GetMoveDirection(_player.Input.MoveInput);

        GroundCheck();
        HandleJumpInput();
    }

    void FixedUpdate()
    {
        // 사망 시 모든 물리 이동 정지 (경사면 미끄러짐 방지).
        // BeginDeathSettle이 죽는 즉시 발밑 지면으로 스냅 + kinematic 고정한다. 그 뒤엔
        // 죽은 자리(_deathPos)로 매 프레임 강제 고정 = 경사 잔여 슬라이드/Interpolate 보간 밀림까지 원천 차단.
        if (_player.Stat.IsDead)
        {
            _currentSpeed = 0f;
            if (!_deadHandled)
                BeginDeathSettle();   // 이벤트(OnDead) 누락 대비 - 죽는 순간 1회 즉시 안착+고정
            else
            {
                // 이미 사망 고정됨: 어떤 이유로든 위치가 밀리면 죽은 자리로 다시 못박음.
                _rb.position = _deathPos;
                transform.position = _deathPos;   // 보간(Interpolate) 시각 밀림까지 즉시 차단
            }
            return;
        }
        _deadHandled = false;   // 살아있음 = 다음 사망 위해 재무장(부활/리셋 포함)

        // 대쉬 중: 물리를 대쉬 전용으로 구동(경사 투영) + 표면을 막 벗어난 순간(크레스트) 스냅으로
        // 지면에 붙잡음 → 경사 대쉬가 꼭대기에서 공중에 뜨는 것 방지. (접지 중엔 스냅 안 함 = 관통 없음)
        if (_dashDriving)
        {
            HandleDashMove();
            HandleGroundSnap();
            ClampSteepClimb();      // 대쉬도 급경사 표면 등반 차단(접지/공중 무관)
            _steepContact = false;  // 소비 후 리셋 → 다음 스텝 컨택트가 다시 설정(자기정리)
            return;
        }

        HandleJump();
        HandleSlash();
        HandleMove();
        HandleGravity();
        HandleGroundSnap();     // 언덕 꼭대기/내리막에서 뜨지 않게 지면 밀착(중력 뒤)
        HandleStepUp();         // 낮은 턱·계단 자동 오르기(캡슐이 걸려 멈추지 않게)
        HandleRotation();
        HandleSlopeStabilize(); // 경사면 슬라이딩 방지 (마지막 실행)
        ClampGroundedRise();    // 접지/경사 접촉 중 슈퍼점프(램프 튕김) 상한
        ClampSteepClimb();      // 급경사 표면 등반 차단(접지/공중 무관, 점프로 뜬 채 밀어 올라가는 것 방지)
        _steepContact = false;  // 소비 후 리셋 → 다음 스텝 컨택트가 다시 설정(자기정리)
    }

    // 현재 밟고 있는 지면 경사각(도) / 급경사 여부 — 외부·디버그 확인용
    public float CurrentSlopeAngle => Vector3.Angle(Vector3.up, _smoothedGroundNormal);
    public bool  OnSteepSlope      => _onSteepSlope;

    void GroundCheck()
    {
        _wasGrounded = _isGrounded;

        float halfHeight = _capsule.height / 2f;
        Vector3 origin = transform.position
                           + Vector3.down * (halfHeight - _capsule.radius + GroundCheckOffset);

        // QueryTriggerInteraction.Ignore: Trigger 콜라이더를 지면으로 인식하지 않음
        // (Ground 레이어에 대형 Trigger가 있어도 무한점프 방지)
        bool checkSphere = Physics.CheckSphere(origin, GroundCheckRadius, GroundMask,
                                               QueryTriggerInteraction.Ignore);

        // SphereCast 로 지면 법선 획득 → 경사각 계산.
        // 수직 캡슐은 경사에서 바닥 중심 구(CheckSphere)가 지면을 놓치기 쉬우므로,
        // 아래로 쓸어내리는 SphereCast 히트도 접지로 인정한다(경사 접지 판정 보강).
        Vector3 castStart = transform.position + Vector3.up * 0.1f;
        float castDist    = halfHeight + GroundCheckOffset + 0.15f;

        bool sphereCastHit = Physics.SphereCast(castStart, GroundCheckRadius * 0.9f,
                                                Vector3.down, out RaycastHit hit, castDist, GroundMask,
                                                QueryTriggerInteraction.Ignore);
        _groundNormal = sphereCastHit ? hit.normal : Vector3.up;

        _isGrounded = checkSphere || sphereCastHit;
        // 단, 점프로 상승 중엔 접지로 오판하지 않게(중력이 걸려야 함) 강제 해제
        if (_isJumping && _rb.linearVelocity.y > 0.1f)
            _isGrounded = false;

        // SphereCast 원시 법선을 스무딩: 폴리곤 경계·돌출부에서 1~2프레임 노이즈 제거
        // Slerp factor 15 × deltaTime ≈ 0.25/frame → 약 4프레임에 걸쳐 부드럽게 수렴
        _smoothedGroundNormal = Vector3.Slerp(
            _smoothedGroundNormal, _groundNormal, Time.deltaTime * 15f);

        // 급경사 판정은 '원시 법선'으로 즉시 한다(스무딩 지연 X).
        // 스무딩 법선을 쓰면 점프 직후(공중=법선 위) 착지 시 수렴 지연 동안 급경사가 안 잡혀
        // 점프로 급경사를 우회해 그냥 올라가지던 버그가 생김.
        float steepAngle = Vector3.Angle(Vector3.up, _groundNormal);
        bool rawSteep = _isGrounded && steepAngle > MaxSlopeAngle;

        if (rawSteep)
        {
            // 진입: 급경사가 ENTER_DELAY 이상 지속되면 슬라이딩(폴리곤 경계 1~2프레임 오탐 방지)
            _steepSlopeTimer += Time.deltaTime;
            _steepExitTimer   = 0f;
            if (_steepSlopeTimer >= STEEP_SLOPE_ENTER_DELAY) _onSteepSlope = true;
        }
        else
        {
            _steepSlopeTimer = 0f;
            // 해제: '완만한 지면에 접지'가 EXIT_DELAY 이상 지속됐을 때만 급경사 상태를 푼다.
            // 점프/바운스로 잠깐 공중이 돼도(=!_isGrounded) 해제하지 않음 → 점프로 급경사 우회 불가.
            if (_isGrounded)
            {
                _steepExitTimer += Time.deltaTime;
                if (_steepExitTimer >= STEEP_SLOPE_EXIT_DELAY) _onSteepSlope = false;
            }
        }

        // UI 열려있을 때 점프 홀드 상태 무시 (PlayerInputComponent를 통해 읽음)
        bool jumpHeld = _player.Input.JumpHeld;

        if (!_wasGrounded && _isGrounded)
        {
            // ★ 핵심 가드: 상승 중(velocity.y > 0)이면 착지 처리 무시
            // 공중 콜라이더 오인식 or GroundMask 레이어 설정 오류 시 무한점프 방지
            if (_rb.linearVelocity.y > 0.5f)
            {
                // 착지 처리 스킵 → _canJump 리셋 안 함
            }
            else
            {
                _isJumping = false;
                if (!jumpHeld) _canJump = true;
            }
        }
        else if (_wasGrounded && !_isGrounded)
        {
            _canJump = false;
        }
        else
        {
            if (_isGrounded && _player.Input.JumpUp)
                _canJump = true;
        }

        // 코요테 타임: 접지면 리필, 이탈하면 감소 → 낭떠러지 살짝 벗어나도 잠깐 점프 허용
        if (_isGrounded) _coyoteCounter = CoyoteTime;
        else             _coyoteCounter = Mathf.Max(0f, _coyoteCounter - Time.deltaTime);
    }

    void HandleJumpInput()
    {
        // UI 열려있으면 점프 입력 무시, 버퍼 초기화 (팀원 추가)
        if (PlayerInputComponent.IsBlocked)
        {
            _jumpBufferCounter = 0f;
            return;
        }

        // 공격·스킬 실행 중 점프 차단 (1단계 추가)
        if (_player.Skill.IsExecuting) return;

        // 대시 중 점프 차단 — 대시 수평속도(15)와 점프 수직속도가 합산되어
        // 앞으로 포물선으로 튕겨 날아가는 버그 방지. (달리기+대시+점프 동시 입력)
        if (_player.Dash != null && _player.Dash.IsDashing) return;

        // Dead 상태 점프 차단 (1단계 추가)
        if (_player.Stat.IsDead) return;

        // 급경사(슬라이딩 중)에선 점프 차단 — 점프로 급경사를 우회해 올라가던 버그 방지.
        if (_onSteepSlope) { _jumpBufferCounter = 0f; return; }

        // 접지 점프 or 코요테 점프(낭떠러지 이탈 직후, 아직 점프 안 한 상태)
        bool groundOk = _isGrounded && _canJump;
        bool coyoteOk = !_isJumping && _coyoteCounter > 0f;

        if (_player.Input.JumpPressed && (groundOk || coyoteOk))
            _jumpBufferCounter = JumpBufferTime;
        else
            _jumpBufferCounter = Mathf.Max(_jumpBufferCounter - Time.deltaTime, 0);

        if ((groundOk || coyoteOk) && _jumpBufferCounter > 0f)
        {
            _jumpRequested = true;
            _jumpBufferCounter = 0f;
            _canJump = false;
            _coyoteCounter = 0f;   // 코요테 1회 소모(공중 재점프 방지)
        }
    }

    void HandleJump()
    {
        if (!_jumpRequested) return;

        Vector3 v = _rb.linearVelocity;
        v.y = Mathf.Sqrt(JumpHeight * -2f * Gravity);
        _rb.linearVelocity = v;

        _jumpRequested = false;
        _isGrounded = false;
        _isJumping = true;

        // 점프 애니메이션 재생
        _player.Anim.PlayJump();
    }

    void HandleSlash()
    {
        if (!_isSlashing) return;

        _rb.linearVelocity = new Vector3(
            transform.forward.x * _slashForce,
            _rb.linearVelocity.y,
            transform.forward.z * _slashForce
        );

        _slashTimer -= Time.fixedDeltaTime;

        if (_slashTimer <= 0)
        {
            _isSlashing = false;
            // LockMovement 해제는 Attack3Skill 코루틴 끝에서 처리
            _rb.linearVelocity = new Vector3(0, _rb.linearVelocity.y, 0);
        }
    }

    void HandleMove()
    {
        // 피격 경직 중: 이동 완전 차단 + velocity 즉시 0
        if (_player.Stat.IsHurt)
        {
            _planarVel = Vector3.zero;
            _currentSpeed = 0f;
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            return;
        }

        // 사망 중: 이동 완전 차단
        if (_player.Stat.IsDead)
        {
            _planarVel = Vector3.zero;
            _currentSpeed = 0f;
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            return;
        }

        // 가파른 경사(걷기 불가 각도): 이동 차단 + 경사면 표면 방향(y 포함)으로 미끄러짐.
        // ★ 이동 잠금(스킬 등) 체크보다 '먼저' 처리한다 — 스킬 중에도 미끄러짐 속도가 항상 SlopeSlideSpeed로
        //   제한되게 하여, 스킬 힘·중력이 경사에 튕겨 말도 안 되게 멀리 미끄러지던 버그를 막는다.
        if (_onSteepSlope)
        {
            _planarVel = Vector3.zero;
            _currentSpeed = 0f;
            if (_isGrounded)
            {
                // 접지 급경사: 표면 아래방향으로 미끄러짐(y 포함)
                Vector3 slideDir    = Vector3.ProjectOnPlane(Vector3.down, _groundNormal).normalized;
                Vector3 targetSlide = slideDir * SlopeSlideSpeed;
                _rb.linearVelocity  = Vector3.Lerp(_rb.linearVelocity, targetSlide, Time.fixedDeltaTime * 12f);
            }
            else
            {
                // 공중 급경사(점프/바운스): 수평 이동만 제거해 위로 못 올라가게, 수직은 중력이 담당 → 도로 떨어짐
                _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            }
            return;
        }

        // 이동 잠금(공격·스킬 등): 이동 입력 무시(위 급경사 미끄러짐 제한은 이미 적용됨).
        // 슬래시(돌진) 중이 아니면 수평 잔류속도를 제거한다 — 스킬 중 루트모션/이전 관성이 남긴 수평 속도가
        // 감쇠 없이 유지돼 경사에서 아래로 계속 미끄러지던 버그 방지. (루트모션 전진은 MovePosition이 담당 → 유지됨)
        // 슬래시는 HandleSlash가 매 프레임 속도를 직접 제어하므로 여기서 지우면 안 됨.
        if (_movementLocked)
        {
            _planarVel = Vector3.zero;
            if (!_isSlashing)
                _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            return;
        }

        // UI 열려있을 때 스프린트 무시 (팀원 추가)
        bool isSprinting = !PlayerInputComponent.IsBlocked
                        && Input.GetKey(KeyCode.LeftShift)
                        && _player.Stat.TryDrainSprintStamina()
                        && _moveDir.magnitude > 0.1f;

        IsSprinting = isSprinting; // 외부(PlayerStatComponent 스태미나 재생)에서 참조

        float targetSpeed = _moveDir.magnitude > 0.1f
                          ? (isSprinting ? SprintSpeed : MoveSpeed)
                          : 0f;

        // 공격 종료 직후 dead zone: 키를 누르고 있어도 이동 완전 차단
        if (_postUnlockPhysTimer > 0f)
        {
            _postUnlockPhysTimer -= Time.fixedDeltaTime;
            _postUnlockAnimTimer  = Mathf.Max(0f, _postUnlockAnimTimer - Time.fixedDeltaTime);
            _planarVel = Vector3.zero;
            _currentSpeed = 0f;
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            return;
        }

        // dead zone 종료 후 anim sync 타이머만 감소 (이동은 즉시 허용)
        if (_postUnlockAnimTimer > 0f)
            _postUnlockAnimTimer -= Time.fixedDeltaTime;

        // 가감속: 의도한 수평 속도(_planarVel)를 목표로 부드럽게 수렴.
        // 리지드바디 되읽기와 분리 → 경사면 처리와 엉키지 않음(얕은 경사 못 오르던 문제 원인 제거).
        Vector3 targetPlanar = _moveDir * targetSpeed;
        float   rate         = _isGrounded ? (targetSpeed > 0.01f ? MoveAccel : MoveDecel) : AirAccel;
        _planarVel = Vector3.MoveTowards(_planarVel, targetPlanar, rate * Time.fixedDeltaTime);
        _currentSpeed = _planarVel.magnitude;

        // 수평 속도만 설정한다. 경사면 정렬(표면 따라 이동)과 접촉 press(뜸/미끄러짐 방지),
        // 공중 중력은 HandleGravity가 담당 → velocity 기반이라 물리가 충돌을 해결(오브젝트 관통·텔레포트 없음).
        _rb.linearVelocity = new Vector3(_planarVel.x, _rb.linearVelocity.y, _planarVel.z);
    }

    void HandleGravity()
    {
        // 접지 급경사: HandleMove의 슬라이드가 y 포함 전담 → 스킵.
        // (공중 급경사는 아래 중력을 적용해 도로 떨어지게 = 점프로 급경사 위에 뜨는 것 방지)
        if (_isGrounded && _onSteepSlope) return;

        // 접지: 표면에 '수직으로' 눌러 밀착 = velocity 기반 → 물리가 해결(관통/텔레포트 없음).
        // (점프 상승 중엔 GroundCheck가 _isGrounded=false로 강제하므로 여기 안 들어옴)
        if (_isGrounded)
        {
            float slopeAngle = Vector3.Angle(Vector3.up, _smoothedGroundNormal);
            if (slopeAngle > 1f)
            {
                Vector3 n     = _smoothedGroundNormal;
                Vector3 horiz = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);

                if (horiz.sqrMagnitude > 0.01f)
                {
                    // 이동 중: 진행 방향(X,Z)은 '그대로' 두고, 경사면을 따라가도록 수직속도만 계산한다.
                    // ProjectOnPlane은 법선이 옆으로 조금만 기울어도 X,Z까지 회전시켜, 울퉁불퉁한 길에서
                    // 혼자 옆으로 밀리던 원인 — 헤딩을 보존하면 요철에서도 원하는 방향으로 곧게 걷는다.
                    float targetVy = 0f;
                    if (Mathf.Abs(n.y) > 1e-3f)
                        targetVy = -(n.x * horiz.x + n.z * horiz.z) / n.y;   // 평면 위 유지: n·v = 0
                    targetVy -= GroundStickForce;                            // 접촉 유지 press

                    // 요철 덜덜거림 방지: 수직속도를 목표로 '부드럽게' 수렴(저역통과)시킨다.
                    // 하드로 덮어쓰면 솔버가 요철을 밀어낸 +y를 즉시 아래로 되받아 상하 진동(덜덜)이 생김 → 흡수.
                    float smoothVy = Mathf.Lerp(_rb.linearVelocity.y, targetVy, 0.4f);
                    _rb.linearVelocity = new Vector3(horiz.x, smoothVy, horiz.z);
                }
                else
                {
                    // 정지: 표면 법선 방향으로만 눌러 접촉 유지(접선 0 → 경사에서도 안 미끄러짐).
                    _rb.linearVelocity = -n * GroundStickForce;
                }
            }
            else
            {
                // 평지: 수평 유지 + 아래로 살짝 눌러 접촉(뜸 방지). 수평엔 영향 없음.
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, -GroundStickForce, _rb.linearVelocity.z);
            }
            return;
        }

        // 공중: 중력(하강 시 배수)
        float multiplier = _rb.linearVelocity.y < 0 ? FallMultiplier : 1f;
        _rb.AddForce(Vector3.up * Gravity * multiplier, ForceMode.Acceleration);
    }

    // 평지 XZ 잔류속도 제거 (FixedUpdate 마지막에 실행)
    // 경사면: HandleGravity의 법선 방향 밀착 속도가 담당 → 여기서 XZ를 건드리면 안 됨
    // 평지:   HandleGravity의 v.y = -2f는 XZ에 영향 없으므로 잔류 XZ 드리프트만 정리
    void HandleSlopeStabilize()
    {
        if (_stepping) return;                 // 턱 오르기 중엔 XZ를 건드리지 않음(스텝 전진 방해 방지)
        // 가파른 경사: 미끄러짐 허용 (안정화 스킵)
        if (_onSteepSlope) return;

        float slopeAngle = Vector3.Angle(Vector3.up, _smoothedGroundNormal);
        // 경사면: HandleGravity가 법선-방향 밀착으로 이미 XZ 드리프트를 원천 차단
        //         여기서 XZ를 0으로 만들면 탄젠트 이동 성분까지 지워서 걷기가 멈춤
        if (slopeAngle > 1f) return;

        // 평지 전용: 아이들 상태에서 물리 시뮬레이션이 남긴 미세 XZ 잔류 속도 제거
        float actualXZ = Mathf.Sqrt(_rb.linearVelocity.x * _rb.linearVelocity.x
                                  + _rb.linearVelocity.z * _rb.linearVelocity.z);
        // 임계값 2f: 대쉬(15 m/s)는 건드리지 않음
        if (_isGrounded && !_isJumping && !_isSlashing && _currentSpeed < 0.01f && actualXZ < 2f)
        {
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
        }
    }

    // ── 지면 스냅 ────────────────────────────────────────────────
    // 언덕 꼭대기(볼록면)를 넘거나 내리막을 지날 때 수평 관성으로 몸이 붕 뜨는 것 방지.
    // 발밑 GroundSnapDistance 내에 지면이 있으면 그 지면으로 끌어붙인다. 그보다 큰 낙차는 진짜 낙하로 둔다.
    void HandleGroundSnap()
    {
        if (_stepping) return;                            // 턱 오르기 중엔 스냅 금지(끌어내려 스텝 방해)
        if (_onSteepSlope || _isJumping) return;          // 급경사 슬라이딩/점프 중엔 스냅 안 함
        // 접지 중엔 스냅(위치 순간이동) 금지 — 터레인·오브젝트 겹친 구간에서 관통/떨림의 원인.
        // 접지 밀착은 HandleGravity의 수직 press(velocity 기반, 물리가 해결)가 담당.
        if (_isGrounded) return;
        if (!_wasGrounded) return;                        // 표면을 '막 벗어난' 순간(크레스트/작은 낙차)에만
        TrySnapToGround();
    }

    // 발밑으로 스냅거리만큼 캐스트해서 지면이 있으면 이번 물리 스텝에 그 지면까지 밀착시킨다.
    // (대쉬 구동과 지면 스냅에서 공용)
    void TrySnapToGround()
    {
        // Rigidbody.SweepTest: 캡슐 형상 그대로 아래로 쓸어 '지면까지의 정확한 거리'를 얻는다.
        // 경사에 붙어 있으면 gap≈0(오탐 없음=떨림 없음), 실제로 뜬 만큼만 정확히 내려 붙인다.
        // (예전의 수직 아래 레이는 경사에서 접촉점 오프셋 때문에 gap을 과대평가해 떨림/미끄러짐을 유발)
        if (!_rb.SweepTest(Vector3.down, out RaycastHit hit, GroundSnapDistance,
                           QueryTriggerInteraction.Ignore))
            return;                                          // 스냅거리 내 지면 없음 → 진짜 낙하

        if (((1 << hit.collider.gameObject.layer) & GroundMask) == 0) return;   // 지면 레이어만
        float gap = hit.distance;
        if (gap <= 0.02f) return;                            // 이미 붙어 있음

        // 속도가 아니라 '위치'로만 밀착(하강 속도를 넣으면 경사에서 내리막 슬라이드로 변환됨).
        Vector3 delta = Vector3.down * gap;
        _rb.position       += delta;
        transform.position += delta;                        // 보간(Interpolate) 시각 밀림 방지

        // 지면에 붙이는 순간 '위로 뜨는' 운동량은 제거(경사/대쉬 크레스트에서 공중에 뜨는 것 방지).
        // 상승만 지우고 하강은 안 건드림 → 슬라이드로 변환될 하강속도 주입 없음.
        if (_rb.linearVelocity.y > 0f)
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);

        _isGrounded = true;                                 // 크레스트/내리막에서도 접지 유지
        _isJumping  = false;
    }

    // ── 턱·계단 자동 오르기 ──────────────────────────────────────
    // 낮은 턱/계단에 캡슐이 걸려 멈추지 않고 부드럽게 올라선다.
    // '스텝'만 대상: (1)앞면이 걸어 오를 수 없는 수직에 가까운 면 + (2)윗면이 완만(설 수 있음) + (3)무릎 높이 이하.
    // 연속 경사(slope)는 앞면 법선이 완만해 자동 제외 → 기존 경사 처리가 담당(오르기 오작동 없음).
    void HandleStepUp()
    {
        // 진행 중이 아니면 새 스텝을 감지한다. 감지되면 '같은 프레임에' 바로 아래 진행 로직으로 이어져
        // 올라가기 시작한다 → 감지 후 한 박자 늦게 시작하던 멈칫 제거.
        if (!_stepping)
        {
            if (!_isGrounded || _isJumping || _onSteepSlope) return;
            if (_moveDir.sqrMagnitude < 0.01f) return;             // 이동 입력이 있을 때만

            Vector3 dir     = new Vector3(_moveDir.x, 0f, _moveDir.z).normalized;
            float   radius  = _capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            float   bottomY = transform.position.y
                            + (_capsule.center.y - _capsule.height * 0.5f) * transform.lossyScale.y;
            float   probeDist = radius + 0.1f;   // 실제 닿기 직전에만 감지(너무 일찍 떠오르지 않게)

            // 1) 발밑 앞쪽(발목 높이)에 장애물이 있는가?
            Vector3 lowOrigin = new Vector3(transform.position.x, bottomY + 0.05f, transform.position.z);
            if (!Physics.Raycast(lowOrigin, dir, out RaycastHit lowHit, probeDist, GroundMask,
                                 QueryTriggerInteraction.Ignore))
                return;
            // 앞면이 '걸어 오를 수 없는 벽/턱'이어야 함 — 완만한 경사면이면 기존 경사 처리에 맡기고 스텝업 안 함.
            if (Vector3.Angle(Vector3.up, lowHit.normal) <= MaxSlopeAngle) return;

            // 2) 턱 높이 위쪽은 비어있는가? (막혀 있으면 벽/높은 장애물 → 오르기 불가)
            Vector3 highOrigin = new Vector3(transform.position.x, bottomY + MaxStepHeight + 0.05f, transform.position.z);
            if (Physics.Raycast(highOrigin, dir, probeDist, GroundMask, QueryTriggerInteraction.Ignore))
                return;

            // 3) 턱 윗면 높이 확인 — 앞쪽 위에서 아래로 캐스트해 설 수 있는 완만한 윗면인지 검사.
            Vector3 topOrigin = new Vector3(transform.position.x, bottomY + MaxStepHeight + 0.1f, transform.position.z)
                              + dir * (radius + 0.05f);
            if (!Physics.Raycast(topOrigin, Vector3.down, out RaycastHit topHit, MaxStepHeight + 0.1f, GroundMask,
                                 QueryTriggerInteraction.Ignore))
                return;
            if (Vector3.Angle(Vector3.up, topHit.normal) > MaxSlopeAngle) return;   // 설 수 있는 윗면만

            float stepHeight = topHit.point.y - bottomY;
            if (stepHeight <= 0.02f || stepHeight > MaxStepHeight) return;

            // 대각선(위+앞) 궤적으로 부드럽게 넘어가도록 목표를 잡고 스텝 시작.
            _stepStart  = transform.position;
            _stepTarget = transform.position + Vector3.up * stepHeight + dir * (radius * 0.8f);
            _stepTimer  = 0f;
            _stepping   = true;
        }

        // ── 진행: up+forward 등속 '속도'로 올라간다.
        // 위치를 직접 세팅(_rb.position=)하면 순간이동으로 처리돼 보간이 끊겨 멈칫하므로, 실제 velocity로 밀어
        // 물리 보간이 매끄럽게 처리하게 한다. 충돌 솔버가 모서리를 자연스럽게 타넘는다(위 성분이 우세).
        // (스텝 중엔 HandleGroundSnap/SlopeStabilize/ClampGroundedRise/ClampSteepClimb가 가드로 비활성 → 간섭 없음)
        // 스텝 도중 점프가 발동되면(HandleJump가 먼저 실행돼 _isJumping=true) 취소 → 점프 우선.
        if (_isJumping) { _stepping = false; return; }

        _rb.linearVelocity = (_stepTarget - _stepStart) / STEP_UP_TIME;   // 등속 대각선
        _stepTimer += Time.fixedDeltaTime;
        if (_stepTimer >= STEP_UP_TIME) _stepping = false;
    }

    // ── 대쉬 물리 구동 ───────────────────────────────────────────
    // 대쉬 속도를 이동 컴포넌트가 매 물리 스텝 재적용한다. 접지면 경사에 투영해 '경사를 따라' 이동 +
    // 지면 스냅 → 수평 속도가 오르막에 램프처럼 튕겨 슈퍼점프 되던 문제 해결. 공중/급경사는 수평 유지 + 중력.
    void HandleDashMove()
    {
        // 지면/경사 접촉(완만·급경사 무관) 시: 표면 법선에 투영해 '표면을 따라' 대쉬.
        // 순수 수평 속도가 오르막에 램프처럼 튕겨 슈퍼점프 되던 것을 급경사에서도 방지.
        if (_isGrounded || _onSteepSlope)
        {
            Vector3 dir = _dashDir;
            float slope = Vector3.Angle(Vector3.up, _smoothedGroundNormal);
            if (slope > 1f)
                dir = Vector3.ProjectOnPlane(_dashDir, _smoothedGroundNormal).normalized;

            _rb.linearVelocity = dir * _dashSpeed;
            // 접지 스냅(위치 순간이동)은 겹친 오브젝트 관통을 유발하므로 안 함.
            // 경사 투영으로 표면을 따라가고, 상승은 ClampGroundedRise가 상한 처리.
        }
        else
        {
            _rb.linearVelocity = new Vector3(_dashDir.x * _dashSpeed, _rb.linearVelocity.y, _dashDir.z * _dashSpeed);
            float mult = _rb.linearVelocity.y < 0f ? FallMultiplier : 1f;
            _rb.AddForce(Vector3.up * Gravity * mult, ForceMode.Acceleration);
        }

        ClampGroundedRise();   // 접지/경사 접촉 중 슈퍼점프 상한(램프 튕김·솔버 여분 제거)
    }

    // 접지·경사 접촉 중(점프가 아닌 한) 상승속도를 상한으로 제한한다.
    // 대쉬가 경사에 부딪혀 물리 솔버가 수평운동량을 위로 튕겨 슈퍼점프 되던 것을 어떤 원인이든 원천 차단.
    void ClampGroundedRise()
    {
        if (_stepping) return;                                    // 턱 오르기 상승속도는 상한 제외
        if (_isJumping) return;                                   // 점프는 제외
        if (!_isGrounded && !_onSteepSlope && !_wasGrounded) return; // 진짜 공중은 제외
        if (_rb.linearVelocity.y > MaxGroundedRiseSpeed)
        {
            Vector3 v = _rb.linearVelocity;
            v.y = MaxGroundedRiseSpeed;
            _rb.linearVelocity = v;
        }
    }

    // 급경사 표면 등반 차단 — 상태머신(_onSteepSlope)의 접지 판정/지연과 무관하게,
    // '실제로 닿은 급경사면'(콜리전 컨택트)에 대해 매 물리 스텝 등반 성분을 물리적으로 제거한다.
    // 점프로 살짝 뜬 채 경사에 수평으로 밀어붙이면 충돌 솔버가 그 성분을 표면 위로 되돌려
    // '걸어서 급경사를 올라가지던' 버그의 근본 원인을 차단. 옆이동·미끄러져 내려가기는 그대로 허용.
    void ClampSteepClimb()
    {
        if (_stepping) return;   // 턱 오르기 중엔 전진 성분을 제거하면 안 됨(스텝 앞면=수직 컨택트)
        if (!_steepContact) return;
        // 접지 상태에서 '확정 급경사'가 아니면(잔 요철 포함) 등반 차단을 걸지 않는다 → 울퉁불퉁한 길 떨림 방지.
        // _onSteepSlope는 진입 지연(hysteresis)이 있어 순간적인 요철 스파이크엔 안 켜짐.
        // 공중(점프로 급경사 우회)·확정 급경사 접지일 때만 등반 차단이 필요.
        if (_isGrounded && !_onSteepSlope) return;

        Vector3 n     = _steepContactNormal;   // 급경사면 바깥 방향 법선
        float   angle = Vector3.Angle(Vector3.up, n);
        Vector3 v     = _rb.linearVelocity;

        // 1) 표면으로 '파고드는' 성분 제거 — 솔버가 이걸 표면 위로 되돌려 등반시키므로 미리 제거(벽·경사 공통).
        //    벽 옆 점프의 경우 수직속도(vy)는 n이 수평이라 안 건드려짐 → 점프 정상.
        float into = Vector3.Dot(v, n);
        if (into < 0f) v -= n * into;

        // 2) 걸어오를 만한 '경사'(<= WALL_ANGLE)만 표면을 따라 위로 오르는 성분을 통째로 제거한다.
        //    솔버가 이전 스텝에 만든 등반이 vy로 저장돼도 여기서 함께 제거 → 점프로 급경사 우회 불가.
        //    수직에 가까운 '벽'(> WALL_ANGLE)은 up-slope=수직이라 여기서 제거하면 점프가 죽으므로 제외
        //    (벽은 수평 밀기로 등반이 안 되므로 1)만으로 충분).
        const float WALL_ANGLE = 80f;
        if (angle <= WALL_ANGLE)
        {
            Vector3 upSlope = Vector3.ProjectOnPlane(Vector3.up, n).normalized;
            float climb = Vector3.Dot(v, upSlope);
            if (climb > 0f) v -= upSlope * climb;
        }

        _rb.linearVelocity = v;
    }

    /// <summary>대쉬 시작: 이동 컴포넌트가 물리를 구동(경사 투영 + 지면 스냅)하도록 켠다.</summary>
    public void BeginDashDrive(Vector3 planarDir, float speed)
    {
        _dashDir   = planarDir.sqrMagnitude > 1e-4f ? planarDir.normalized : transform.forward;
        _dashSpeed = speed;
        _dashDriving = true;
        _stepping  = false;   // 진행 중이던 턱 오르기 취소(대쉬는 스텝 핸들러를 건너뛰므로 잔여 상태 제거)
    }

    /// <summary>대쉬 종료: 구동 해제. 이동 입력이 있으면 그 방향으로 이어서 달린다(정지→재가속 텀 제거).</summary>
    public void EndDashDrive()
    {
        _dashDriving = false;
        // 대쉬 중엔 _movementLocked로 _moveDir이 0이므로, 차단과 무관한 원시 입력으로 실제 방향키를 읽는다.
        Vector3 held = GetMoveDirection(_player.Input.MoveInputRaw);
        _planarVel = held.sqrMagnitude > 0.01f ? held * MoveSpeed : Vector3.zero;
        _rb.linearVelocity = new Vector3(_planarVel.x, _rb.linearVelocity.y, _planarVel.z);
    }

    // 설비·건물 등 GroundMask 가 아닌 콜라이더에 닿은 채 이동 입력이 없으면
    // 물리 depenetration(겹침 해소)이 만든 수평 미끄러짐을 제거한다.
    // 설비 콜라이더는 BuildPort 레이어라 _isGrounded=false(공중 취급)가 되어
    // HandleSlopeStabilize 같은 평지 안정화가 안 걸리고, 점프 후 설비 모서리에 닿으면
    // 계속 미끄러지던 문제(모든 설비 공통)를 형상과 무관하게 잡는다.
    // 이동 입력이 있으면(의도적 이동) 간섭하지 않으므로 언제든 빠져나올 수 있다.
    // 급경사 표면 접촉 감지 — 접지/공중 무관하게 '실제로 닿은 면'의 법선을 본다.
    // (SphereCast 접지 판정은 점프로 뜬 순간 급경사를 놓치므로, 컨택트 기반으로 보강)
    void EvaluateSteepContact(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & GroundMask) == 0) return; // 지면 레이어만
        int count = collision.contactCount;
        for (int i = 0; i < count; i++)
        {
            Vector3 nrm = collision.GetContact(i).normal;
            if (Vector3.Angle(Vector3.up, nrm) > MaxSlopeAngle)  // 걸어 오를 수 없는 급경사/벽
            {
                _steepContact       = true;
                _steepContactNormal = nrm;
            }
        }
    }

    void OnCollisionEnter(Collision collision) => EvaluateSteepContact(collision);

    void OnCollisionStay(Collision collision)
    {
        EvaluateSteepContact(collision);   // 급경사면 접촉 갱신(등반 차단용) — 지면 조기 반환 전에 먼저

        if (((1 << collision.gameObject.layer) & GroundMask) != 0) return; // 지면은 기존 로직대로
        if (_moveDir.magnitude > 0.1f) return;  // 의도적 이동 중엔 간섭 안 함
        if (_isSlashing) return;                // 슬래시 돌진 속도는 유지

        Vector3 v = _rb.linearVelocity;
        if (v.x * v.x + v.z * v.z < 0.0001f) return;
        v.x = 0f;
        v.z = 0f;
        _rb.linearVelocity = v;
    }

    void HandleRotation()
    {
        // 이동 잠금 / 피격 / 사망 중엔 회전도 막음
        if (_movementLocked) return;
        if (_player.Stat.IsHurt) return;
        if (_player.Stat.IsDead) return;
        if (_moveDir.magnitude < 0.1f) return;

        // 프레임레이트에 안정적인 지수 감쇠 회전(1-e^-kt) → 어느 물리 스텝에서도 같은 '휘어 도는' 감각.
        // 방향 전환 시 몸이 부드럽게 이동 방향으로 돌아가며, 정렬될수록 자연스럽게 감속.
        Quaternion targetRot = Quaternion.LookRotation(_moveDir);
        float t = 1f - Mathf.Exp(-RotSpeed * Time.fixedDeltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }

    Vector3 GetMoveDirection(Vector2 input)
    {
        if (_camera == null) return Vector3.zero;

        Quaternion yaw = _camera.GetYawRotation();
        return (yaw * Vector3.forward * input.y
              + yaw * Vector3.right * input.x).normalized;
    }

    public void StartSlash(float force, float duration)
    {
        _isSlashing = true;
        _slashForce = force;
        _slashTimer = duration;
        LockMovement(true);
    }

    /// <param name="isLocked">true=잠금, false=해제</param>
    /// <param name="applyPostUnlockDelay">
    /// true  = 공격·스킬용: 해제 후 0.15s 물리 dead zone + 0.20s 애니 sync (기본값)
    /// false = 대시용:      dead zone 없이 즉시 이동 허용 (대시는 velocity를 직접 제어하므로 불필요)
    /// </param>
    public void LockMovement(bool isLocked, bool applyPostUnlockDelay = true)
    {
        _movementLocked = isLocked;

        if (isLocked)
        {
            // 잠금 시작(공격·스킬·대시 등): 현재 X/Z 이동 속도 즉시 제거
            _currentSpeed = 0f;
            _postUnlockPhysTimer = 0f;
            _postUnlockAnimTimer = 0f;
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
        }
        else
        {
            _currentSpeed = 0f;
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);

            if (applyPostUnlockDelay)
            {
                // 공격·스킬: dead zone + anim sync 타이머 시작
                // → 해제 직후 키 입력으로 즉시 이동하는 슬라이딩 방지
                _postUnlockPhysTimer = POST_UNLOCK_PHYS;  // 0.15s 이동 차단
                _postUnlockAnimTimer = POST_UNLOCK_ANIM;  // 0.20s 애니메이션 damping=0
            }
            else
            {
                // 대시: dead zone 없이 즉시 이동 허용
                // 대시 velocity는 DashRoutine에서 직접 제어하므로 타이머 불필요
                _postUnlockPhysTimer = 0f;
                _postUnlockAnimTimer = POST_UNLOCK_ANIM;  // anim sync만 유지 (damping=0)
            }
        }
    }

    public void AddForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
        => _rb.AddForce(force, mode);

    // Attack3 중단 시 슬래시 강제 취소 (OnInterrupt에서 호출)
    public void CancelSlash()
    {
        if (!_isSlashing) return;
        _isSlashing = false;
        _slashTimer = 0f;
        _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
    }

    // 대시 방향 반환 — 카메라 기준 현재 방향키.
    // 스킬/평타 캔슬 대쉬처럼 이동입력(_moveDir)이 차단된 상황에서도 실제 방향키(MoveInputRaw)로 방향을 잡는다.
    // (차단 안 된 평소엔 _moveDir와 동일한 값)
    public Vector3 GetDashDirection()
    {
        Vector3 dir = GetMoveDirection(_player.Input.MoveInputRaw);
        return dir.magnitude > 0.1f ? dir : transform.forward;
    }

    void OnDrawGizmosSelected()
    {
        var capsule = GetComponent<CapsuleCollider>();
        if (capsule == null) return;

        float halfHeight = capsule.height / 2f;
        Vector3 origin = transform.position
                           + Vector3.down * (halfHeight - capsule.radius + GroundCheckOffset);

        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(origin, GroundCheckRadius);
    }
}