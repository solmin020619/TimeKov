using UnityEngine;
using UnityEngine.Audio;
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

    [Header("Stamina Run Gating")]
    [Tooltip("스태미나가 0이 되면 탈진 상태로 진입")]
    public float exhaustedEnterStamina = 0.01f;

    [Tooltip("탈진 상태 해제(= 다시 달리기 허용) 스태미나 임계치. Max의 비율")]
    [Range(0f, 1f)]
    public float resumeRunThreshold01 = 0.25f; // 예: 25% 차야 다시 달리기 가능

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

    [Header("Audio Settings")]
    public AudioMixerGroup sfxMixerGroup;

    [Header("ViewModel Pitch Sync")]
    [Tooltip("손(뷰모델) 피치가 카메라 피치를 따라오도록 주입")]
    public bool syncViewModelPitch = true;

    [Tooltip("손이 반대로 움직이면 체크(부호 반전)")]
    public bool invertViewModelPitch = false;

    // 내부
    private Vector3 moveInput;
    public float currentStamina;

    private bool isRunning;

    // 탈진 상태(이 상태면 Shift 눌러도 달리기 금지)
    private bool isExhausted;

    private float yaw;
    private float pitch;

    // cameraPivot 기반 델타 계산용
    private float lastPivotPitch;

    private Quaternion cachedYawRotation;
    private bool hasCachedYawRotation;

    private Rigidbody rb;
    private PlayerTime playerTime;

    // 애니메이션(외부)에서 읽기
    public Vector3 MoveInput => moveInput;
    public bool IsRunning => isRunning;

    // UI 열렸을 때 입력 차단용 캐시
    private bool wasGameplayEnabled = true;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.freezeRotation = true;

        currentStamina = staminaMax;
        isExhausted = false;

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

        // pitch 초기값 세팅
        pitch = (cameraPivot != null) ? cameraPivot.localEulerAngles.x : 0f;
        pitch = NormalizeAngle(pitch);
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // pivot 기반 동기화 초기값
        if (cameraPivot != null)
            lastPivotPitch = NormalizeAngle(cameraPivot.localEulerAngles.x);
        else
            lastPivotPitch = pitch;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        SyncSettings();
    }

    void Update()
    {
        // UI가 열려있으면: 이동/시점/조준 입력 전부 차단
        // (Time.timeScale은 건드리지 않아서 시간은 계속 줄어듦)
        bool gameplayEnabled = UIStateManager.GameplayInputEnabled;

        if (!gameplayEnabled)
        {
            // UI 열리는 순간에만 1회 리셋(불필요한 연산 줄임)
            if (wasGameplayEnabled)
            {
                wasGameplayEnabled = false;

                moveInput = Vector3.zero;
                isRunning = false;

                // 뷰모델도 "정지 상태"로 보내서 손/가동 입력 멈춤
                if (viewModel != null)
                {
                    viewModel.SetMoveInput(Vector2.zero, false, false);
                    viewModel.SetAiming(false);
                }
            }

            return; //  여기서 Update 입력 처리 중단
        }

        // UI 닫혔을 때 플래그 복구
        if (!wasGameplayEnabled)
            wasGameplayEnabled = true;

        HandleInput();
        HandleMouseLook();
        PushToViewModel(); // FPSPlayer에 입력 주입
    }

    private void FixedUpdate()
    {
        // UI 열려있으면: 물리 이동도 멈춰서 미끄러짐/잔진동 방지
        if (!UIStateManager.GameplayInputEnabled)
        {
            Vector3 vel = rb.linearVelocity;
            vel.x = 0f;
            vel.z = 0f;
            vel.y = 0f;
            rb.linearVelocity = vel;

            LockYPosition();
            return;
        }

        MoveRigidbody();
        HandleStaminaFixed();     // 스태미나 처리는 FixedUpdate 기준으로 통일(깜빡임 감소)
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

        // 탈진/재달리기 로직 반영한 canRun 계산
        bool wantRun = runKey && CanRunNow();

        float speed = wantRun ? runSpeed : moveSpeed;

        isRunning = wantRun;

        // 스태미나 소모는 여기서만 (FixedUpdate)
        if (isRunning)
        {
            currentStamina -= runSpeedCost * Time.fixedDeltaTime;
            if (currentStamina < 0f) currentStamina = 0f;
        }

        Vector3 desired = moveDir.normalized * speed;
        Vector3 finalVelocity = rb.linearVelocity;
        finalVelocity.x = desired.x;
        finalVelocity.y = 0f;
        finalVelocity.z = desired.z;
        rb.linearVelocity = finalVelocity;
    }

    // "달리기 가능 여부"는 탈진 상태를 포함해서 판단
    private bool CanRunNow()
    {
        // 이미 탈진이면 일정 이상 찰 때까지 달리기 금지
        if (isExhausted) return false;

        // 스태미나가 거의 없으면 달리기 금지
        return currentStamina > exhaustedEnterStamina;
    }

    Vector3 GetMoveDirectionLocal()
    {
        Vector3 direction = transform.right * moveInput.x + transform.forward * moveInput.z;
        direction.y = 0f;
        return direction;
    }

    // FixedUpdate에서 스태미나 회복 + 탈진 상태 갱신
    void HandleStaminaFixed()
    {
        // 1) 탈진 진입
        if (!isExhausted && currentStamina <= exhaustedEnterStamina)
        {
            isExhausted = true;
        }

        // 2) 회복(달리지 않을 때만)
        if (!isRunning)
        {
            currentStamina += staminaRegen * Time.fixedDeltaTime;
            currentStamina = Mathf.Min(staminaMax, currentStamina);
        }

        // 3) 탈진 해제(= 다시 달리기 허용)
        if (isExhausted)
        {
            float resumeThreshold = staminaMax * Mathf.Clamp01(resumeRunThreshold01);
            if (currentStamina >= resumeThreshold)
            {
                isExhausted = false;
            }
        }
    }

    void HandleMouseLook()
    {
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
        Vector2 move01 = new Vector2(moveInput.x, moveInput.z);
        viewModel.SetMoveInput(move01, isRunning, false);

        // 2) 뷰모델 피치 동기화: cameraPivot 변화량 기반(안정)
        if (syncViewModelPitch && cameraPivot != null)
        {
            float vmSens = 1f;
            if (viewModel.playerSettings != null)
                vmSens = Mathf.Max(0.0001f, viewModel.playerSettings.sensitivity);

            float pivotPitch = NormalizeAngle(cameraPivot.localEulerAngles.x);

            float deltaPitch = Mathf.DeltaAngle(lastPivotPitch, pivotPitch);
            lastPivotPitch = pivotPitch;

            if (invertViewModelPitch) deltaPitch = -deltaPitch;

            viewModel.AddLookPitchDelta(deltaPitch / vmSens);
        }

        // 3) ADS(조준)
        bool aiming = Input.GetMouseButton(1);
        viewModel.SetAiming(aiming);
    }

    void OnPlayerDeath()
    {
        Debug.Log("사망");
        this.enabled = false;
        rb.linearVelocity = Vector3.zero;
    }

    void LockYPosition() //3.11 캐릭터 떨림 오류 주석 처리로 임시 해결
    {
        //Vector3 pos = rb.position;
        //pos.y = fixedY;
        //rb.position = pos;

        //Vector3 v = rb.linearVelocity;
        //v.y = 0f;
        //rb.linearVelocity = v;
    }

    float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    public void SyncSettings()
    {
        if (PlayerPrefs.HasKey("MouseSensitivity"))
        {
            mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 2.0f);
        }

        if (PlayerPrefs.HasKey("SFXVolume") && sfxMixerGroup != null)
        {
            float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 1f);
            float decibel = sfxVol > 0.001f ? Mathf.Log10(sfxVol) * 20f : -80f;

            sfxMixerGroup.audioMixer.SetFloat("SFXVolume", decibel);

            AudioSource[] allAudioSources = GetComponentsInChildren<AudioSource>(true);
            foreach (AudioSource audio in allAudioSources)
            {
                audio.outputAudioMixerGroup = sfxMixerGroup;
            }
        }
    }

    public void ResetInputState()
    {
        moveInput = Vector3.zero;
        isRunning = false;

        if (viewModel != null)
        {
            viewModel.SetMoveInput(Vector2.zero, false, false);
            viewModel.SetAiming(false);
        }
    }

    public void StopImmediately()
    {
        moveInput = Vector3.zero;
        isRunning = false;
        hasCachedYawRotation = false;

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            Vector3 v = rb.linearVelocity;
            v.x = 0f;
            v.y = 0f;
            v.z = 0f;
            rb.linearVelocity = v;

            rb.angularVelocity = Vector3.zero;
        }

        if (viewModel != null)
        {
            viewModel.SetMoveInput(Vector2.zero, false, false);
            viewModel.SetAiming(false);
        }
    }

    public float GetStamina() => currentStamina;
    public float GetStaminaMax() => staminaMax;

    // 디버그/튜닝용
    public bool IsExhausted() => isExhausted;
}