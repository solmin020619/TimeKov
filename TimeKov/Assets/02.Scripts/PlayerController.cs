using System.Collections;
using NUnit.Framework.Interfaces;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public string playerName = "";                          // 플레이어 이름

    [Header("Time")]
    public int baseMaxTime;                                 // 초기 최대 타임 = 체력
    public int timeDecay;                                   // Raid에서 초당 타임 = 체력 감소

    [Header("Movement")]
    public float moveSpeed = 5f;                            // 이동속도(걷기) 스테미나 사용하지않음
    public float runSpeed = 8f;                             // 이동속도(뛰기) 스테미나 사용함
    public float rotationSpeed = 10f;                       // 회전 부드럽게

    [Header("Stamina")]
    public float staminaMax = 100f;                         // 최대 스테미나 100으로 고정
    public float staminaRegen = 5f;                         // 초당 스테미나 회복속도
    public float runSpeedCost = 10f;                        // 뛸떄 사용하는 스테미나(초당)
    public float dashDistance = 3f;                         // 대시거리
    public float dashDuration = 0.15f;                      // 대쉬 시간(초)
    public float dashCost = 30f;                            // 대시할떄 사용하는 스테미나

    [Header("Combat")]
    public int baseDefense;                                 // 기본 방어력
    public int baseAttack;                                  // 기본 공격력

    [Header("Look")]
    public LayerMask groundLayerMask;                       // 바닥 레이어 (Ground)
    public float minLookDistance = 0.5f;                    // 너무 가까우면 회전 무시

    // 내부 사용 변수
    private Vector3 moveInput;                              // 입력 방향 WASD
    public float currentStamina;                            // 현재 스테미나

    private CharacterController controller;
    private Rigidbody rb;
    private bool isRunning;                                 // 달리는지
    private bool isDashing;                                 // 대쉬중인지

    private PlayerTime playerTime;

    // 이동 벡터
    private Vector3 moveVelocity = Vector3.zero;            // 평소 이동 속도
    private Vector3 dashVelocity = Vector3.zero;            // 대쉬 추가 속도

    void Start()
    {
        controller = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();

        // Time 시스템 가져오기
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

        // 시작할떄 스테미나 최대로 채우기
        currentStamina = staminaMax;
    }

    void Update()
    {
        HandleInput();      // WASD 입력
        HandleMovement();   // 이동 방향/속도 계산
        HandleStamina();    // 스테미나 회복/소모 처리
        HandleDashInput();  // 대시 입력

        ApplyMovement();    //종 이동 (항상 여기에서만 Move 호출)
    }

    // 카메라가 LateUpdate에서 따라온 후 기준으로 마우스 회전 처리 -> 떨리는 현상 해결
    void LateUpdate()
    {
        HandleLook();       // 마우스 위치 기준으로 플레이어 회전
    }

    // 입력
    void HandleInput()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // XZ 평면에서의 이동 방향 y는 0
        moveInput = new Vector3(h, 0f, v);

        // 대각선 이동시 속도가 빨라지는 현상을 방지하기 위해 정규화
        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();
    }

    // 이동 백터만 계산
    void HandleMovement()
    {
        if (isDashing)
        {
            // 대쉬 중에는 일반 이동 벡터는 0으로 (대쉬 속도만 적용)
            moveVelocity = Vector3.zero;
            isRunning = false;
            return;
        }

        isRunning = false;

        // 입력 없으면 멈춤
        if (moveInput.sqrMagnitude < 0.001f)
        {
            moveVelocity = Vector3.zero;
            return;
        }

        // 카메라 기준으로 방향 계산(쿼터뷰 방식)
        Camera cam = Camera.main;
        Vector3 moveDir;

        if (cam != null)
        {
            // 카메라의 앞/오른쪽 방향에서 Y를 제거해 수평방향으로만 사용
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;

            // XZ 평면에 투영
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            // 입력 백터를 카메라 기준으로 변환
            moveDir = camRight * moveInput.x + camForward * moveInput.z;
        }
        else
        {
            // 카메라가 없다면 플레이어 자신의 축 기준으로 이동
            moveDir = transform.right * moveInput.x + transform.forward * moveInput.z;
        }

        // 달리기
        bool runKey = Input.GetKey(KeyCode.LeftShift);
        // 스테미나가 달리기 코스트 이상일떄만 달리기 가능
        bool canRun = currentStamina >= runSpeedCost;
        bool wantRun = runKey && canRun;

        float speed = wantRun ? runSpeed : moveSpeed;

        // 실제로 달릴떄 스테미나 소모
        if (wantRun)
        {
            isRunning = true;
            currentStamina -= runSpeedCost * Time.deltaTime;
            if (currentStamina < 0f)
                currentStamina = 0f;
        }

        // 최종 이동 속도 벡터 (방향 * 속도)
        moveVelocity = moveDir.normalized * speed;
    }

    // 마우스 방향 회전
    void HandleLook()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // 화면상의 마우스 위치에서 월드로 나가는 Ray
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 hitPoint;
        bool hitFound = false;

        // 바닥 레이어에 콜라이더가 있다면 거기 먼저 Raycast
        if(Physics.Raycast(ray,out RaycastHit hit, 1000f, groundLayerMask))
        {
            hitPoint = hit.point;
            hitFound = true;
        }
        else
        {
            // 바닥 콜라이더가 없거나 안맞으면 y=0 평면 기준으로라도 계산
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if(groundPlane.Raycast(ray,out float enter))
            {
                hitPoint = ray.GetPoint(enter);
                hitFound = true;
            }
            else
            {
                hitFound = false;
                hitPoint = Vector3.zero;
            }
        }

        if (!hitFound) return;

        // 플레이어 높이에 맞춰서 수평 방향만 계산
        hitPoint.y = transform.position.y;

        Vector3 lookDir = hitPoint - transform.position;
        lookDir.y = 0f;

        // 너무 가까운 경우(발밑에 커서 있을 때)는 회전하지 않음 → 떨림 방지
        if (lookDir.sqrMagnitude < minLookDistance * minLookDistance)
            return;

        // 해당 방향을 향하도록 회전 -> Slerp로 부드럽게
        Quaternion targetRot = Quaternion.LookRotation(lookDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    
    }

    // 스테미나 회복
    void HandleStamina()
    {
        if (!isRunning && !isDashing)
        {
            currentStamina += staminaRegen * Time.deltaTime;
            if (currentStamina > staminaMax)
                currentStamina = staminaMax;
        }
    }

    // 대시 입력 처리
    void HandleDashInput()
    {
        if (isDashing) return;

        // Space 입력 + 스테미나 충분할떄만 Dash 시작
        if (Input.GetKeyDown(KeyCode.Space) && currentStamina >= dashCost)
        {
            Vector3 dashDir;

            if (moveInput.sqrMagnitude > 0.001f)
            {
                // 이동 입력이 있을때는 그 방향으로 대시
                Camera cam = Camera.main;

                if (cam != null)
                {
                    Vector3 camForward = cam.transform.forward;
                    Vector3 camRight = cam.transform.right;

                    camForward.y = 0f;
                    camRight.y = 0f;
                    camForward.Normalize();
                    camRight.Normalize();

                    dashDir = (camRight * moveInput.x + camForward * moveInput.z).normalized;
                }
                else
                {
                    dashDir = (transform.right * moveInput.x + transform.forward * moveInput.z).normalized;
                }
            }
            else
            {
                // 입력 없으면 현재 바라보는 방향 대쉬
                dashDir = transform.forward;
            }

            StartCoroutine(DashRoutine(dashDir));
        }
    }

    // 대시 코루틴
    IEnumerator DashRoutine(Vector3 dashDir)
    {
        isDashing = true;
        isRunning = false;

        currentStamina -= dashCost;
        if (currentStamina < 0f)
            currentStamina = 0f;

        float dashSpeed = dashDistance / dashDuration;

        // 대쉬 중에는 dashVelocity로만 이동
        dashVelocity = dashDir * dashSpeed;

        yield return new WaitForSeconds(dashDuration);

        dashVelocity = Vector3.zero;
        isDashing = false;
    }

    // 최종 이동 적용
    void ApplyMovement()
    {
        // 걷기/달리기 + 대시 속도를 합친 최종 속도
        Vector3 finalVelocity = moveVelocity + dashVelocity;

        // Y축은 따로 안 건드리니, 평평한 맵 기준으로 수평 이동만 함
        controller.Move(finalVelocity * Time.deltaTime);
    }

    void HandleAttackInput()
    {
        // 현재는 PlayerWeaponController에서 처리
    }

    // 사망 처리
    void OnPlayerDeath()
    {
        Debug.Log("PlayerController : 사망 처리 중");

        // 움직임 막기
        this.enabled = false;

        // TODO: 애니메이션 Dead, 사망 UI, 베이스 귀환 로직
    }

    public Vector3 MoveInput => moveInput;
    public bool IsRunning => isRunning;
    public bool IsDashing => isDashing;

    // 외부 UI등에서 스테미나 관련 설정용도
    public float GetStamina() => currentStamina;
    public float GetStaminaMax() => staminaMax;
}
