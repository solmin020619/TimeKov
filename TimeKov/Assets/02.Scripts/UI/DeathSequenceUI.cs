using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DeathSequenceUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup overlayGroup; // DeathOverlay에 CanvasGroup 추천
    [SerializeField] private Image fadeBlack;
    [SerializeField] private TextMeshProUGUI timeZeroText;
    [SerializeField] private TextMeshProUGUI msgText;
    [SerializeField] private GameObject buttonsRoot;

    [Header("Timing")]
    [SerializeField] private float fadeOutDuration = 0.6f;
    [SerializeField] private float showMsgDelay = 0.2f;
    [SerializeField] private float showButtonsDelay = 0.6f;

    private bool played;

    private void Awake()
    {
        if (overlayGroup == null)
            overlayGroup = GetComponent<CanvasGroup>();

        if (overlayGroup == null)
            overlayGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void PlayDeathSequence()
    {
        if (played) return;
        played = true;
        StartCoroutine(CoDeath());
    }

    private IEnumerator CoDeath()
    {
        // 게임 멈추기
        Time.timeScale = 0f;

        // 커서 켜기(버튼 누르게)
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (buttonsRoot != null) buttonsRoot.SetActive(false);
        if (timeZeroText != null) timeZeroText.gameObject.SetActive(false);
        if (msgText != null) msgText.gameObject.SetActive(false);

        // 오버레이 표시 시작
        overlayGroup.alpha = 1f;

        // 페이드아웃(검정 알파 0 -> 1)
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / fadeOutDuration);

            if (fadeBlack != null)
            {
                var c = fadeBlack.color;
                c.a = n;
                fadeBlack.color = c;
            }
            yield return null;
        }

        // Time 0 표시
        yield return new WaitForSecondsRealtime(showMsgDelay);
        if (timeZeroText != null) timeZeroText.gameObject.SetActive(true);

        // 메시지 표시
        if (msgText != null) msgText.gameObject.SetActive(true);

        // 버튼 표시
        yield return new WaitForSecondsRealtime(showButtonsDelay);
        if (buttonsRoot != null) buttonsRoot.SetActive(true);
    }

    // 베이스로 돌아가기 누르면 호출될 때, 타임스케일 복구는 여기서도 해주자
    public void RestoreTimeScale()
    {
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.K)) // K 누르면 죽는 연출 테스트
            PlayDeathSequence();
    }
}
