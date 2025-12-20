using System.Collections;
using NUnit.Framework.Interfaces;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    public string playerName = "";                          // 플레이어 이름

    [Header("Time")]
    public int baseMaxTime;                                 // 초기 체력 타임
    public int timeDecay;                                   // Raid에서 초당 타임 = 체력 감소

    [Header("Movement")]
    public float moveSpeed = 5f;                            // 이동속도
    public float runSpeed = 8f;                             // 뛸떄 이동속도
    public float rotationSpeed = 10f;                       // 회전 부드럽게

    [Header("Stamina")]
    public float staminaMax = 100f;                         // 최대 스테미나
    public float staminaRegen = 5f;                         // 스테미나 회복속도
    public float runSpeedCost = 10f;                        // 뛸떄 소모량
    public float dashDistance = 3f;                         // 대쉬 거리
    public float dashDuration = 0.15f;                      // 대쉬 시간
    public float dashCost = 30f;                            // 대쉬 소모량

    [Header("Combat")]
    public int baseDefense;                                 // 기본 방어력
    public int baseAttack;                                  // 기본 공격력

    [Header("Look")]
    public LayerMask groundLayerMask;                       // 바닥 레이어 (Ground)
    public float minLookDistance = 0.5f;                    // 바닥 붙이기용 

    [Header("Y Lock (No Jump)")]
    public float fixedY = 0f;                               // 0으로 고정

    // 내부 사용 변수
    private Vector3 moveInput;                              
    private Vector3 dashVelocity;                           
    public float currentStamina;                            // 현재 스테미나
    
    private Rigidbody rb;
    private PlayerTime playerTime;

    private bool isRunning;                                 // 뛰는지
    private bool isDashing;                                 // 대쉬 중인지
    private bool isGrounded;                                // 땅에 닿아있는지

    // 애니메이션(외부)에서 읽기
    public Vector3 MoveInput => moveInput;
    public bool IsRunning => isRunning;
    public bool IsDashing => isDashing;
    public bool IsGrounded => isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        // 시작할떄 스테미나 최대로 채우기
        currentStamina = staminaMax;

        // Time 시스템 불러오기
        playerTime = GetComponent<PlayerTime>();

        if (playerTime != null)
        {
            // PlayerTime 초기 세팅을 PlayerController값에 맞게 넘겨줌
            playerTime.baseMaxTime = baseMaxTime;
            playerTime.timeDecay = timeDecay;
            playerTime.isInRaid = true;                 // 레이드 씬에서는 자동 true

            // Time이 0이 되었을떄 사망처리 콜백 연결
            playerTime.onTimeDepleted += OnPlayerDeath;
        }

        // 시작 위치의 Y도 고정값으로 맞춰줌(초기 틀어짐 방지)
        Vector3 p = rb.position;
        p.y = fixedY;
        rb.position = p;
    }

    void Update()
    {
        HandleInput();      // 입력 받기 
        HandleStamina();    // 스테미나 회복/소모 처리
        HandleDashInput();  // 대쉬 입력
    }

    private void FixedUpdate()
    {
        MoveRigidbody();    // 실제 물리 이동
        LockYPosition();    // Y=0 고정
    }

    // 카메라가 LateUpdate에서 따라온 후 기준으로 마우스 회전 처리 -> 떨리는 현상 해결
    void LateUpdate()
    {
        HandleLook();       // 마우스 위치 기준으로 플레이어 회전
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
        // 대쉬 중이면 대쉬 속도만 적용
        if (isDashing)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x = dashVelocity.x;
            velocity.y = 0f;
            velocity.z = dashVelocity.z;
            rb.linearVelocity = velocity;
            return;
        }

        // 바라보는 방향 기준 이동
        Vector3 moveDir = GetMoveDirectionLocal();


        // 입력 없으면 수평 유지
        if (moveDir.sqrMagnitude < 0.001f)
        {
            Vector3 velocity2 = rb.linearVelocity;
            velocity2.x = 0f;
            velocity2.y = 0f;
            velocity2.z = 0f;
            rb.linearVelocity = velocity2;
            isRunning = false;
            return;
        }
        
        // 달리기 판정
        bool runKey = Input.GetKey(KeyCode.LeftShift);
        bool canRun = currentStamina >= runSpeedCost;
        bool wantRun = runKey && canRun;

        float speed = wantRun ? runSpeed : moveSpeed;

        // 달리기 스테미나 소모
        isRunning = false;
        if (wantRun)
        {
            isRunning = true;
            currentStamina -= runSpeedCost * Time.fixedDeltaTime;
            currentStamina = Mathf.Max(0f, currentStamina);
        }

        // 최종 속도 적용 Y는 Rigidbody 중력 유지
        Vector3 desired = moveDir.normalized * speed;
        Vector3 finalVelocity = rb.linearVelocity;
        finalVelocity.x = desired.x;
        finalVelocity.y = 0f;
        finalVelocity.z = desired.z;
        rb.linearVelocity = finalVelocity;
    }

    // 로컬 방향 기준 이동 계산
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

    void HandleLook()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 hitPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayerMask))
        {
            hitPoint = hit.point;
        }
        else
        {
            Plane plane = new Plane(Vector3.up, Vector3.zero);  
            if (plane.Raycast(ray, out float enter))
            {
                hitPoint = ray.GetPoint(enter);
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

    // 사망 처리
    void OnPlayerDeath()
    {
        Debug.Log("사망");

        // 움직임 막기
        this.enabled = false;
        rb.linearVelocity = Vector3.zero;

        // TODO: 애니메이션 Dead 사망 UI 베이스 귀환 로직
    }

    void LockYPosition()
    {
        // 위치 Y 고정
        Vector3 pos = rb.position;
        pos.y = fixedY;
        rb.MovePosition(pos);

        // Y 속도도 제거 (뚝뚝 튐 방지)
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;
    }


    // 외부 UI 등에서 스테미나 관련 용도
    public float GetStamina() => currentStamina;
    public float GetStaminaMax() => staminaMax;
}
