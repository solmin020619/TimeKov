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

    // Slash
    private bool _isSlashing;
    private float _slashTimer;
    private float _slashForce;

    public float CurrentSpeed => _currentSpeed;  // 현재 이동 속도
    public bool IsGrounded => _isGrounded;     // 지면 여부
    public bool IsJumping => _isJumping;      // 점프 중 여부
    public bool IsSlashing => _isSlashing;     // 슬래시 중 여부
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

    void Update()
    {
        // 공격·스킬 잠금 중에는 이동 방향 입력 무시
        // → 잠금 해제 직후 이전에 눌렸던 키 때문에 즉시 이동하는 슬라이딩 방지
        _moveDir = _movementLocked
            ? Vector3.zero
            : GetMoveDirection(_player.Input.MoveInput);

        GroundCheck();
        HandleJumpInput();
    }

    void FixedUpdate()
    {
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

#if UNITY_EDITOR
        // ── 진단: 공중 오인식 감지 ──────────────────────────────────────
        // 상승 중(_isJumping && velocity.y > 0)인데 isGrounded=true면 무언가를 잘못 감지한 것
        if (_isGrounded && _isJumping && _rb.linearVelocity.y > 0.5f)
        {
            var overlaps = Physics.OverlapSphere(origin, GroundCheckRadius, GroundMask);
            foreach (var c in overlaps)
            {
                Debug.LogWarning(
                    $"[GroundCheck] 공중 오인식! " +
                    $"Hit='{c.name}'  Layer={LayerMask.LayerToName(c.gameObject.layer)}  " +
                    $"Pos={c.transform.position}  IsTrigger={c.isTrigger}");
            }
        }
#endif

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

        Debug.Log($"[Jump] PlayJump 호출됨 / isGrounded={_isGrounded} / canJump={_canJump}");

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

    void HandleRotation()
    {
        // 이동 잠금 중엔 회전도 막음
        if (_movementLocked) return;
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

    public void LockMovement(bool isLocked)
    {
        _movementLocked = isLocked;

        if (isLocked)
        {
            // 잠금 시작(공격·스킬 등): 현재 X/Z 이동 속도 즉시 제거
            // HandleGravity가 매 프레임 X/Z를 보존하므로, 여기서 초기화하지 않으면
            // 달리던 방향으로 공격 내내 미끄러지는 버그가 발생함
            _currentSpeed = 0f;
            _postUnlockPhysTimer = 0f;
            _postUnlockAnimTimer = 0f;
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
        }
        else
        {
            // 잠금 해제: dead zone + anim sync 타이머 시작
            _currentSpeed = 0f;
            _postUnlockPhysTimer = POST_UNLOCK_PHYS;  // 0.15s 이동 차단
            _postUnlockAnimTimer = POST_UNLOCK_ANIM;  // 0.20s 애니메이션 damping=0
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
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