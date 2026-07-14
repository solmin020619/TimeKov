using System.Collections;
using UnityEngine;

/// <summary>
/// 비상 제어 장치 퀘스트 표시 시 카메라가 해당 위치로 줌인해 아웃라인을 보여준다.
/// 흐름: 줌인(smooth) → 유지 → 줌아웃(smooth) → 순간 암전(snap 커버) → 플레이어 뷰 복귀
/// </summary>
public class PrologueCinematicHint : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] string _questId = "quest_prolog_emergency";

    [Header("References")]
    [Tooltip("EmergencyConsole 바로 앞 줌인 시점 Transform")]
    [SerializeField] Transform _viewPoint;
    [Tooltip("복귀 시 카메라 스냅을 가리는 암전용 CanvasGroup (FadeCanvas)")]
    [SerializeField] CanvasGroup _fadeCanvas;
    [Tooltip("아웃라인을 강제 표시할 EmergencyConsole 컴포넌트")]
    [SerializeField] EmergencyConsole _emergencyConsole;

    [Header("Timing")]
    [SerializeField] float _zoomInDuration  = 0.7f;
    [SerializeField] float _holdDuration    = 1.5f;
    [SerializeField] float _zoomOutDuration = 0.6f;
    [SerializeField] float _snapFadeDuration = 0.15f;

    bool _played;

    void Start()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestShown += OnQuestShown;
    }

    void OnDestroy()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestShown -= OnQuestShown;
    }

    void OnQuestShown(CategoryRuntime rt, QuestSO quest)
    {
        if (_played || quest == null || quest.id != _questId) return;
        _played = true;
        StartCoroutine(PlayCinematic());
    }

    IEnumerator PlayCinematic()
    {
        if (_viewPoint == null) yield break;

        var cam = Camera.main;
        var tpc = ThirdPersonCamera.Instance;

        // 아웃라인 강제 표시
        _emergencyConsole?.ForceOutline(true);

        PlayerInputComponent.IsBlocked = true;
        if (tpc != null) tpc.enabled = false;

        // 시작 transform 저장
        Vector3    fromPos = cam.transform.position;
        Quaternion fromRot = cam.transform.rotation;
        Vector3    toPos   = _viewPoint.position;
        Quaternion toRot   = _viewPoint.rotation;

        // ─ 줌인 ───────────────────────────────────────────────
        float t = 0f;
        while (t < _zoomInDuration)
        {
            t += Time.deltaTime;
            float f = Mathf.SmoothStep(0f, 1f, t / _zoomInDuration);
            cam.transform.position = Vector3.Lerp(fromPos, toPos, f);
            cam.transform.rotation = Quaternion.Slerp(fromRot, toRot, f);
            yield return null;
        }

        // ─ 유지 ───────────────────────────────────────────────
        yield return new WaitForSeconds(_holdDuration);

        // ─ 줌아웃 (원위치로) ──────────────────────────────────
        t = 0f;
        while (t < _zoomOutDuration)
        {
            t += Time.deltaTime;
            float f = Mathf.SmoothStep(0f, 1f, t / _zoomOutDuration);
            cam.transform.position = Vector3.Lerp(toPos, fromPos, f);
            cam.transform.rotation = Quaternion.Slerp(toRot, fromRot, f);
            yield return null;
        }

        // ─ 짧은 암전으로 ThirdPersonCamera 스냅 커버 ──────────
        if (_fadeCanvas != null)
            yield return StartCoroutine(CoreUtilities.Fade(_fadeCanvas, 0f, 1f, _snapFadeDuration));

        if (tpc != null) tpc.enabled = true;     // 플레이어 위치로 즉시 스냅

        if (_fadeCanvas != null)
            yield return StartCoroutine(CoreUtilities.Fade(_fadeCanvas, 1f, 0f, _snapFadeDuration));

        // 아웃라인 복원 (플레이어가 가까이 없으면 숨김)
        _emergencyConsole?.ForceOutline(false);

        PlayerInputComponent.IsBlocked = false;
    }
}
