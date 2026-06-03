using UnityEngine;

public class TopViewPanCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;

    [Header("Mouse Drag (Left Click)")]
    [SerializeField] private float dragSensitivity = 1f;
    [SerializeField] private bool invertDrag = false;

    [Header("Keyboard Move (WASD)")]
    [SerializeField] private float keyboardMoveSpeed = 20f;

    [Header("Zoom")]
    [SerializeField] private bool allowZoom = true;
    [SerializeField] private float zoomSpeed = 8f;
    [SerializeField] private float minOrthoSize = 8f;
    [SerializeField] private float maxOrthoSize = 40f;
    [SerializeField] private float minHeight = 10f;
    [SerializeField] private float maxHeight = 60f;

    [Header("Bounds")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 xBounds = new Vector2(-100f, 100f);
    [SerializeField] private Vector2 zBounds = new Vector2(-100f, 100f);

    [Header("Smoothing")]
    [Tooltip("SmoothDamp 시간(초). 작을수록 즉각 반응, 클수록 부드러움. 0이면 즉시 이동.")]
    [SerializeField] private float smoothTime = 0.05f;

    private bool isEnabledControl;
    private bool isMouseDragEnabled = true;
    private Plane dragPlane;
    private Vector3 _leftDragLastWorld;
    private Vector3 _middleDragLastWorld;
    private bool _leftDragActive;
    private bool _middleDragActive;

    private Vector3 _targetPosition;
    private Vector3 _velocity;

    private void Awake()
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        dragPlane = new Plane(Vector3.up, Vector3.zero);
        _targetPosition = transform.position;
    }

    private void OnEnable()
    {
        // 외부에서 transform.position을 직접 바꿔놓고 활성화한 경우 동기화
        _targetPosition = transform.position;
        _velocity = Vector3.zero;
    }

    public void SetControlEnabled(bool value)
    {
        isEnabledControl = value;
    }

    public void SetMouseDragEnabled(bool value)
    {
        // 비활성화되는 순간 진행 중이던 좌드래그 추적을 끊는다.
        // 안 끊으면 재활성화 첫 프레임에 오래된 기준점과의 큰 델타로 카메라가 순간이동함.
        if (!value)
            _leftDragActive = false;

        isMouseDragEnabled = value;
    }

    /// <summary>외부에서 카메라 위치를 점프시킨 직후 호출. 보간 잔여 속도 제거.</summary>
    public void SnapToCurrent()
    {
        _targetPosition = transform.position;
        _velocity = Vector3.zero;
    }

    /// <summary>목표 위치 직접 설정. snap=true면 즉시 점프, false면 SmoothDamp 보간.</summary>
    public void SetTargetPosition(Vector3 pos, bool snap = false)
    {
        _targetPosition = pos;
        if (snap)
        {
            transform.position = pos;
            _velocity = Vector3.zero;
        }
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        if (isEnabledControl)
        {
            HandleKeyboardMove();
            HandleDrag();
            HandleZoom();
            ClampTargetPosition();
        }

        if (smoothTime > 0f)
            transform.position = Vector3.SmoothDamp(transform.position, _targetPosition, ref _velocity, smoothTime);
        else
            transform.position = _targetPosition;
    }

    private void HandleKeyboardMove()
    {
        float h = 0f;
        float v = 0f;
        if (Input.GetKey(KeyCode.W)) v += 1f;
        if (Input.GetKey(KeyCode.S)) v -= 1f;
        if (Input.GetKey(KeyCode.A)) h -= 1f;
        if (Input.GetKey(KeyCode.D)) h += 1f;
        if (h == 0f && v == 0f) return;

        Vector3 delta = new Vector3(h, 0f, v);
        if (delta.sqrMagnitude > 1f) delta.Normalize();

        _targetPosition += delta * keyboardMoveSpeed * Time.deltaTime;
    }

    private void HandleDrag()
    {
        if (isMouseDragEnabled)
            ProcessMouseDrag(0, ref _leftDragLastWorld, ref _leftDragActive);

        ProcessMouseDrag(2, ref _middleDragLastWorld, ref _middleDragActive);
    }

    private void ProcessMouseDrag(int mouseButton, ref Vector3 lastWorld, ref bool dragActive)
    {
        if (Input.GetMouseButtonDown(mouseButton))
        {
            if (TryGetMouseWorldPoint(out Vector3 worldPoint))
            {
                lastWorld = worldPoint;
                dragActive = true;
            }
            return;
        }

        if (Input.GetMouseButtonUp(mouseButton))
        {
            dragActive = false;
            return;
        }

        // 버튼을 "누른 채로" 드래그가 켜진 경우(dragActive=false)는 무시한다.
        // GetMouseButtonDown 부터 추적해야 기준점이 현재 마우스로 잡혀 순간이동이 안 생김.
        if (dragActive && Input.GetMouseButton(mouseButton))
        {
            if (!TryGetMouseWorldPoint(out Vector3 currentWorld))
                return;

            Vector3 delta = lastWorld - currentWorld;

            if (invertDrag)
                delta = -delta;

            delta.y = 0f;
            _targetPosition += delta * dragSensitivity;

            // 다음 frame ScreenPointToRay 에서 자연스럽게 수렴하도록 현재 지점을 저장.
            lastWorld = currentWorld;
        }
    }

    private void HandleZoom()
    {
        if (!allowZoom)
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f)
            return;

        if (cam.orthographic)
        {
            cam.orthographicSize -= scroll * zoomSpeed * Time.deltaTime * 10f;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minOrthoSize, maxOrthoSize);
        }
        else
        {
            Vector3 pos = _targetPosition;
            pos.y -= scroll * zoomSpeed * Time.deltaTime * 10f;
            pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
            _targetPosition = pos;
        }
    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (dragPlane.Raycast(ray, out float enter))
        {
            worldPoint = ray.GetPoint(enter);
            return true;
        }

        worldPoint = Vector3.zero;
        return false;
    }

    private void ClampTargetPosition()
    {
        if (!useBounds)
            return;

        Vector3 pos = _targetPosition;
        pos.x = Mathf.Clamp(pos.x, xBounds.x, xBounds.y);
        pos.z = Mathf.Clamp(pos.z, zBounds.x, zBounds.y);
        _targetPosition = pos;
    }
}
