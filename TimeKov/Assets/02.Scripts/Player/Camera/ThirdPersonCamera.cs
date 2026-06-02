using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Follow Target")]
    public Transform FollowTarget;
    public Vector3 FollowOffset = new Vector3(0, 1.5f, 0);

    [Header("Rotation")]
    public float SensitivityX = 3f;
    public float SensitivityY = 2f;
    public float MinPitchAngle = -20f;
    public float MaxPitchAngle = 60f;

    [Header("Distance")]
    public float DefaultDistance = 5f;
    public float MinDistance = 1f;
    public float MaxDistance = 8f;
    public float ScrollSpeed = 2f;
    public float DistanceSmoothSpeed = 8f;

    [Header("Collision (오토 줌인/아웃)")]
    public float CollisionRadius = 0.3f;
    public LayerMask CollisionMask;
    [Tooltip("벽에 닿았을 때 벽보다 이만큼 앞에 카메라를 둠 (m). 벽 뚫림/클리핑 방지 여유.")]
    public float CollisionBuffer = 0.2f;
    [Tooltip("벽에 가까워질 때(줌인) 당기는 속도. 빠르게 해야 벽을 안 뚫음.")]
    public float ZoomInSpeed = 20f;
    [Tooltip("벽에서 멀어질 때(줌아웃) 되돌아가는 속도. 느리게 해야 출렁임이 적음.")]
    public float ZoomOutSpeed = 6f;

    private Transform _pivot;
    private float _yaw;
    private float _pitch;
    private float _currentDist;
    private float _targetDist;
    private float _sensitivityMult = 1f;  // 설정창 감도 슬라이더 배율

    public static bool IsUIOpen = false;
    public static bool BlockZoom = false;

    // ── Camera Shake ──────────────────────────────────────────────────
    public static ThirdPersonCamera Instance { get; private set; }

    private float _shakeTimer;
    private float _shakeDuration;
    private float _shakeMagnitude;

    void Awake()
    {
        Instance = this;

        // CollisionMask 가 비어있으면(인스펙터 미설정) 안전 폴백: Default/Ground/Building/BuildPort.
        // 카메라가 벽을 뚫고 들어가는 것만 막는 보조용. 투명화(WallOcclusionFader)가 시야는 담당.
        if (CollisionMask.value == 0)
            CollisionMask = LayerMask.GetMask("Default", "Ground", "Building", "BuildPort");

        _pivot = transform.GetChild(0);
        _currentDist = DefaultDistance;
        _targetDist = DefaultDistance;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Start()
    {
        // 저장된 감도 값 불러오기
        _sensitivityMult = PlayerPrefs.GetFloat("MouseSensitivity", 1f);
        // 이후 설정창에서 변경 시 실시간 반영
        GlobalSettingsManager.OnSensitivityChanged += ApplySensitivity;
    }

    void OnDestroy()
    {
        GlobalSettingsManager.OnSensitivityChanged -= ApplySensitivity;
    }

    void ApplySensitivity(float mult)
    {
        _sensitivityMult = mult;
    }

    void LateUpdate()
    {
        if (FollowTarget == null) return;

        HandleFollow();
        HandleRotation();
        HandleZoom();
        HandleCollision();
        ApplyShake();   // HandleCollision 이후: distance 설정 후 셰이크 오프셋 추가
    }

    void HandleFollow()
    {
        // ��ġ�� �ٷ� ���� -> Rigidbody Ƣ�� ���� ����
        transform.position = FollowTarget.position + FollowOffset;
    }

    void HandleRotation()
    {
        // IsUIOpen 또는 GameUIController 기준 둘 다 차단
        if (IsUIOpen || !GameUIController.GameplayInputEnabled) return;

        // 마우스 입력에 감도 배율 적용
        _yaw += Input.GetAxis("Mouse X") * SensitivityX * _sensitivityMult;
        _pitch -= Input.GetAxis("Mouse Y") * SensitivityY * _sensitivityMult;
        _pitch = Mathf.Clamp(_pitch, MinPitchAngle, MaxPitchAngle);

        transform.rotation = Quaternion.Euler(0, _yaw, 0);
        _pivot.localRotation = Quaternion.Euler(_pitch, 0, 0);
    }

    void HandleZoom()
    {
        // UI가 열려있는 동안 줌 차단
        if (IsUIOpen || BlockZoom || !GameUIController.GameplayInputEnabled) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        _targetDist = Mathf.Clamp(
            _targetDist - scroll * ScrollSpeed,
            MinDistance, MaxDistance
        );
    }

    void HandleCollision()
    {
        // [오토 줌인/아웃] 카메라~플레이어(pivot) 사이에 벽이 있으면 카메라를 앞으로 당기고(줌인),
        // 벗어나면 원래 거리로 되돌린다(줌아웃). 투명화(WallOcclusionFader) 대신 이 방식 사용.
        Vector3 pivotPos = _pivot.position;
        Vector3 camDir = _pivot.forward * -1f;
        float desiredDist = _targetDist;

        if (Physics.SphereCast(
            pivotPos, CollisionRadius,
            camDir, out RaycastHit hit,
            _targetDist, CollisionMask, QueryTriggerInteraction.Ignore))
        {
            // 벽보다 CollisionBuffer 만큼 앞에 멈춰 클리핑 방지
            desiredDist = Mathf.Clamp(hit.distance - CollisionBuffer, MinDistance, _targetDist);
        }

        // 줌인(가까워짐)은 빠르게, 줌아웃(멀어짐)은 느리게 — 비대칭 속도로 자연스럽게
        float speed = desiredDist < _currentDist ? ZoomInSpeed : ZoomOutSpeed;
        _currentDist = Mathf.Lerp(_currentDist, desiredDist, speed * Time.deltaTime);

        Camera.main.transform.localPosition = new Vector3(0, 0, -_currentDist);
    }

    public Quaternion GetYawRotation() => Quaternion.Euler(0, _yaw, 0);

    // ── Camera Shake API ─────────────────────────────────────────────
    /// <summary>
    /// 카메라 셰이크 요청.
    /// 현재 진행 중인 셰이크보다 강도가 크거나 남은 시간이 길 때만 덮어씀.
    /// (여러 히트가 동시에 들어와도 가장 강한 셰이크가 우선)
    /// </summary>
    public static void Shake(float duration, float magnitude)
    {
        if (Instance == null) return;
        // 더 강하거나 더 긴 셰이크로만 갱신 (약한 히트가 강한 셰이크를 방해하지 않도록)
        if (magnitude >= Instance._shakeMagnitude || duration > Instance._shakeTimer)
        {
            Instance._shakeDuration  = duration;
            Instance._shakeMagnitude = magnitude;
            Instance._shakeTimer     = duration;
        }
    }

    void ApplyShake()
    {
        if (_shakeTimer <= 0f) return;

        _shakeTimer -= Time.deltaTime;

        // 시간에 따라 선형 감쇠 (시작이 강하고 끝으로 갈수록 약해짐)
        float t   = Mathf.Clamp01(_shakeTimer / Mathf.Max(0.001f, _shakeDuration));
        float mag = _shakeMagnitude * t;

        // Camera.main localPosition에 X/Y 랜덤 오프셋 추가
        // HandleCollision이 Z를 이미 설정했으므로 XY만 건드림
        var camPos = Camera.main.transform.localPosition;
        camPos.x += Random.Range(-mag, mag);
        camPos.y += Random.Range(-mag, mag);
        Camera.main.transform.localPosition = camPos;

        if (_shakeTimer <= 0f)
        {
            _shakeTimer     = 0f;
            _shakeMagnitude = 0f;
        }
    }
}