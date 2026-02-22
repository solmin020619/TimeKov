using UnityEngine;
using KINEMATION.FPSAnimationPack.Scripts.Player; // FPSPlayer

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    public string playerName = "";

    [Header("Time")]
    public int baseMaxTime;
    public int timeDecay;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float runSpeed = 8f;
    public float rotationSpeed = 10f;

    [Header("Stamina")]
    public float staminaMax = 100f;
    public float staminaRegen = 5f;
    public float runSpeedCost = 10f;

    [Header("Combat")]
    public int baseDefense;
    public int baseAttack;

    [Header("Y Lock (No Jump)")]
    public float fixedY = 0f;

    [Header("FPS Look")]
    public Transform cameraPivot; // Pitch
    public float mouseSensitivity = 2.0f;
    public float pitchMin = -80f;
    public float pitchMax = 80f;
    public bool lockCursor = true;

    [Header("ViewModel (KINEMATION)")]
    [Tooltip("SK_Arms_Mono 안에 붙어있는 FPSPlayer 컴포넌트를 드래그해서 넣어")]
    public FPSPlayer viewModel;

    // 내부
    private Vector3 moveInput;
    public float currentStamina;
    private bool isRunning;

    private float yaw;
    private float pitch;

    private Quaternion cachedYawRotation;
    private bool hasCachedYawRotation;

    private Rigidbody rb;
    private PlayerTime playerTime;

    // 애니메이션(외부)에서 읽기
    public Vector3 MoveInput => moveInput;
    public bool IsRunning => isRunning;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.freezeRotation = true;

        currentStamina = staminaMax;

        // Time 시스템 연결
        playerTime = GetComponent<PlayerTime>();
        if (playerTime != null)
        {
            playerTime.baseMaxTime = baseMaxTime;
            playerTime.timeDecay = timeDecay;
            playerTime.isInRaid = true;
            playerTime.onTimeDepleted += OnPlayerDeath;
        }

        // 시작 위치 Y 고정
        Vector3 p = rb.position;
        p.y = fixedY;
        rb.position = p;

        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        // cameraPivot 자동 탐색
        if (cameraPivot == null && Camera.main != null)
        {
            cameraPivot = Camera.main.transform.parent != null ? Camera.main.transform.parent : Camera.main.transform;
        }

        yaw = transform.eulerAngles.y;
        pitch = (cameraPivot != null) ? cameraPivot.localEulerAngles.x : 0f;
        pitch = NormalizeAngle(pitch);

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        HandleInput();
        HandleStamina();
        HandleMouseLook();
        PushToViewModel(); // ⭐ FPSPlayer에 입력 주입
    }

    private void FixedUpdate()
    {
        MoveRigidbody();
        ApplyCachedYawRotation();
        LockYPosition();
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
        Vector3 moveDir = GetMoveDirectionLocal();

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

        bool runKey = Input.GetKey(KeyCode.LeftShift);
        bool canRun = currentStamina >= runSpeedCost;
        bool wantRun = runKey && canRun;

        float speed = wantRun ? runSpeed : moveSpeed;

        isRunning = false;
        if (wantRun)
        {
            isRunning = true;
            currentStamina -= runSpeedCost * Time.fixedDeltaTime;
            currentStamina = Mathf.Max(0f, currentStamina);
        }

        Vector3 desired = moveDir.normalized * speed;
        Vector3 finalVelocity = rb.linearVelocity;
        finalVelocity.x = desired.x;
        finalVelocity.y = 0f;
        finalVelocity.z = desired.z;
        rb.linearVelocity = finalVelocity;
    }

    Vector3 GetMoveDirectionLocal()
    {
        Vector3 direction = transform.right * moveInput.x + transform.forward * moveInput.z;
        direction.y = 0f;
        return direction;
    }

    void HandleStamina()
    {
        if (!isRunning)
        {
            currentStamina += staminaRegen * Time.deltaTime;
            currentStamina = Mathf.Min(staminaMax, currentStamina);
        }
    }

    void HandleMouseLook()
    {
        // 네 기존 감도 로직 유지
        float sens = mouseSensitivity;

        float mx = Input.GetAxis("Mouse X") * sens;
        float my = Input.GetAxis("Mouse Y") * sens;

        yaw += mx;

        pitch -= my;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        cachedYawRotation = Quaternion.Euler(0f, yaw, 0f);
        hasCachedYawRotation = true;

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

    void PushToViewModel()
    {
        if (viewModel == null) return;

        // 1) 이동 입력 -> GAIT
        // FPSPlayer는 x=strafe, y=forward로 Vector2 받음
        Vector2 move01 = new Vector2(moveInput.x, moveInput.z);
        viewModel.SetMoveInput(move01, isRunning, false);

        // 2) 피치 입력 주입 (팔이 위아래 따라가게)
        // FPSPlayer.AddLookPitchDelta()는 내부에서 playerSettings.sensitivity를 곱함
        // 우리는 "이미 스케일된 my"를 쓰고 있으니, 감도 중복을 피하려고 나눠서 넣음
        float sens = mouseSensitivity;
        float myScaled = Input.GetAxis("Mouse Y") * sens;

        float vmSens = 1f;
        if (viewModel.playerSettings != null)
            vmSens = Mathf.Max(0.0001f, viewModel.playerSettings.sensitivity);

        viewModel.AddLookPitchDelta(myScaled / vmSens);

        // 3) ADS(조준) - 우클릭 기준 (원하면 나중에 네 입력 규칙으로 바꾸면 됨)
        bool aiming = Input.GetMouseButton(1);
        viewModel.SetAiming(aiming);
    }

    void OnPlayerDeath()
    {
        Debug.Log("사망");
        this.enabled = false;
        rb.linearVelocity = Vector3.zero;
    }

    void LockYPosition()
    {
        Vector3 pos = rb.position;
        pos.y = fixedY;
        rb.position = pos;

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

    public float GetStamina() => currentStamina;
    public float GetStaminaMax() => staminaMax;
}
