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
        // 데이터 로드가 성공할 때까지 반복한다. 실패해도 World 로 강행하지 않고 로딩씬에 머물며
        // 잠시 뒤 자동 재시도 -> 네트워크가 복구되면 그대로 통과한다.
        // (예전엔 실패해도 강행해서 데이터 깨진 채 게임에 진입, 플레이어가 영문 모른 채 망가진 화면을 봤음.)
        // 진행바는 구글시트 일괄 다운로드라 실제 진행도를 알 수 없어 가짜 진행도를 99%까지 점근 보간(끝에 도는 점).
        float dataFill = 0f;
        float dotTimer = 0f;
        int dotCount = 0;
        bool dataSuccess = false;
        int attempt = 0;

        while (!dataSuccess)
        {
            bool dataReady = false;
            attempt++;
            System.Action<bool> onLoaded = success => { dataSuccess = success; dataReady = true; };
            // 첫 시도는 LoadAsync, 재시도는 ForceReload(부분 로드 상태를 비우고 깨끗이 다시 받음).
            if (attempt == 1) DataBoot.Instance.LoadAsync(onLoaded);
            else              DataBoot.Instance.ForceReload(onLoaded);

            // 이번 시도 완료 대기
            while (!dataReady)
            {
                yield return null;
                dataFill = Mathf.Lerp(dataFill, 1f, Time.deltaTime * loadingSpeed * 0.6f);
                if (dataFill > 0.99f) dataFill = 0.99f;

                dotTimer += Time.deltaTime;
                if (dotTimer >= 0.35f) { dotTimer = 0f; dotCount = (dotCount + 1) % 4; }

                SetProgress(SCENE_LOAD_RATIO + dataFill * DATA_LOAD_RATIO, new string('.', dotCount));
            }

            if (dataSuccess) break;

            // 실패 -> World 진입 보류. 진행바 되감고 안내 표시 후 잠시 뒤 재시도.
            Debug.LogWarning($"[LoadingSceneManager] 데이터 로드 실패 (시도 {attempt}) - World 진입 보류, 재시도합니다.");
            dataFill = 0f;
            float wait = 2f;
            while (wait > 0f)
            {
                wait -= Time.unscaledDeltaTime;
                if (loadingText != null)
                    loadingText.text = string.Format(Loc.Get("데이터를 불러오지 못했습니다. 재시도 중...({0})"), attempt);
                yield return null;
            }
        }

        // 100% 확정
        SetProgress(1f);
        yield return new WaitForSeconds(0.3f);

        // 페이드 아웃
        yield return StartCoroutine(CoreUtilities.Fade(fadeCanvasGroup, 0f, 1f, fadeDuration));

        // 씬 활성화
        op.allowSceneActivation = true;
    }

    void SetProgress(float value, string suffix = "")
    {
        value = Mathf.Clamp01(value);
        if (loadingSlider != null) loadingSlider.value = value;
        if (loadingText   != null) loadingText.text    = ((int)(value * 100)) + "%" + suffix;
    }
}
