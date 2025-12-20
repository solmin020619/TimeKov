using System.Collections;
using NUnit.Framework.Interfaces;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    public string playerName = "";                          // �÷��̾� �̸�

    [Header("Time")]
    public int baseMaxTime;                                 // �ʱ� �ִ� Ÿ�� = ü��
    public int timeDecay;                                   // Raid���� �ʴ� Ÿ�� = ü�� ����

    [Header("Movement")]
    public float moveSpeed = 5f;                            // �̵��ӵ�(�ȱ�) ���׹̳� �����������
    public float runSpeed = 8f;                             // �̵��ӵ�(�ٱ�) ���׹̳� �����
    public float rotationSpeed = 10f;                       // ȸ�� �ε巴��

    [Header("Stamina")]
    public float staminaMax = 100f;                         // �ִ� ���׹̳� 100���� ����
    public float staminaRegen = 5f;                         // �ʴ� ���׹̳� ȸ���ӵ�
    public float runSpeedCost = 10f;                        // �ۋ� ����ϴ� ���׹̳�(�ʴ�)
    public float dashDistance = 3f;                         // ��ðŸ�
    public float dashDuration = 0.15f;                      // �뽬 �ð�(��)
    public float dashCost = 30f;                            // ����ҋ� ����ϴ� ���׹̳�

    [Header("Combat")]
    public int baseDefense;                                 // �⺻ ����
    public int baseAttack;                                  // �⺻ ���ݷ�

    [Header("Look")]
    public LayerMask groundLayerMask;                       // �ٴ� ���̾� (Ground)
    public float minLookDistance = 0.5f;                    // �ʹ� ������ ȸ�� ����

    [Header("GroundCheck")]
    public float groundCheckDistance = 0.2f;                // ĸ�� �Ʒ��� üũ
    public float groundStickForce = 30f;                    // �ٴڿ� ���̱��(��翡�� �ߴ� ���� ��ȭ)

    // ���� ��� ����
    private Vector3 moveInput;                              // �Է� ���� WASD
    private Vector3 dashVelocity;                           // �뽬 �� �ӵ�
    public float currentStamina;                            // ���� ���׹̳�
    
    private Rigidbody rb;
    private PlayerTime playerTime;

    private bool isRunning;                                 // �޸�����
    private bool isDashing;                                 // �뽬������
    private bool isGrounded;                                // �ٴڿ� ����ִ���

    // �ܺ�(�ִϸ��̼�)���� �б�
    public Vector3 MoveInput => moveInput;
    public bool IsRunning => isRunning;
    public bool IsDashing => isDashing;
    public bool IsGrounded => isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // �����ҋ� ���׹̳� �ִ�� ä���
        currentStamina = staminaMax;

        // Time �ý��� ��������
        playerTime = GetComponent<PlayerTime>();

        if (playerTime != null)
        {
            // PlayerTime �ʱ� ������ PlayerController���� �°� �Ѱ���
            playerTime.baseMaxTime = baseMaxTime;
            playerTime.timeDecay = timeDecay;
            playerTime.isInRaid = true;                 // ���̵� �������� �ڵ� true

            // Time�� 0�� �Ǿ����� ���ó�� �ݹ� ����
            playerTime.onTimeDepleted += OnPlayerDeath;
        }
    }

    void Update()
    {
        HandleInput();      // �Է� �ޱ�
        HandleStamina();    // ���׹̳� ȸ��/�Ҹ� ó��
        HandleDashInput();  // ��� �Է�
    }

    private void FixedUpdate()
    {
        GroundCheck();      // �ٴ� üũ
        MoveRigidbody();    // ���� ���� �̵�
        ApplyGroundStick(); // ���/�� ����
    }

    // ī�޶� LateUpdate���� ����� �� �������� ���콺 ȸ�� ó�� -> ������ ���� �ذ�
    void LateUpdate()
    {
        HandleLook();       // ���콺 ��ġ �������� �÷��̾� ȸ��
    }

    void HandleInput()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        moveInput = new Vector3(h, 0f, v);

        if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
    }
    void MoveRigidbody()
    {
        // ��� ���̸� ��� �ӵ��� ����
        if (isDashing)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x = dashVelocity.x;
            velocity.z = dashVelocity.z;
            rb.linearVelocity = velocity;
            return;
        }

        // �ٶ󺸴� ���� ���� �̵�
        Vector3 moveDir = GetMoveDirectionLocal();


        // �Է� ������ ���� ����
        if (moveDir.sqrMagnitude < 0.001f)
        {
            Vector3 velocity2 = rb.linearVelocity;
            velocity2.x = 0f;
            velocity2.z = 0f;
            rb.linearVelocity = velocity2;
            isRunning = false;
            return;
        }
        
        // �޸��� ����
        bool runKey = Input.GetKey(KeyCode.LeftShift);
        bool canRun = currentStamina >= runSpeedCost;
        bool wantRun = runKey && canRun;

        float speed = wantRun ? runSpeed : moveSpeed;

        // �޸��� ���¹̳� �Ҹ�
        isRunning = false;
        if (wantRun)
        {
            isRunning = true;
            currentStamina -= runSpeedCost * Time.fixedDeltaTime;
            currentStamina = Mathf.Max(0f, currentStamina);
        }

        // ���� �ӵ� ����(Y�� RigidBody �߷� ����)
        Vector3 desired = moveDir.normalized * speed;
        Vector3 finalVelocity = rb.linearVelocity;
        finalVelocity.x = desired.x;
        finalVelocity.z = desired.z;
        rb.linearVelocity = finalVelocity;
    }

    // ���� ���� �̵� ���� ���
    Vector3 GetMoveDirectionLocal()
    {
        Vector3 direction = transform.right * moveInput.x + transform.forward * moveInput.z;
        direction.y = 0f;
        return direction;
    }

    void HandleDashInput()
    {
        if (isDashing) return;

        if (Input.GetKeyDown(KeyCode.Space) && currentStamina >= dashCost)
        {
            Vector3 dashDir = GetMoveDirectionLocal();
            if (dashDir.sqrMagnitude < 0.001f)
                dashDir = transform.forward;

            StartCoroutine(DashRoutine(dashDir.normalized));
        }
    }

    IEnumerator DashRoutine(Vector3 dir)
    {
        isDashing = true;
        isRunning = false;

        currentStamina -= dashCost;
        currentStamina = Mathf.Max(0f,currentStamina);

        float dashSpeed = dashDistance / dashDuration;
        dashVelocity = dir * dashSpeed;

        yield return new WaitForSeconds(dashDuration);

        dashVelocity = Vector3.zero;
        isDashing = false;
    }

    void HandleStamina()
    {
        if (!isRunning && !isDashing)
        {
            currentStamina += staminaRegen * Time.deltaTime;
            currentStamina = Mathf.Min(staminaMax,currentStamina);
        }
    }

    void GroundCheck()
    {
        // ĸ�� ���� �Ʒ��� ���� Raycast
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        isGrounded = Physics.Raycast(origin, Vector3.down, groundCheckDistance + 0.1f);
    }

    void ApplyGroundStick()
    {
        // �ٴڿ��� ��¦ ���� "����" ���� ���� ��ȭ
        if (isGrounded && rb.linearVelocity.y <= 0f)
        {
            rb.AddForce(Vector3.down * groundStickForce, ForceMode.Acceleration);
        }
    }

    void HandleLook()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 hitPoint;
        bool hitFound = false;

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayerMask))
        {
            hitPoint = hit.point;
            hitFound = true;
        }
        else
        {
            Plane plane = new Plane(Vector3.up, Vector3.zero);  
            if (plane.Raycast(ray, out float enter))
            {
                hitPoint = ray.GetPoint(enter);
                hitFound = true;
            }
            else return;
        }

        hitPoint.y = transform.position.y;

        Vector3 lookDir = hitPoint - transform.position;
        lookDir.y = 0f;

        if (lookDir.sqrMagnitude < minLookDistance * minLookDistance)
            return;

        Quaternion targetRot = Quaternion.LookRotation(lookDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    // ��� ó��
    void OnPlayerDeath()
    {
        Debug.Log("PlayerController : ��� ó�� ��");

        // ������ ����
        this.enabled = false;
        rb.linearVelocity = Vector3.zero;

        // TODO: �ִϸ��̼� Dead, ��� UI, ���̽� ��ȯ ����
    }


    // �ܺ� UI��� ���׹̳� ���� �����뵵
    public float GetStamina() => currentStamina;
    public float GetStaminaMax() => staminaMax;
}
