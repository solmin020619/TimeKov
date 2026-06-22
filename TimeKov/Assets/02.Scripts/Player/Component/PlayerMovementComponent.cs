using System.Collections;
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
    public float RotSpeed = 10f;        // 회전 속도

    [Header("Ground Check")]
    public float GroundCheckRadius = 0.25f; // 지면 감지 구체 반경
    public float GroundCheckOffset = 0.05f; // 지면 감지 오프셋
    public LayerMask GroundMask;            // 지면 레이어 마스크

    [Header("Slope")]
    [Tooltip("이 각도(도) 초과 경사면은 올라갈 수 없고 미끄러짐")]
    public float MaxSlopeAngle   = 45f;   // 최대 등반 가능 경사각
    public float SlopeSlideSpeed = 4f;    // 가파른 경사에서 미끄러지는 속도

    [Header("Death")]
    [Tooltip("죽은 뒤 바닥에 안착(공중=낙하/경사=안착)되기를 기다리는 최대 시간(초). 지나면 강제로 지면 스냅 후 고정.")]
    public float DeathSettleMaxTime = 3f;

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
    private const float STEEP_SLOPE_ENTER_DELAY = 0.08f; // 이 시간 이상 급경사여야 슬라이딩 시작
    private bool _canJump = true;
    private float _jumpBufferCounter;
    private bool _jumpRequested;
    private float _currentSpeed;
    private bool _isJumping;
    private bool _movementLocked;
    private float _postUnlockPhysTimer;   // 물리 dead zone: 이 시간 동안 velocity=0 (공격 후 즉시 이동 차단)
    private float _postUnlockAnimTimer;   // 애니메이션 sync: IsPostLockTransition 유지 (damping=0)
    private const float POST_UNLOCK_PHYS = 0.15f; // 물리 dead zone 길이
    private const float POST_UNLOCK_ANIM = 0.20f; // anim sync 길이 (dead zone + 1프레임 여유)

    private bool _settling;            // 사망 안착 진행 중(동적 상태로 중력 낙하/경사 안착 중)
    private Coroutine _settleCo;

    // 사망 처리: 죽는 순간 즉시 고정하지 않고, 중력으로 바닥까지 떨어뜨리거나 경사에 안착시킨 뒤 고정(FreezeOnDeath -> SettleThenFreezeRoutine).

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
            _player.Stat.OnDead += FreezeOnDeath;
        }
    }

    void OnDisable()
    {
        if (_player?.Stat != null)
        {
            _player.Stat.OnDead -= FreezeOnDeath;
        }
    }

    /// <summary>
    /// 사망 처리: 죽는 순간 즉시 고정하지 않고, 중력으로 바닥까지 떨어뜨리거나(공중) 경사에 안착시킨 뒤 고정한다.
    /// - 공중사망: 동적 상태 유지 + 중력으로 수직 낙하 -> 접지 후 고정(순간이동 스냅 아님).
    /// - 경사사망: 그 자리 안착 + yaw만 유지(가로로 안 눕혀 맵 뚫림 방지).
    /// - 극단 케이스(허공): 타임아웃 후 SnapDownToGround(폴백 레이캐스트)로 강제 안착.
    /// </summary>
    private void FreezeOnDeath()
    {
        if (_settling) return;   // 중복 OnDead 방지
        _rb.angularVelocity = Vector3.zero;
        _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);   // 수평 관성만 제거, 낙하(Y)는 유지
        UprightYaw();

        // kinematic 으로 박지 않음 = 동적 상태로 중력 낙하/경사 안착 후(아래 코루틴) 고정.
        _settling = true;
        _settleCo = StartCoroutine(SettleThenFreezeRoutine());
    }

    // 접지 + 거의 정지(또는 타임아웃)까지 기다렸다 지면 스냅 + kinematic 고정.
    private IEnumerator SettleThenFreezeRoutine()
    {
        yield return new WaitForFixedUpdate();   // 최소 한 스텝(이미 접지면 다음 루프서 즉시 종료)
        float t = 0f;
        while (t < DeathSettleMaxTime)
        {
            if (_isGrounded && Mathf.Abs(_rb.linearVelocity.y) < 0.6f) break;   // 바닥 안착 완료
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
        SnapDownToGround();   // 최종 정렬(폴백 포함 = 못 찾아도 안전)
        UprightYaw();
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;
        _settling = false;
        _settleCo = null;
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
        if (_settleCo != null) { StopCoroutine(_settleCo); _settleCo = null; }   // 안착 코루틴 진행 중이면 중단
        _settling = false;
        _rb.isKinematic = false;
        UprightYaw();
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
        // 사망 시 모든 물리 이동 정지 (경사면 미끄러짐 방지)
        // FreezeOnDeath가 kinematic으로 전환하지만, 전환 직전 1프레임이나
        // HandleGravity의 경사 밀착(중력 Y를 경사면 접선으로 투영)이 잔류 XZ 속도를
        // 다시 만들 수 있어, 핸들러 진입 전에 여기서 확실히 차단한다.
        if (_player.Stat.IsDead)
        {
            _currentSpeed = 0f;
            if (_settling && !_rb.isKinematic)
            {
                // 안착 중: 중력으로 낙하/경사 안착. 수평 관성만 제거하고 낙하(Y)는 유지.
                Vector3 v = _rb.linearVelocity;
                v.x = 0f; v.z = 0f;
                if (!_rb.useGravity) v.y += Gravity * Time.fixedDeltaTime;   // 커스텀 중력(useGravity off)이면 수동 낙하
                _rb.linearVelocity = v;
            }
            else if (!_rb.isKinematic)
            {
                _rb.linearVelocity  = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            return;
        }

        HandleJump();
        HandleSlash();
        HandleMove();
        HandleGravity();
        HandleRotation();
        HandleSlopeStabilize(); // 경사면 슬라이딩 방지 (마지막 실행)
    }

    void GroundCheck()
    {
        _wasGrounded = _isGrounded;

        float halfHeight = _capsule.height / 2f;
        Vector3 origin = transform.position
                           + Vector3.down * (halfHeight - _capsule.radius + GroundCheckOffset);

        // QueryTriggerInteraction.Ignore: Trigger 콜라이더를 지면으로 인식하지 않음
        // (Ground 레이어에 대형 Trigger가 있어도 무한점프 방지)
        _isGrounded = Physics.CheckSphere(origin, GroundCheckRadius, GroundMask,
                                          QueryTriggerInteraction.Ignore);

        // SphereCast 로 지면 법선 획득 → 경사각 계산
        Vector3 castStart = transform.position + Vector3.up * 0.1f;
        float castDist    = halfHeight + GroundCheckOffset + 0.15f;

        if (Physics.SphereCast(castStart, GroundCheckRadius * 0.9f,
                               Vector3.down, out RaycastHit hit, castDist, GroundMask,
                               QueryTriggerInteraction.Ignore))
        {
            _groundNormal = hit.normal;
        }
        else
        {
            _groundNormal = Vector3.up;
        }

        // SphereCast 원시 법선을 스무딩: 폴리곤 경계·돌출부에서 1~2프레임 노이즈 제거
        // Slerp factor 15 × deltaTime ≈ 0.25/frame → 약 4프레임에 걸쳐 부드럽게 수렴
        _smoothedGroundNormal = Vector3.Slerp(
            _smoothedGroundNormal, _groundNormal, Time.deltaTime * 15f);

        float slopeAngle = Vector3.Angle(Vector3.up, _smoothedGroundNormal);

        // Hysteresis: 급경사(> MaxSlopeAngle)가 STEEP_SLOPE_ENTER_DELAY 초 이상 지속될 때만
        // _onSteepSlope = true → 폴리곤 경계를 밟는 순간 1~2프레임 오탐으로 인한 불필요한 슬라이딩 방지
        bool rawSteep = _isGrounded && slopeAngle > MaxSlopeAngle;
        if (rawSteep)
            _steepSlopeTimer += Time.deltaTime;
        else
            _steepSlopeTimer = 0f;

        _onSteepSlope = _steepSlopeTimer >= STEEP_SLOPE_ENTER_DELAY;

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

        if (_player.Input.JumpPressed && _isGrounded && _canJump)
            _jumpBufferCounter = JumpBufferTime;
        else
            _jumpBufferCounter = Mathf.Max(_jumpBufferCounter - Time.deltaTime, 0);

        if (_isGrounded && _jumpBufferCounter > 0f && _canJump)
        {
            _jumpRequested = true;
            _jumpBufferCounter = 0f;
            _canJump = false;
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
        if (_movementLocked) return;

        // 피격 경직 중: 이동 완전 차단 + velocity 즉시 0
        if (_player.Stat.IsHurt)
        {
            _currentSpeed = 0f;
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            return;
        }

        // 사망 중: 이동 완전 차단
        if (_player.Stat.IsDead)
        {
            _currentSpeed = 0f;
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            return;
        }

        // 가파른 경사: 이동 차단 + 경사면 표면 방향(3D 전체)으로 미끄러짐
        // XZ 만 설정하면 HandleGravity 의 y=-2 와 충돌 → 경사면 안으로 밀림 → 떨림
        // → 경사면에 접선인 전체 벡터로 설정해 충돌 반응 제거
        if (_onSteepSlope)
        {
            Vector3 slideDir    = Vector3.ProjectOnPlane(Vector3.down, _groundNormal).normalized;
            Vector3 targetSlide = slideDir * SlopeSlideSpeed;
            // Lerp: 갑작스런 속도 변화 없이 부드럽게 미끄러짐
            _rb.linearVelocity  = Vector3.Lerp(_rb.linearVelocity, targetSlide, Time.fixedDeltaTime * 12f);
            _currentSpeed = 0f;
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
        // → 잠금 해제 후 곧바로 뛰는 현상 제거
        if (_postUnlockPhysTimer > 0f)
        {
            _postUnlockPhysTimer -= Time.fixedDeltaTime;
            _postUnlockAnimTimer  = Mathf.Max(0f, _postUnlockAnimTimer - Time.fixedDeltaTime);
            _currentSpeed = 0f;
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            return;
        }

        // dead zone 종료 후 anim sync 타이머만 감소 (이동은 즉시 허용)
        if (_postUnlockAnimTimer > 0f)
            _postUnlockAnimTimer -= Time.fixedDeltaTime;

        _currentSpeed = targetSpeed;

        _rb.linearVelocity = new Vector3(
            _moveDir.x * _currentSpeed,
            _rb.linearVelocity.y,
            _moveDir.z * _currentSpeed
        );
    }

    void HandleGravity()
    {
        if (_isGrounded && _rb.linearVelocity.y <= 0)
        {
            // 가파른 경사: HandleMove 가 경사면 접선 속도(y 포함)를 전담 → 스킵
            if (_onSteepSlope) return;

            float slopeAngle = Vector3.Angle(Vector3.up, _smoothedGroundNormal);

            if (slopeAngle > 1f)
            {
                // ── 경사면 지면 밀착 ──────────────────────────────────────
                // 기존: v.y = -2f (수직 하강) → 경사 법선과 충돌 해결 시 XZ depenetration 발생
                //       physics가 XZ를 매 프레임 추가 → HandleMove가 XZ=0으로 리셋해도
                //       position correction이 누적되어 미끄러짐 발생
                //
                // 수정: 경사 법선 방향(수직·경사 모두 포함)으로 밀착 속도 설정
                //       velocity ⊥ 경사면 → physics 충돌해결의 XZ 성분 = 0 → 미끄러짐 없음
                //       탄젠트 이동 성분은 ProjectOnPlane으로 보존 (걷기·달리기 정상 작동)
                Vector3 tangential = Vector3.ProjectOnPlane(_rb.linearVelocity, _smoothedGroundNormal);
                _rb.linearVelocity = tangential + (-_smoothedGroundNormal * 2f);
            }
            else
            {
                // ── 평지 지면 밀착 (기존 방식 유지) ───────────────────────
                Vector3 v = _rb.linearVelocity;
                v.y = -2f;
                _rb.linearVelocity = v;
            }
            return;
        }

        float multiplier = _rb.linearVelocity.y < 0 ? FallMultiplier : 1f;
        _rb.AddForce(Vector3.up * Gravity * multiplier, ForceMode.Acceleration);
    }

    // 평지 XZ 잔류속도 제거 (FixedUpdate 마지막에 실행)
    // 경사면: HandleGravity의 법선 방향 밀착 속도가 담당 → 여기서 XZ를 건드리면 안 됨
    // 평지:   HandleGravity의 v.y = -2f는 XZ에 영향 없으므로 잔류 XZ 드리프트만 정리
    void HandleSlopeStabilize()
    {
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

    // 설비·건물 등 GroundMask 가 아닌 콜라이더에 닿은 채 이동 입력이 없으면
    // 물리 depenetration(겹침 해소)이 만든 수평 미끄러짐을 제거한다.
    // 설비 콜라이더는 BuildPort 레이어라 _isGrounded=false(공중 취급)가 되어
    // HandleSlopeStabilize 같은 평지 안정화가 안 걸리고, 점프 후 설비 모서리에 닿으면
    // 계속 미끄러지던 문제(모든 설비 공통)를 형상과 무관하게 잡는다.
    // 이동 입력이 있으면(의도적 이동) 간섭하지 않으므로 언제든 빠져나올 수 있다.
    void OnCollisionStay(Collision collision)
    {
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

        Quaternion targetRot = Quaternion.LookRotation(_moveDir);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRot, RotSpeed * Time.fixedDeltaTime);
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

    // 대시 방향 반환, 카메라 기준 현재 이동 방향
    public Vector3 GetDashDirection()
    {
        return _moveDir.magnitude > 0.1f ? _moveDir : transform.forward;
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