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

    private bool isEnabledControl;
    private bool isMouseDragEnabled = true;
    private Plane dragPlane;
    private Vector3 _leftDragLastWorld;
    private Vector3 _middleDragLastWorld;

    private void Awake()
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        dragPlane = new Plane(Vector3.up, Vector3.zero);
    }

    public void SetControlEnabled(bool value)
    {
        isEnabledControl = value;
    }

    public void SetMouseDragEnabled(bool value)
    {
        isMouseDragEnabled = value;
    }

    private void Update()
    {
        if (!isEnabledControl || cam == null)
            return;

        HandleKeyboardMove();
        HandleDrag();
        HandleZoom();
        ClampPosition();
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

        transform.position += delta * keyboardMoveSpeed * Time.deltaTime;
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
        }

        if (Input.GetMouseButton(mouseButton))
        {
            if (!TryGetMouseWorldPoint(out Vector3 currentWorld))
                return;

            Vector3 delta = lastWorld - currentWorld;

            if (invertDrag)
                delta = -delta;

            delta.y = 0f;
            transform.position += delta * dragSensitivity;

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
            Vector3 pos = transform.position;
            pos.y -= scroll * zoomSpeed * Time.deltaTime * 10f;
            pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
            transform.position = pos;
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

    private void ClampPosition()
    {
        if (!useBounds)
            return;

        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, xBounds.x, xBounds.y);
        pos.z = Mathf.Clamp(pos.z, zBounds.x, zBounds.y);
        transform.position = pos;
    }
}
