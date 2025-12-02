using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

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
    public float staminaRegen = 5f;                        // 초당 스테미나 회복속도
    public float runSpeedCost = 10f;                        // 뛸떄 사용하는 스테미나
    public float dashDistance = 3f;                         // 대시거리
    public float dashDuration = 0.15f;                      // 대쉬 시간(초)
    public float dashCost = 30f;                            // 대시할떄 사용하는 스테미나

    [Header("Combat")]
    public int baseDefense;                                 // 기본 방어력
    public int baseAttack;                                  // 기본 공격력

    // 내부 사용 변수
    private Vector3 moveInput;                              // 입력 방향 WASD
    public float currentStamina;                           // 현재 스테미나
    private CharacterController controller;                 
    private bool isRunning;                                 // 달리는지
    private bool isDashing;                                 // 대쉬중인지

    private PlayerTime playerTime;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Time 시스템 가져오기
        playerTime = GetComponent<PlayerTime>();

        // PlayerController의 변수 전달
        if(playerTime != null)
        {
            playerTime.baseMaxTime = baseMaxTime;
            playerTime.timeDecay = timeDecay;
            playerTime.isInRaid = true;                 // 레이드 씬에서는 자동 true

            playerTime.onTimeDepleted += OnPlayerDeath;
        }

        // 시작할떄 스테미나 최대로 채우기
        currentStamina = staminaMax;
    }

    void Update()
    {
        HandleInput();                  // WASD 입력
        HandleMovement();               // 이동 + 회전
        HandleStamina();                // 스테미나 회복
        HandleDashInput();              // 대시 입력
    }

    // 입력처리
    void HandleInput()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        moveInput = new Vector3(h, 0f, v);

        // 대각선 움직임 속도 일정하게
        if(moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();
    }

    // 카메라 기준 쿼터뷰 이동
    void HandleMovement()
    {
        if(isDashing) return;

        isRunning = false;

        // 입력 없으면 멈춤
        if(moveInput.sqrMagnitude < 0.001f)
        {
            controller.SimpleMove(Vector3.zero);
            return;
        }

        // 카메라 기준 방향 가져오기
        Camera cam = Camera.main;
        Vector3 moveDir;

        if(cam != null)
        {
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;

            // XZ 평면에 투영
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            // 카메라 기준 이동 방향
            moveDir = camRight * moveInput.x + camForward * moveInput.z;
        }
        else
        {
            // 혹시 카메라 못 찾으면 자기 기준으로라도 이동
            moveDir = transform.right * moveInput.x + transform.forward * moveInput.z;
        }

        // 달리기
        bool wantRun = Input.GetKey(KeyCode.LeftShift) && currentStamina > 0f;
        float speed = wantRun ? runSpeed : moveSpeed;

        if (wantRun)
        {
            isRunning = true;
            currentStamina -= runSpeedCost * Time.deltaTime;
            if(currentStamina < 0f)
                currentStamina = 0f;
        }

        // 실제 이동
        controller.SimpleMove(moveDir.normalized * speed);

        Quaternion targetRot = Quaternion.LookRotation(moveDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    // 스테미나 회복
    void HandleStamina()
    {
        if(!isRunning && !isDashing)
        {
            currentStamina += staminaRegen * Time.deltaTime;
            if(currentStamina > staminaMax)
                currentStamina = staminaMax;
        }
    }

    // 대시 입력
    void HandleDashInput()
    {
        if (isDashing) return;

        if(Input.GetKeyDown(KeyCode.Space) && currentStamina >= dashCost)
        {
            Vector3 dashDir;

            if(moveInput.sqrMagnitude > 0.001f)
            {
                //이동 입력이 있을때는 그 방향으로 대시
                Camera cam = Camera.main;

                if(cam != null)
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

    // 대쉬 코루틴
    IEnumerator DashRoutine(Vector3 dashDir)
    {
        isDashing = true;
        isRunning = false;

        currentStamina -= dashCost;

        if(currentStamina < 0f) 
            currentStamina = 0f;

        float elapsed = 0f;
        float dashSpeed = dashDistance / dashDuration;

        while(elapsed < dashDuration)
        {
            controller.SimpleMove(dashDir * dashSpeed);
            elapsed += Time.deltaTime;
            yield return null;
        }

        isDashing = false;
    }

    void OnPlayerDeath()
    {
        Debug.Log("PlayerController : 사망 처리 중");

        // 움직임 막기
        this.enabled = false;

        // 애니메이션 Dead 사망 UI 베이스 귀환 로직
    }

    public float GetStamina() => currentStamina;
    public float GetStaminaMax() => staminaMax;
}
