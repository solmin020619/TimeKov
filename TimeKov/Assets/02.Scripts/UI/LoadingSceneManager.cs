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

    // ── 표시값/실제값 분리 ────────────────────────────────────────────
    // 실제 진행 신호는 원래 뚝뚝 끊긴다:
    //   - LoadSceneAsync.progress 는 큰 덩어리 몇 번으로만 움직인다(0 근처에 오래 머물다 0.9 로 점프)
    //   - 데이터가 이미 캐시돼 있으면 Phase 2 콜백이 즉시 떨어져 애니메이션 루프를 한 프레임도 안 돈다
    // 바를 신호에 직결하면 "0% 에서 멈췄다가 100% 로 팍" 이 된다(종욱 QA 08-15).
    // 그래서 화면의 바(_display)는 실제 진행(_target)을 추격만 한다 - 멀면 빨리, 가까우면 천천히,
    // 절대 순간이동하지 않는다. 어떤 경로로 끝나든 바는 항상 이어서 흐른다.
    private float _display;
    private float _target;

    private const float CatchUpPerGap = 4f;     // 추격 속도 = 격차 x 이 값 (초당)
    private const float MinBarSpeed   = 0.08f;  // 격차가 작아도 최소 이 속도로는 움직인다

    // ★목표값 자체에도 상승 속도 제한을 건다.
    //   추격기만으로는 부족했다 - LoadSceneAsync 가 한 번에 0 -> 0.9 로 뛰면 목표가 즉시 60% 가 되고,
    //   추격 속도는 격차 비례라 0.25초 만에 따라잡아 결국 눈에는 점프로 보인다(종욱 QA: 0 -> 69%).
    //   목표가 초당 이 속도보다 빨리 오르지 못하게 막으면, 신호가 아무리 계단식이어도
    //   화면에서는 항상 일정 속도로 차오른다.
    private const float MaxTargetRise = 0.28f;  // 목표 상승 상한(초당). 0.28 = 0에서 100까지 최소 약 3.6초

    // 목표를 올리되 한 프레임에 올릴 수 있는 양을 제한한다. 되돌리지는 않는다(단조증가).
    void RaiseTarget(float want)
    {
        if (want <= _target) return;
        _target = Mathf.Min(want, _target + MaxTargetRise * Time.deltaTime);
    }

    private void Start()
    {
        // 씬에 구운 안내 문구("이 게임은 아직 개발중인 게임입니다.")에 번역을 붙인다.
        // 표는 Loc 부트스트랩이 캐시에서 미리 깔아둔다(첫 실행만 한국어).
        // 라벨이 이 오브젝트 하위란 보장이 없어 씬 루트를 전부 훑는다(로딩 씬은 작다).
        foreach (var go in gameObject.scene.GetRootGameObjects())
            LocalizedLabel.AttachToStaticLabels(go);

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

        // ★빌드 목록에 없는 씬이면 LoadSceneAsync 는 null 을 돌려준다(예외를 던지지 않는다).
        //   그대로 두면 다음 줄에서 NullReferenceException 이 나서 진짜 원인이 안 보인다.
        //   씬 폴더를 옮기거나 이름을 바꾸면 실제로 이 상태가 된다(08-15 폴더정리 때 발생).
        if (op == null)
        {
            Debug.LogError($"[LoadingSceneManager] '{CoreUtilities.NextSceneName}' 씬이 빌드 목록에 없다. " +
                           "File > Build Profiles 의 Scene List 에 추가해야 한다.");
            if (loadingText != null)
                loadingText.text = string.Format(Loc.Get("씬 '{0}' 을(를) 빌드 목록에서 찾을 수 없습니다."),
                                                 CoreUtilities.NextSceneName);
            yield break;   // 로딩씬에 머문다. 검은 화면으로 넘어가는 것보다 낫다.
        }

        op.allowSceneActivation = false;

        // 실제 신호(op.progress)가 덩어리째 멈춰 있어도 바가 굳지 않게 가짜 진행을 점근으로 섞는다.
        // 실제 신호가 더 앞서면 그걸 따른다. 상한 92%(구간 기준) = 진짜 끝나기 전에 다 찬 척은 안 한다.
        float scnFake = 0f;
        while (op.progress < 0.9f)
        {
            scnFake = Mathf.Lerp(scnFake, 1f, Time.deltaTime * 0.35f * loadingSpeed);
            float real = op.progress / 0.9f;
            RaiseTarget(Mathf.Min(Mathf.Max(real, scnFake), 0.92f) * SCENE_LOAD_RATIO);
            DriveBar();
            yield return null;
        }

        // Phase 1 완료. 목표를 60% 로 두되 여기서도 한 번에 올리지 않는다 -
        // 씬 로드가 순식간에 끝나는 경우(가벼운 씬/캐시)를 그대로 통과시키면 다시 점프가 된다.
        while (_target < SCENE_LOAD_RATIO - 0.001f)
        {
            RaiseTarget(SCENE_LOAD_RATIO);
            DriveBar();
            yield return null;
        }

        // ── Phase 2: 데이터 로드 (60 ~ 100%) ───────────────────────
        // 데이터 로드가 성공할 때까지 반복한다. 실패해도 World 로 강행하지 않고 로딩씬에 머물며
        // 잠시 뒤 자동 재시도 -> 네트워크가 복구되면 그대로 통과한다.
        // (예전엔 실패해도 강행해서 데이터 깨진 채 게임에 진입, 플레이어가 영문 모른 채 망가진 화면을 봤음.)
        // 진행바는 시트 다운로드 실제 진행도(GameDataHolder.DownloadProgress = 끝난 장수/전체 장수)를 쓴다.
        // 시트가 12장이라 한 장 끝날 때마다 8% 씩 계단으로 오르는데, 그 계단은 목표 상승 제한(RaiseTarget)이
        // 부드럽게 편다. 가짜 보간은 신호가 아직 0일 때(첫 장이 안 끝난 구간)만 바닥을 받쳐준다.
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

            // 이번 시도 완료 대기 (캐시 히트로 콜백이 즉시 떨어지면 이 루프는 안 돈다 -
            // 그래도 바는 아래 100% 추격 루프가 이어서 채우므로 점프하지 않는다)
            while (!dataReady)
            {
                // 가짜 진행은 바닥만 받친다(첫 시트가 끝나기 전 구간). 상한 0.35 = 실제 신호를 앞지르지 않게.
                dataFill = Mathf.Lerp(dataFill, 1f, Time.deltaTime * loadingSpeed * 0.6f);
                if (dataFill > 0.35f) dataFill = 0.35f;

                float realData = Mathf.Clamp01(GameDataHolder.I.DownloadProgress);
                float shown = Mathf.Max(realData, dataFill);
                if (shown > 0.98f) shown = 0.98f;   // 파싱/현지화가 남아 있다 - 100% 는 진짜 끝날 때만

                dotTimer += Time.deltaTime;
                if (dotTimer >= 0.35f) { dotTimer = 0f; dotCount = (dotCount + 1) % 4; }

                RaiseTarget(SCENE_LOAD_RATIO + shown * DATA_LOAD_RATIO);
                DriveBar(new string('.', dotCount));
                yield return null;
            }

            if (dataSuccess) break;

            // 실패 -> World 진입 보류. 안내 표시 후 잠시 뒤 재시도.
            // 바는 되감지 않는다(표시값은 단조증가) - 뒤로 훅 빠지는 바는 고장처럼 보인다.
            Debug.LogWarning($"[LoadingSceneManager] 데이터 로드 실패 (시도 {attempt}) - World 진입 보류, 재시도합니다.");
            float wait = 2f;
            while (wait > 0f)
            {
                wait -= Time.unscaledDeltaTime;
                if (loadingText != null)
                    loadingText.text = string.Format(Loc.Get("데이터를 불러오지 못했습니다. 재시도 중...({0})"), attempt);
                yield return null;
            }
        }

        // 100% 까지 추격으로 마저 채운다. 캐시 히트로 순식간에 끝난 판이어도
        // 여기서 0 -> 100 이 약 1초짜리 한 번의 흐름으로 보인다(점프 없음).
        while (_display < 0.995f)
        {
            RaiseTarget(1f);
            DriveBar();
            yield return null;
        }
        SetProgress(1f);
        yield return new WaitForSeconds(0.3f);

        // 페이드 아웃
        yield return StartCoroutine(CoreUtilities.Fade(fadeCanvasGroup, 0f, 1f, fadeDuration));

        // 씬 활성화
        op.allowSceneActivation = true;
    }

    // 표시값을 목표로 한 걸음 추격시키고 화면에 반영한다. 매 프레임 1회 호출.
    void DriveBar(string suffix = "")
    {
        float gap = _target - _display;
        if (gap > 0f)
        {
            float speed = Mathf.Max(MinBarSpeed, gap * CatchUpPerGap);
            _display = Mathf.MoveTowards(_display, _target, speed * Time.deltaTime);
        }
        SetProgress(_display, suffix);
    }

    void SetProgress(float value, string suffix = "")
    {
        value = Mathf.Clamp01(value);
        if (loadingSlider != null) loadingSlider.value = value;
        if (loadingText   != null) loadingText.text    = ((int)(value * 100)) + "%" + suffix;
    }
}
