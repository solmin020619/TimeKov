// =====================================================================
// LoadingSceneManager.cs
// 로딩 씬 전체 흐름 관리
//
// Phase 1 (0 ~ 60%): 다음 씬 AsyncLoad
// Phase 2 (60 ~ 100%): DataBoot.LoadAsync (이미 로드됐으면 즉시 통과)
// 두 단계 모두 완료 후 씬 활성화
// =====================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LoadingSceneManager : MonoBehaviour
{
    [Header("UI Components")]
    public Slider loadingSlider;
    public TextMeshProUGUI loadingText;
    public CanvasGroup fadeCanvasGroup;

    [Header("Settings")]
    public float fadeDuration = 0.5f;
    public float loadingSpeed = 1.0f;

    // Phase 비율
    private const float SCENE_LOAD_RATIO = 0.6f;   // Phase 1: 0 ~ 60%
    private const float DATA_LOAD_RATIO  = 0.4f;   // Phase 2: 60 ~ 100%

    private void Start()
    {
        if (loadingSlider != null) loadingSlider.value = 0f;
        if (loadingText   != null) loadingText.text    = "0%";

        // DataBoot 가 씬에 없으면 자동 생성 (씬 직접 열기 대비)
        if (DataBoot.Instance == null)
        {
            var go = new GameObject("[DataBoot]");
            DontDestroyOnLoad(go);
            go.AddComponent<DataBoot>();
        }

        StartCoroutine(LoadProcess());
    }

    IEnumerator LoadProcess()
    {
        // 페이드 인
        yield return StartCoroutine(CoreUtilities.Fade(fadeCanvasGroup, 1f, 0f, fadeDuration));

        // ── Phase 1: 씬 비동기 로드 (0 ~ 60%) ──────────────────────
        AsyncOperation op = SceneManager.LoadSceneAsync(CoreUtilities.NextSceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            yield return null;
            float phaseProgress = (op.progress / 0.9f) * SCENE_LOAD_RATIO;
            SetProgress(phaseProgress);
        }

        // Phase 1 완료 → 60% 고정
        SetProgress(SCENE_LOAD_RATIO);

        // ── Phase 2: 데이터 로드 (60 ~ 100%) ───────────────────────
        bool dataReady = false;

        DataBoot.Instance.LoadAsync(success =>
        {
            if (!success)
                Debug.LogError("[LoadingSceneManager] 데이터 로드 실패 — 씬 전환을 강행합니다.");
            dataReady = true;
        });

        // 데이터 로드 중 진행바 스무스 이동 (실제 완료 전까지 99%까지만)
        float dataFill = 0f;
        while (!dataReady)
        {
            yield return null;
            dataFill = Mathf.MoveTowards(dataFill, 0.99f, Time.deltaTime * loadingSpeed * 0.3f);
            SetProgress(SCENE_LOAD_RATIO + dataFill * DATA_LOAD_RATIO);
        }

        // 100% 확정
        SetProgress(1f);
        yield return new WaitForSeconds(0.3f);

        // 페이드 아웃
        yield return StartCoroutine(CoreUtilities.Fade(fadeCanvasGroup, 0f, 1f, fadeDuration));

        // 씬 활성화
        op.allowSceneActivation = true;
    }

    void SetProgress(float value)
    {
        value = Mathf.Clamp01(value);
        if (loadingSlider != null) loadingSlider.value = value;
        if (loadingText   != null) loadingText.text    = ((int)(value * 100)) + "%";
    }
}
