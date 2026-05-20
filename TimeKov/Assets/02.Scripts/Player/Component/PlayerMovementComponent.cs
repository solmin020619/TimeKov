using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerMovementComponent : MonoBehaviour
{
    [Header("Speed")]
    public float MoveSpeed = 5f;      // �⺻ �̵� �ӵ�
    public float SprintSpeed = 8f;      // �޸��� �ӵ�

    [Header("Jump")]
    public float JumpHeight = 2f;   // ���� ����
    public float Gravity = -20f; // �߷� ��
    public float FallMultiplier = 2.5f; // �ϰ� �� �߷� ���
    public float JumpBufferTime = 0.1f; // ���� �Է� ���� �ð�

    [Header("Movement")]
    public float RotSpeed = 10f;        // ȸ�� �ӵ�

    [Header("Ground Check")]
    public float GroundCheckRadius = 0.25f; // ���� ���� ��ü �ݰ�
    public float GroundCheckOffset = 0.05f; // ���� ���� ������
    public LayerMask GroundMask;                 // ���� ���̾� ����ũ

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
    private bool _ignoreMovementInput;  // ��� ���� ���� �̵� ���� �÷��� �߰�


    // Slash
    private bool _isSlashing;
    private float _slashTimer;
    private float _slashForce;

    public float CurrentSpeed => _currentSpeed;  // ���� �̵� �ӵ�
    public bool IsGrounded => _isGrounded;     // ���� ����
    public bool IsJumping => _isJumping;      // ���� �� ����
    public bool IsSlashing => _isSlashing;     // ������ �� ����

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
        // UI 열려있으면 점프 입력 무시, 버퍼도 초기화
        if (PlayerInputComponent.IsBlocked)
        {
            _jumpBufferCounter = 0f;
            return;
        }

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

        // ���� �ִϸ��̼� ���
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
            // LockMovement ������ Attack3Skill �ڷ�ƾ ������ ó��
            _rb.linearVelocity = new Vector3(0, _rb.linearVelocity.y, 0);
        }
    }

    void HandleMove()
    {
        if (_movementLocked) return;

        // ��� ���� ���� �� ������ �̵� ����
        if (_ignoreMovementInput)
        {
            _ignoreMovementInput = false;
            _currentSpeed = 0f;
            return;
        }

        bool isSprinting = !PlayerInputComponent.IsBlocked
                        && Input.GetKey(KeyCode.LeftShift)
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
        // �̵� ��� �߿� ȸ���� ����
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

        // ��� ���� �� �ӵ� �ʱ�ȭ
        if (!isLocked)
        {
            _currentSpeed = 0f;
            _ignoreMovementInput = true;   // ���� ���� �� ������ �̵� ����
            _rb.linearVelocity = new Vector3(0, _rb.linearVelocity.y, 0);
        }
    }

    public void AddForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
        => _rb.AddForce(force, mode);

    // ��� ���� ��ȯ, ī�޶� ���� ���� �̵� ����
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