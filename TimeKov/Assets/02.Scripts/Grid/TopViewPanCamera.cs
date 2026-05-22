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
            ProcessMouseDrag(0, ref _leftDragLastWorld);

        ProcessMouseDrag(2, ref _middleDragLastWorld);
    }

    private void ProcessMouseDrag(int mouseButton, ref Vector3 lastWorld)
    {
        if (Input.GetMouseButtonDown(mouseButton))
        {
            if (TryGetMouseWorldPoint(out Vector3 worldPoint))
                lastWorld = worldPoint;
            return;
        }

        if (Input.GetMouseButton(mouseButton))
        {
            if (!TryGetMouseWorldPoint(out Vector3 currentWorld))
                return;

            Vector3 delta = lastWorld - currentWorld;

            if (invertDrag)
                delta = -delta;

            delta.y = 0f;
            _targetPosition += delta * dragSensitivity;

            // 카메라 이동 후 lastWorld를 같은 마우스 좌표로 재측정해야 다음 frame과 일관됨.
            // 단, transform.position은 LateUpdate 끝에서야 갱신되므로 ray는 아직 이전 위치 기준.
            // 따라서 SmoothDamp 결과를 미리 시뮬레이션해서 ray 시작점만 보정한 worldPoint를 다시 계산하는 대신,
            // 단순히 currentWorld를 저장 (다음 frame ScreenPointToRay에서 자연스럽게 수렴).
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
