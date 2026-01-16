using UnityEngine;

public class CursorRingFollow : MonoBehaviour
{
    [SerializeField] private RectTransform ring;
    [SerializeField] private Canvas canvas;



    private void Awake()
    {
        if (ring == null) ring = GetComponent<RectTransform>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)canvas.transform,
            Input.mousePosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out pos
        );

        ring.anchoredPosition = pos;
    }
}
