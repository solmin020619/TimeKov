using UnityEngine;

public class FollowPlayerScreenUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Camera targetCamera;

    [Header("Offset")]
    [Tooltip("X는 화면 기준 오른쪽, Y는 월드 위쪽, Z는 카메라 전방 기준 오프셋")]
    [SerializeField] private Vector3 worldOffset = new Vector3(1.05f, 1.2f, 0f);

    [Header("Visibility")]
    [SerializeField] private bool hideWhenBehindCamera = true;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        Vector3 targetWorldPosition =
            target.position
            + targetCamera.transform.right * worldOffset.x
            + Vector3.up * worldOffset.y
            + targetCamera.transform.forward * worldOffset.z;

        Vector3 screenPos = targetCamera.WorldToScreenPoint(targetWorldPosition);

        bool isBehindCamera = screenPos.z < 0f;

        if (hideWhenBehindCamera && canvasGroup != null)
            canvasGroup.alpha = isBehindCamera ? 0f : 1f;

        if (!isBehindCamera)
            rectTransform.position = screenPos;
    }
}