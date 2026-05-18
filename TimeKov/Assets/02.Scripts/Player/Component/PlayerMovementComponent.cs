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
    private bool _ignoreMovementInput;  // 잠금 해제 직후 이동 무시 플래그 추가


    // Slash
    private bool _isSlashing;
    private float _slashTimer;
    private float _slashForce;

    public float CurrentSpeed => _currentSpeed;  // 현재 이동 속도
    public bool IsGrounded => _isGrounded;     // 지면 여부
    public bool IsJumping => _isJumping;      // 점프 중 여부
    public bool IsSlashing => _isSlashing;     // 슬래시 중 여부

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
    }

    void GroundCheck()
    {
        _wasGrounded = _isGrounded;

        float halfHeight = _capsule.height / 2f;
        Vector3 origin = transform.position
                           + Vector3.down * (halfHeight - _capsule.radius + GroundCheckOffset);

        _isGrounded = Physics.CheckSphere(origin, GroundCheckRadius, GroundMask);

        if (!_wasGrounded && _isGrounded)
        {
            _isJumping = false;
            if (!Input.GetButton("Jump")) _canJump = true;
        }
        else if (_wasGrounded && !_isGrounded)
        {
            _canJump = false;
        }
        else
        {
            if (_isGrounded && Input.GetButtonUp("Jump")) _canJump = true;
        }
    }

    void HandleJumpInput()
    {
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

        // 잠금 해제 직후 한 프레임 이동 무시
        if (_ignoreMovementInput)
        {
            _ignoreMovementInput = false;
            _currentSpeed = 0f;
            return;
        }

        bool isSprinting = Input.GetKey(KeyCode.LeftShift)
                        && _player.Stat.TryDrainSprintStamina()
                        && _moveDir.magnitude > 0.1f;

        _currentSpeed = _moveDir.magnitude > 0.1f
                      ? (isSprinting ? SprintSpeed : MoveSpeed)
                      : 0f;

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
            Vector3 v = _rb.linearVelocity;
            v.y = -2f;
            _rb.linearVelocity = v;
            return;
        }

        float multiplier = _rb.linearVelocity.y < 0 ? FallMultiplier : 1f;
        _rb.AddForce(Vector3.up * Gravity * multiplier, ForceMode.Acceleration);
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

    public void LockMovement(bool isLocked)
    {
        _movementLocked = isLocked;

        // 잠금 해제 시 속도 초기화
        if (!isLocked)
        {
            _currentSpeed = 0f;
            _ignoreMovementInput = true;   // 해제 직후 한 프레임 이동 무시
            _rb.linearVelocity = new Vector3(0, _rb.linearVelocity.y, 0);
        }
    }

    public void AddForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
        => _rb.AddForce(force, mode);

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