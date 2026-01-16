using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class UIButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float animDuration = 0.08f;

    private Vector3 originalScale;
    private Coroutine co;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData) => StartScale(originalScale * hoverScale);
    public void OnPointerExit(PointerEventData eventData) => StartScale(originalScale);

    private void StartScale(Vector3 target)
    {
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(ScaleRoutine(target));
    }

    private IEnumerator ScaleRoutine(Vector3 target)
    {
        Vector3 start = transform.localScale;
        float t = 0f;

        while (t < animDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / animDuration);
            float eased = 1f - Mathf.Pow(1f - n, 3f);

            transform.localScale = Vector3.Lerp(start, target, eased);
            yield return null;
        }

        transform.localScale = target;
    }
}
