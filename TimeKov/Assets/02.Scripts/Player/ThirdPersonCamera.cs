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

    [Header("Collision")]
    public float CollisionRadius = 0.3f;
    public LayerMask CollisionMask;

    private Transform _pivot;
    private float _yaw;
    private float _pitch;
    private float _currentDist;
    private float _targetDist;

    void Awake()
    {
        _pivot = transform.GetChild(0);
        _currentDist = DefaultDistance;
        _targetDist = DefaultDistance;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (FollowTarget == null) return;

        HandleFollow();
        HandleRotation();
        HandleZoom();
        HandleCollision();
    }

    void HandleFollow()
    {
        // 위치는 바로 따라감 -> Rigidbody 튀는 문제 방지
        transform.position = FollowTarget.position + FollowOffset;
    }

    void HandleRotation()
    {
        // 마우스 입력 그대로 누적 -> Slerp 없음
        _yaw += Input.GetAxis("Mouse X") * SensitivityX;
        _pitch -= Input.GetAxis("Mouse Y") * SensitivityY;
        _pitch = Mathf.Clamp(_pitch, MinPitchAngle, MaxPitchAngle);

        transform.rotation = Quaternion.Euler(0, _yaw, 0);
        _pivot.localRotation = Quaternion.Euler(_pitch, 0, 0);
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        _targetDist = Mathf.Clamp(
            _targetDist - scroll * ScrollSpeed,
            MinDistance, MaxDistance
        );
    }

    void HandleCollision()
    {
        Vector3 pivotPos = _pivot.position;
        Vector3 camDir = _pivot.forward * -1f;
        float desiredDist = _targetDist;

        if (Physics.SphereCast(
            pivotPos, CollisionRadius,
            camDir, out RaycastHit hit,
            _targetDist, CollisionMask))
        {
            desiredDist = Mathf.Clamp(hit.distance, MinDistance, _targetDist);
        }

        _currentDist = Mathf.Lerp(
            _currentDist, desiredDist, DistanceSmoothSpeed * Time.deltaTime);

        Camera.main.transform.localPosition = new Vector3(0, 0, -_currentDist);
    }

    public Quaternion GetYawRotation() => Quaternion.Euler(0, _yaw, 0);
}