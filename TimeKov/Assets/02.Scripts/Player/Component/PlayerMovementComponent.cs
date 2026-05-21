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
    public LayerMask GroundMask;                 // 지면 레이어 마스크

    private Player _player;
    private Rigidbody _rb;
    private CapsuleCollider _capsule;
    private ThirdPersonCamera _camera;

    private Vector3 _moveDir;
    private bool _isGrounded;
    private bool _wasGrounded;
    private bool _canJump = true;
    private float _jumpBufferCounter;
    private bool _jumpRequested;
    private float _currentSpeed;
    private bool _isJumping;
    private bool _movementLocked;
    private float _postUnlockTimer;      // 잠금 해제 직후 이동 복구 타이머 (애니메이션 블렌딩 동기화)
    private const float POST_UNLOCK_RAMP = 0.15f; // 애니메이션 SetFloat damping(0.15f)과 동일

    // Slash
    private bool _isSlashing;
    private float _slashTimer;
    private float _slashForce;

    // Dash (PlayerDashComponent가 SetDashing으로 토글. HandleSlopeStabilize / LockMovement가 velocity 안 건드리도록 가드)
    private bool _isDashing;

    public float CurrentSpeed => _currentSpeed;  // 현재 이동 속도
    public bool IsGrounded => _isGrounded;     // 지면 여부
    public bool IsJumping => _isJumping;      // 점프 중 여부
    public bool IsSlashing => _isSlashing;     // 슬래시 중 여부
    public bool IsDashing => _isDashing;      // 대시 중 여부

    public void SetDashing(bool dashing) => _isDashing = dashing;

    void Awake()
    {
        _player = GetComponent<Player>();
        _rb = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>();
        _camera = FindAnyObjectByType<ThirdPersonCamera>();

        _rb.freezeRotation = true;
        _rb.useGravity = false;
    }

    void Update()
    {
        _moveDir = GetMoveDirection(_player.Input.MoveInput);

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

        _isGrounded = Physics.CheckSphere(origin, GroundCheckRadius, GroundMask);

        // UI 열려있을 때 점프 홀드 상태 무시 (팀원 추가)
        bool jumpHeld = !PlayerInputComponent.IsBlocked && Input.GetButton("Jump");

        if (!_wasGrounded && _isGrounded)
        {
            _isJumping = false;
            if (!jumpHeld) _canJump = true;
        }
        else if (_wasGrounded && !_isGrounded)
        {
            _canJump = false;
        }
        else
        {
            if (_isGrounded && !PlayerInputComponent.IsBlocked && Input.GetButtonUp("Jump"))
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

        if (Input.GetButtonDown("Jump") && _isGrounded && _canJump)
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

        // UI 열려있을 때 스프린트 무시 (팀원 추가)
        bool isSprinting = !PlayerInputComponent.IsBlocked
                        && Input.GetKey(KeyCode.LeftShift)
                        && _player.Stat.TryDrainSprintStamina()
                        && _moveDir.magnitude > 0.1f;

        float targetSpeed = _moveDir.magnitude > 0.1f
                          ? (isSprinting ? SprintSpeed : MoveSpeed)
                          : 0f;

        // 잠금 해제 직후 점진적 속도 복구 (애니메이션 SetFloat damping 0.15f 와 동기화)
        // → 속도가 갑자기 튀지 않아 공격 후 "살짝 끌리는" 현상 제거
        if (_postUnlockTimer > 0f)
        {
            _postUnlockTimer -= Time.fixedDeltaTime;
            float ramp = 1f - Mathf.Clamp01(_postUnlockTimer / POST_UNLOCK_RAMP);
            _currentSpeed = targetSpeed * ramp;
        }
        else
        {
            _currentSpeed = targetSpeed;
        }

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
            // 정지 상태(공격 중 포함): y를 0으로 고정
            //   → 경사면에서 -2 하향 속도가 법선 벡터에 의해 X/Z로 분해되는 것을 방지
            // 이동 중 또는 슬래시 중: -2로 유지하여 지면 밀착 유지
            bool isStationary = _currentSpeed < 0.01f && !_isSlashing;
            float groundY = isStationary ? 0f : -2f;

            Vector3 v = _rb.linearVelocity;
            v.y = groundY;
            _rb.linearVelocity = v;
            return;
        }

        float multiplier = _rb.linearVelocity.y < 0 ? FallMultiplier : 1f;
        _rb.AddForce(Vector3.up * Gravity * multiplier, ForceMode.Acceleration);
    }

    // 경사면 슬라이딩 완전 차단 (FixedUpdate 마지막에 실행)
    // 물리 시뮬레이션이 경사 법선 계산으로 남긴 X/Z 잔류 속도를 최종 정리
    void HandleSlopeStabilize()
    {
        // 대시 중에는 dashForce가 X/Z velocity에 살아있어야 하므로 안정화 skip
        if (_isDashing) return;
        if (_isGrounded && !_isJumping && !_isSlashing && _currentSpeed < 0.01f)
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

    public void LockMovement(bool isLocked, bool preserveVelocity = false)
    {
        _movementLocked = isLocked;

        if (isLocked)
        {
            // 잠금 시작(공격·스킬 등): 현재 X/Z 이동 속도 즉시 제거
            // HandleGravity가 매 프레임 X/Z를 보존하므로, 여기서 초기화하지 않으면
            // 달리던 방향으로 공격 내내 미끄러지는 버그가 발생함
            // 단, preserveVelocity=true (대시 등)일 땐 velocity 안 건드림 — 호출 직후 dashForce 박을 거라
            _currentSpeed = 0f;
            _postUnlockTimer = 0f;
            if (!preserveVelocity)
                _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
        }
        else
        {
            // 잠금 해제 시: 속도 초기화 + 점진적 복구 타이머 시작
            _currentSpeed = 0f;
            _postUnlockTimer = POST_UNLOCK_RAMP;  // 0.15s 동안 0→MoveSpeed 선형 램프
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