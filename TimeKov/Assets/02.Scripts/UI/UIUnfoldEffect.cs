using UnityEngine;
using System.Collections;

public class UIUnfoldEffect : MonoBehaviour
{
    [Header("펼쳐지는 시간 (초)")]
    public float duration = 0.15f;

    [Header("펼쳐지는 방식")]
    public AnimationCurve unfoldCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Coroutine unfoldCoroutine;
    private Vector3 originalScale;
    private bool isInitialized = false;

    private void Awake()
    {
        // 1. 처음 시작할 때 이 오브젝트의 '진짜' 원래 크기를 저장해 둡니다.
        originalScale = transform.localScale;
        isInitialized = true;
    }

    private void OnEnable()
    {
        if (!isInitialized) return;

        if (unfoldCoroutine != null)
            StopCoroutine(unfoldCoroutine);

        unfoldCoroutine = StartCoroutine(UnfoldRoutine());
    }

    private IEnumerator UnfoldRoutine()
    {
        // 2. 시작할 때 원래 크기의 X, Z는 유지하고 Y(세로)만 0으로 납작하게 만듭니다.
        transform.localScale = new Vector3(originalScale.x, 0f, originalScale.z);

        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;

            float progress = time / duration;
            float curveValue = unfoldCurve.Evaluate(progress);

            // 3. 원래 크기의 Y값에 커브 비율을 곱해서 서서히 펴지게 만듭니다.
            transform.localScale = new Vector3(originalScale.x, originalScale.y * curveValue, originalScale.z);

            yield return null;
        }

        // 4. 무조건 1배율이 아니라, 저장해둔 '원래 크기'로 완벽하게 원상복구!
        transform.localScale = originalScale;
    }
}