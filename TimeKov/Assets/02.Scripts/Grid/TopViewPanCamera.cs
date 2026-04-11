using UnityEngine;

public class TopViewPanCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;

    [Header("Drag")]
    [SerializeField] private int dragMouseButton = 2; // 0=¡¬≈¨∏Ø, 1=øÏ≈¨∏Ø, 2=»Ÿ≈¨∏Ø
    [SerializeField] private float dragSensitivity = 1f;
    [SerializeField] private bool invertDrag = false;

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
    private Plane dragPlane;
    private Vector3 lastDragWorld;

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

    private void Update()
    {
        if (!isEnabledControl || cam == null)
            return;

        HandleDrag();
        HandleZoom();
        ClampPosition();
    }

    private void HandleDrag()
    {
        if (Input.GetMouseButtonDown(dragMouseButton))
        {
            if (TryGetMouseWorldPoint(out Vector3 worldPoint))
                lastDragWorld = worldPoint;
        }

        if (Input.GetMouseButton(dragMouseButton))
        {
            if (!TryGetMouseWorldPoint(out Vector3 currentWorld))
                return;

            Vector3 delta = lastDragWorld - currentWorld;

            if (invertDrag)
                delta = -delta;

            delta.y = 0f;
            transform.position += delta * dragSensitivity;

            lastDragWorld = currentWorld;
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