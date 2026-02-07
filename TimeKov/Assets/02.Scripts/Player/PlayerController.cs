using System.Collections;
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

    [Header("Combat")]
    public int baseDefense;                                 // 기본 방어력
    public int baseAttack;                                  // 기본 공격력

    [Header("Y Lock (No Jump)")]
    public float fixedY = 0f;                               // 0으로 고정

    [Header("FPS Look")]
    public Transform cameraPivot;                           // 상하(Pitch) 회전용 피벗(카메라 부모)
    public float mouseSensitivity = 2.0f;                   // 기본 감도(프로젝트 체감값)
    public float pitchMin = -80f;                           // 위로 최대 각도
    public float pitchMax = 80f;                            // 아래로 최대 각도
    public bool lockCursor = true;                          // 플레이 시작 시 커서 잠금

    // 내부 사용 변수
    private Vector3 moveInput;                              // wasd 입력값
    public float currentStamina;                            // 현재 스테미나

    private bool isRunning;                                 // 뛰는지

    // FPS 회전값(누적)
    private float yaw;                                      // 좌우
    private float pitch;                                    // 상하

    // 회전 캐싱(Update에서 계산 -> FixedUpdate에서 적용)
    private Quaternion cachedYawRotation;
    private bool hasCachedYawRotation;

    // 컴포넌트 캐싱
    private Rigidbody rb;
    private PlayerTime playerTime;

    // 애니메이션(외부)에서 읽기
    public Vector3 MoveInput => moveInput;
    public bool IsRunning => isRunning;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;                                          // 점프 중력 없음
        rb.interpolation = RigidbodyInterpolation.Interpolate;          // 물리 보간(화면 떨림 완화)
        rb.freezeRotation = true;                                       // 물리 충돌로 회전하지않게 고정

        // 시작할떄 스테미나 최대로 채우기
        currentStamina = staminaMax;

        // Time 시스템 연결
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

        // Y 속도 제거
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        // 카메라 피벗 자동 보정(미할당 시 메인카메라 부모/본인으로 최대한 찾기)
        if (cameraPivot == null && Camera.main != null)
        {
            // 카메라가 플레이어 자식이라면 그대로 사용
            cameraPivot = Camera.main.transform.parent != null ? Camera.main.transform.parent : Camera.main.transform;
        }

        // 시작 yaw/pitch 초기화
        yaw = transform.eulerAngles.y;
        pitch = (cameraPivot != null) ? cameraPivot.localEulerAngles.x : 0f;
        pitch = NormalizeAngle(pitch);

        // 커서 잠금
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        HandleInput();          // 입력 받기 
        HandleStamina();        // 스테미나 회복/소모 처리
        HandleMouseLook();      // FPS 마우스룩
    }

    private void FixedUpdate()
    {
        MoveRigidbody();        // 실제 물리 이동
        ApplyCachedYawRotation();// 좌우 회전 적용(Rigidbody)
        LockYPosition();        // Y=0 고정
    }

    void HandleInput()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        moveInput = new Vector3(h, 0f, v);

        // 대각선 속도 보정
        if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
    }

    void MoveRigidbody()
    {
        // 로컬 기준 이동 방향
        Vector3 moveDir = GetMoveDirectionLocal();

        // 입력 없으면 정지
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

        // 최종 속도 적용
        Vector3 desired = moveDir.normalized * speed;
        Vector3 finalVelocity = rb.linearVelocity;
        finalVelocity.x = desired.x;
        finalVelocity.y = 0f;
        finalVelocity.z = desired.z;
        rb.linearVelocity = finalVelocity;
    }

    // 로컬 방향 기준 이동 계산(플레이어가 바라보는 방향 기준 이동 벡터 계산)
    Vector3 GetMoveDirectionLocal()
    {
        Vector3 direction = transform.right * moveInput.x + transform.forward * moveInput.z;
        direction.y = 0f;
        return direction;
    }

    void HandleStamina()
    {
        // 달리기 아닐 때만 회복
        if (!isRunning)
        {
            currentStamina += staminaRegen * Time.deltaTime;
            currentStamina = Mathf.Min(staminaMax, currentStamina);
        }
    }

    void HandleMouseLook()
    {
        // Settings 감도값(0.2~3.0)을 곱해서 최종 감도 구성
        float sens = SettingsData.MouseSensitivity * mouseSensitivity;

        float mx = Input.GetAxis("Mouse X") * sens;
        float my = Input.GetAxis("Mouse Y") * sens;

        // 좌우(Yaw) 누적
        yaw += mx;

        // 상하(Pitch) 누적 (FPS는 보통 마우스 위=시선 위라서 -my)
        pitch -= my;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        // 좌우 회전은 Rigidbody로 적용하기 위해 캐싱
        cachedYawRotation = Quaternion.Euler(0f, yaw, 0f);
        hasCachedYawRotation = true;

        // 상하 회전은 카메라 피벗 로컬 회전
        if (cameraPivot != null)
        {
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    void ApplyCachedYawRotation()
    {
        if (!hasCachedYawRotation) return;

        Quaternion newRot = Quaternion.Slerp(rb.rotation, cachedYawRotation, rotationSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(newRot);
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
        rb.position = pos;

        // Y 속도도 제거
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;
    }

    float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    // 외부 UI 등에서 스테미나 관련 용도
    public float GetStamina() => currentStamina;
    public float GetStaminaMax() => staminaMax;
}
