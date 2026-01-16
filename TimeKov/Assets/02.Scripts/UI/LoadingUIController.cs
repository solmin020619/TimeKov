using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class LoadingUIController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI tipText;

    [Header("Fade Overlay (Image)")]
    [SerializeField] private Image fadeOverlay; // FadeOverlay Panel의 Image
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float completeDelay = 0.5f;

    [Header("Loading Text")]
    [SerializeField] private string baseLoadingText = "로딩중";
    [SerializeField] private float dotInterval = 0.35f;
    [SerializeField] private int maxDots = 3;

    [Header("Tips")]
    [SerializeField] private TipDatabase tipDatabase;
    [SerializeField] private string tipPrefix = "팁: ";

    private Coroutine dotsCo;
    private bool isCompleting;

    private void OnEnable()
    {
        SetRandomTip();
        dotsCo = StartCoroutine(DotsRoutine());

        // 페이드 오버레이가 있으면 시작은 투명으로
        if (fadeOverlay != null)
        {
            var c = fadeOverlay.color;
            c.a = 0f;
            fadeOverlay.color = c;
            fadeOverlay.raycastTarget = true; // 전환 중 입력 막기
        }
    }

    private void OnDisable()
    {
        if (dotsCo != null) StopCoroutine(dotsCo);
    }

    //  LoadingSceneController가 로딩 완료 시 호출할 함수
    public IEnumerator PlayCompleteAndFadeOut()
    {
        if (isCompleting) yield break;
        isCompleting = true;

        // 점 애니메이션 멈추기 (원하면 유지해도 됨)
        if (dotsCo != null) StopCoroutine(dotsCo);

        // 0.5초 딜레이
        yield return new WaitForSeconds(completeDelay);

        // 페이드아웃(화면을 검정으로 덮기)
        if (fadeOverlay != null)
        {
            float t = 0f;
            Color c = fadeOverlay.color;

            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                c.a = Mathf.Clamp01(t / fadeOutDuration);
                fadeOverlay.color = c;
                yield return null;
            }

            c.a = 1f;
            fadeOverlay.color = c;
        }
    }

    private void SetRandomTip()
    {
        if (tipText == null) return;

        if (tipDatabase == null || tipDatabase.tips == null || tipDatabase.tips.Length == 0)
        {
            tipText.text = tipPrefix + "팁 데이터가 비어있습니다.";
            return;
        }

        int idx = Random.Range(0, tipDatabase.tips.Length);
        tipText.text = tipPrefix + tipDatabase.tips[idx];
    }

    private IEnumerator DotsRoutine()
    {
        if (loadingText == null) yield break;

        int dots = 0;
        while (true)
        {
            dots = (dots + 1) % (maxDots + 1);
            loadingText.text = baseLoadingText + new string('.', dots);
            yield return new WaitForSeconds(dotInterval);
        }
    }
}
