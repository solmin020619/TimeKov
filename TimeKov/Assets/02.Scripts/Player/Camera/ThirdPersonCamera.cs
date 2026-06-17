using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Follow Target")]
    public Transform FollowTarget;
    public Vector3 FollowOffset = new Vector3(0, 1.5f, 0);
    [Tooltip("카메라가 캐릭터를 따라가는 지연(초). 0=즉시(딱 붙음), 0.1~0.2=엔드필드처럼 살짝 뒤따라옴.")]
    public float FollowSmoothTime = 0.12f;
    [Tooltip("세로(Y) 추적 지연(초). 가로보다 작게 두면 점프/경사에서 둥실거림이 줄어듦. 0이면 Y는 즉시.")]
    public float FollowVerticalSmoothTime = 0.05f;
    [Tooltip("이 거리(m) 이상 벌어지면(리스폰/탑뷰 복귀 등 순간이동) 보간 없이 즉시 스냅. 천천히 슬라이드되는 현상 방지. 대쉬에서 스냅 튀면 더 올릴 것.")]
    public float FollowSnapDistance = 8f;

    [Header("Look-Ahead (이동 방향 미리보기)")]
    [Tooltip("이동 방향으로 카메라가 미리 빠지는 최대 거리(m). 0=끔(기본). 켜면 카메라가 캐릭터가 아닌 이동방향을 따라가는 느낌이 됨 - QA 피드백으로 기본 끔.")]
    public float LookAheadDistance = 0f;
    [Tooltip("look-ahead가 빠지고 돌아오는 부드러움(초). 클수록 천천히 빠지고 천천히 복귀.")]
    public float LookAheadSmoothTime = 0.25f;

    [Header("Rotation")]
    public float SensitivityX = 3f;
    public float SensitivityY = 2f;
    public float MinPitchAngle = -20f;
    public float MaxPitchAngle = 60f;
    [Tooltip("시점 회전에 살짝 쿠션을 줌(초). 0=마우스에 즉시 1:1(기존). 0.03~0.06=부드럽게. 너무 키우면 조준이 끈적해짐.")]
    public float RotationSmoothTime = 0.04f;

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
    private float _yaw;                    // 마우스 입력 누적 (회전 목표)
    private float _pitch;
    private float _yawDisplay;             // 실제 적용되는 yaw (_yaw 를 부드럽게 따라감)
    private float _pitchDisplay;
    private float _yawVel;                 // SmoothDampAngle 내부 속도
    private float _pitchVel;
    private float _currentDist;
    private float _targetDist;
    private float _sensitivityMult = 1f;  // 설정창 감도 슬라이더 배율
    private Vector3 _followVelocity;       // SmoothDamp 내부 속도 (follow 지연용)
    private Vector3 _lastTargetPos;        // look-ahead 속도 계산용 직전 타겟 위치
    private bool _hasLastTargetPos;
    private Vector3 _lookAheadOffset;      // 현재 적용 중인 look-ahead 오프셋 (부드럽게 변함)
    private Vector3 _lookAheadVel;         // 위 오프셋 SmoothDamp 속도
    private const float LOOKAHEAD_FULL_SPEED = 6f;  // 이 속도(m/s)에서 look-ahead 최대치 도달

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
        // 일시정지(영상 팝업/설정 등 timeScale=0) 가드.
        // deltaTime=0 에서 Mathf.SmoothDamp 는 current==target 일 때 내부 속도를 0/0=NaN 으로 만든다
        // (Unity 내부 overshoot 보정이 /deltaTime 를 함). 그 NaN 속도가 정지 해제 후 카메라 Y 를
        // 영구 NaN 으로 오염시켜 'transform.position ... NaN' 에러가 매 프레임 떴다.
        // 정지 중엔 따라갈 것도 없으니 보간 자체를 생략.
        if (Time.deltaTime <= 0f) return;

        Vector3 cur = transform.position;
        Vector3 desired = FollowTarget.position + FollowOffset;

        // 순간이동(리스폰/탑뷰 복귀 등) 감지: 너무 멀어지면 보간 없이 즉시 스냅. SmoothTime 0이면 즉시.
        if (FollowSmoothTime <= 0f ||
            (desired - cur).sqrMagnitude > FollowSnapDistance * FollowSnapDistance)
        {
            transform.position = desired;
            _followVelocity = Vector3.zero;
            _lookAheadOffset = Vector3.zero;
            _lookAheadVel = Vector3.zero;
            _lastTargetPos = FollowTarget.position;
            _hasLastTargetPos = true;
            return;
        }

        // [Look-Ahead] 이동 방향으로 목표를 살짝 당겨 진행 방향 시야를 미리 확보.
        // FollowTarget 위치 변화로 속도를 구하고, 오프셋 자체를 SmoothDamp 해서 노이즈/급변 제거.
        if (LookAheadDistance > 0.0001f && _hasLastTargetPos && Time.deltaTime > 0f)
        {
            Vector3 flatVel = (FollowTarget.position - _lastTargetPos) / Time.deltaTime;
            flatVel.y = 0f;
            float speed = flatVel.magnitude;
            Vector3 targetLA = speed > 0.1f
                ? (flatVel / speed) * LookAheadDistance * Mathf.Clamp01(speed / LOOKAHEAD_FULL_SPEED)
                : Vector3.zero;
            _lookAheadOffset = Vector3.SmoothDamp(_lookAheadOffset, targetLA, ref _lookAheadVel, LookAheadSmoothTime);
        }
        else
        {
            // look-ahead 꺼짐/첫 프레임: 남은 오프셋을 0으로 부드럽게 복귀.
            _lookAheadOffset = Vector3.SmoothDamp(_lookAheadOffset, Vector3.zero, ref _lookAheadVel, LookAheadSmoothTime);
        }
        desired += _lookAheadOffset;

        // [Follow lag] 가로(XZ)는 지연시켜 뒤따라오게, 세로(Y)는 더 빠르게 -> 점프/경사 둥실거림 방지.
        // LateUpdate 보간이라 Rigidbody Interpolate(렌더 위치) 기준 -> 떨림 없이 부드러움.
        float nx = Mathf.SmoothDamp(cur.x, desired.x, ref _followVelocity.x, FollowSmoothTime);
        float nz = Mathf.SmoothDamp(cur.z, desired.z, ref _followVelocity.z, FollowSmoothTime);
        float ny = Mathf.SmoothDamp(cur.y, desired.y, ref _followVelocity.y, FollowVerticalSmoothTime);
        Vector3 next = new Vector3(nx, ny, nz);

        // 방어: 속도 오염/타겟 위치 비정상 등으로 NaN/Inf 가 생기면 즉시 스냅+속도 리셋해 자가복구.
        // (한 번 NaN 이 되면 cur 로 되먹임돼 매 프레임 NaN 이 유지되므로 끊어줘야 함.)
        if (float.IsNaN(next.x) || float.IsNaN(next.y) || float.IsNaN(next.z) ||
            float.IsInfinity(next.x) || float.IsInfinity(next.y) || float.IsInfinity(next.z))
        {
            next = FollowTarget.position + FollowOffset;
            _followVelocity = Vector3.zero;
            _lookAheadOffset = Vector3.zero;
            _lookAheadVel = Vector3.zero;
        }
        transform.position = next;

        _lastTargetPos = FollowTarget.position;
        _hasLastTargetPos = true;
    }

    void HandleRotation()
    {
        // IsUIOpen 또는 GameUIController 기준 둘 다 차단
        if (IsUIOpen || !GameUIController.GameplayInputEnabled) return;

        // 마우스 입력에 감도 배율 적용 (목표 각도 누적)
        _yaw += Input.GetAxis("Mouse X") * SensitivityX * _sensitivityMult;
        _pitch -= Input.GetAxis("Mouse Y") * SensitivityY * _sensitivityMult;
        _pitch = Mathf.Clamp(_pitch, MinPitchAngle, MaxPitchAngle);

        // 목표 각도(_yaw/_pitch)를 display 각도가 부드럽게 따라감 -> 마우스 휙 돌릴 때 쿠션.
        // 0이면 즉시 1:1 (기존 동작).
        if (RotationSmoothTime <= 0f)
        {
            _yawDisplay = _yaw;
            _pitchDisplay = _pitch;
        }
        else
        {
            _yawDisplay   = Mathf.SmoothDampAngle(_yawDisplay, _yaw, ref _yawVel, RotationSmoothTime);
            _pitchDisplay = Mathf.SmoothDampAngle(_pitchDisplay, _pitch, ref _pitchVel, RotationSmoothTime);
        }

        transform.rotation = Quaternion.Euler(0, _yawDisplay, 0);
        _pivot.localRotation = Quaternion.Euler(_pitchDisplay, 0, 0);
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

    // 이동 방향 기준축은 "보이는 카메라 방향"(display yaw)으로 맞춤 -> 시점과 이동이 한 덩어리로 움직임.
    public Quaternion GetYawRotation() => Quaternion.Euler(0, _yawDisplay, 0);

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