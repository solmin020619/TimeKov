using System.Collections;
using UnityEngine;

/// <summary>
/// Tutorial.unity 씬 담당. 프롤로그 퀘스트 완료 → 불시착 연출 → World 씬 전환.
/// </summary>
public class PrologueManager : MonoBehaviour
{
    [Header("Crash Sequence Timing")]
    [SerializeField] float shakeDelay     = 0.3f;
    [SerializeField] float shakeDuration  = 2.0f;
    [SerializeField] float shakeMagnitude = 0.4f;
    [SerializeField] float fadeDelay      = 1.8f;
    [SerializeField] float fadeDuration   = 1.0f;

    [Header("References")]
    [Tooltip("씬 전체를 가릴 전체화면 검정 CanvasGroup (alpha 0 → 1 페이드아웃용)")]
    [SerializeField] CanvasGroup fadeCanvas;
    [Tooltip("추락 연출 컨트롤러. 연결되면 CrashSequence 대신 위임.")]
    [SerializeField] CrashSequenceController _crashController;

    void Start()
    {
        // 프롤로그는 결계 안(안전 구역)으로 취급 - 시간(HP) 드레인 방지.
        //   프롤로그 씬엔 BaseZone(결계)이 없어 IsInBase가 false로 남아, 결계 밖 취급이라 시간이 계속 깎이던 것 방지.
        //   (World 진입 시 새 Player + BaseZone이 IsInBase를 정상 구동하므로 여기서 켜도 World엔 영향 없음.)
        var stat = FindFirstObjectByType<PlayerStatComponent>();
        if (stat != null) stat.SetInBase(true);

        if (QuestManager.Instance != null)
            QuestManager.Instance.OnAllCompleted += OnPrologueComplete;
        else
            Debug.LogError("[PrologueManager] QuestManager.Instance null. 씬에 QuestManager가 있는지 확인.");
    }

    void OnDestroy()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnAllCompleted -= OnPrologueComplete;
    }

    void OnPrologueComplete()
    {
        if (_crashController != null) { _crashController.Play(); return; }
        StartCoroutine(CrashSequence()); // fallback
    }

    IEnumerator CrashSequence()
    {
        // 입력 차단
        PlayerInputComponent.IsBlocked = true;

        yield return new WaitForSeconds(shakeDelay);

        // 카메라 흔들기
        ThirdPersonCamera.Shake(shakeDuration, shakeMagnitude);

        yield return new WaitForSeconds(fadeDelay);

        // 화면 페이드 아웃
        if (fadeCanvas != null)
            yield return StartCoroutine(CoreUtilities.Fade(fadeCanvas, 0f, 1f, fadeDuration));
        else
            yield return new WaitForSeconds(fadeDuration);

        // 여기까지 왔으면 프롤로그를 끝까지 본 것. 다음부터 이 월드는 World 로 바로 들어간다.
        SaveSlotManager.Instance?.MarkPrologueDone();
        CoreUtilities.LoadViaLoading("World");
    }
}
