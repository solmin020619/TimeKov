using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;   // 고정 카메라에 URP 표현 설정(포스트 프로세싱 등) 복사용
using UnityEngine.UI;

// ── 우주선 외형 변화 연출 ─────────────────────────────────────────────────────
// 수리로 우주선 모델이 바뀔 때:
//   ① 화면이 천천히 검어짐(페이드 아웃)
//   ② 검은 동안 모델을 교체하고, 고정 카메라로 전환
//   ③ 페이드 인 — 바뀐 외형을 보여줌
//   ④ 잠시 유지 후 다시 페이드 아웃
//   ⑤ 플레이어 시점으로 복귀 후 페이드 인
// 연출 동안 UI를 끄고 플레이어 조작을 막는다.
//
// ★UI는 캔버스 컴포넌트만 꺼서 숨긴다(GameObject 를 끄면 자식들의 OnDisable 이 연쇄로 돌아
//   설정창 블러 해제 같은 부작용이 난다). 페이드용 캔버스는 대상에서 제외한다.
// ★고정 카메라에 AudioListener 가 있으면 꺼 준다 — 씬에 둘이면 유니티가 경고를 뱉는다.
public class ShipStageCinematic : MonoBehaviour
{
    [Header("카메라")]
    [Tooltip("바뀐 우주선을 비출 고정 카메라. 씬에 배치해 두고 비활성 상태로 둔다.")]
    [SerializeField] private Camera showcaseCamera;

    [Header("타이밍(초)")]
    [Tooltip("플레이어 화면 → 검게")]
    [SerializeField] private float fadeOutTime = 0.8f;
    [Tooltip("완전히 검은 상태 유지(이 사이에 모델 교체·카메라 전환)")]
    [SerializeField] private float blackHold = 0.25f;
    [Tooltip("검게 → 우주선(아직 이전 외형)")]
    [SerializeField] private float fadeInTime = 0.8f;
    [Tooltip("교체가 시작되기 전, 이전 외형을 보여주는 시간")]
    [SerializeField] private float beforeHold = 0.6f;
    [Tooltip("이전 외형이 디졸브로 사라지는 시간")]
    [SerializeField] private float dissolveOutTime = 1.1f;
    [Tooltip("새 외형이 디졸브로 나타나는 시간")]
    [SerializeField] private float dissolveInTime = 1.1f;
    [Tooltip("바뀐 외형을 보여주는 시간")]
    [SerializeField] private float viewHold = 1.6f;

    [Header("디졸브 (플레이어 사망 연출과 같은 효과)")]
    [SerializeField] private ModelDissolve.Style dissolveStyle = new ModelDissolve.Style();

    [Header("연출 중 처리")]
    [Tooltip("체크: 연출 동안 게임 UI를 숨긴다.")]
    [SerializeField] private bool hideUI = true;
    [Tooltip("체크: 연출 동안 플레이어 조작을 막는다.")]
    [SerializeField] private bool blockInput = true;

    /// 연출 진행 중인가. 중복 발동을 막는다.
    public bool IsPlaying { get; private set; }

    private CanvasGroup _fadeCg;
    private GameObject _fadeGO;

    /// 연출을 재생한다.
    ///   from : 지금 보이는(이전 단계) 모델 — 디졸브로 사라진다
    ///   to   : 다음 단계 모델 — 디졸브로 나타난다
    ///   applyChange : 실제 교체(활성 전환). 이전 모델이 다 사라진 뒤에 호출된다.
    public void Play(GameObject from, GameObject to, Action applyChange)
    {
        if (IsPlaying)
        {
            applyChange?.Invoke();   // 겹치면 연출 없이 즉시 반영(상태가 어긋나지 않게)
            return;
        }
        StartCoroutine(Routine(from, to, applyChange));
    }

    private IEnumerator Routine(GameObject from, GameObject to, Action applyChange)
    {
        IsPlaying = true;
        EnsureFader();

        bool prevBlocked = PlayerInputComponent.IsBlocked;
        if (blockInput) PlayerInputComponent.IsBlocked = true;

        List<Canvas> hidden = hideUI ? HideGameCanvases() : null;

        // ① 플레이어 화면 → 검게
        yield return Fade(0f, 1f, fadeOutTime);
        yield return new WaitForSecondsRealtime(blackHold);

        // ② 검은 동안 고정 카메라로 전환하고, 이전 모델을 디졸브 재질로 미리 갈아끼운다.
        //    ★교체를 보이는 상태에서 하면 재질이 달라 보인다(디졸브 셰이더와 URP/Lit 은
        //      그림자 패스·GI 경로까지 같을 수 없다). 그래서 검을 때 미리 바꿔 둔다.
        Camera prevCam = SwitchToShowcase();
        if (from != null) ModelDissolve.Prepare(from, 0f, dissolveStyle);   // 0 = 온전한 모습

        // ③ 페이드 인 — 이전 외형이 보인다
        yield return Fade(1f, 0f, fadeInTime);
        yield return new WaitForSecondsRealtime(beforeHold);

        // ④ 이전 외형이 녹아 사라짐 → 교체 → 새 외형이 차오르며 등장
        if (from != null)
        {
            yield return ModelDissolve.Animate(from, 0f, 1.05f, dissolveOutTime);
            ModelDissolve.Restore(from, disableRenderers: true);   // 안 보이는 상태라 티가 안 난다
        }

        applyChange?.Invoke();   // from 끄고 to 켜기(상태 갱신)

        if (to != null)
        {
            ModelDissolve.Prepare(to, 1.05f, dissolveStyle);   // 완전히 사라진 상태로 시작
            yield return ModelDissolve.Animate(to, 1.05f, 0f, dissolveInTime);
        }

        // ⑤ 잠시 감상 → 다시 검게 → (검은 동안) 원본 재질 복구 → 플레이어 시점 복귀
        yield return new WaitForSecondsRealtime(viewHold);
        yield return Fade(0f, 1f, fadeOutTime);

        if (to != null) ModelDissolve.Restore(to);   // ★복구도 검을 때 — 재질이 바뀌는 순간이 안 보인다
        RestoreCamera(prevCam);
        if (hidden != null) RestoreCanvases(hidden);

        yield return Fade(1f, 0f, fadeInTime);

        if (blockInput) PlayerInputComponent.IsBlocked = prevBlocked;
        IsPlaying = false;
    }

    // ── 카메라 ───────────────────────────────────────────────────────────────
    // ★메인 카메라를 '끄지 않는다'. 끄면 Camera.main 이 null 이 되고,
    //   ThirdPersonCamera 처럼 매 프레임 Camera.main 을 참조하는 코드가 NullReference 로 도배된다.
    //   대신 고정 카메라의 depth 를 위로 올려 덮어 그린다(플레이어 카메라는 밑에서 계속 돌아도 무해).
    private Camera SwitchToShowcase()
    {
        var prev = Camera.main;
        if (showcaseCamera == null) return prev;   // 카메라를 안 걸었으면 시점 전환 없이 진행

        var al = showcaseCamera.GetComponent<AudioListener>();
        if (al != null) al.enabled = false;        // 리스너 중복 경고 방지

        CopyCameraLook(prev, showcaseCamera);   // 색감이 튀지 않게 메인 카메라 설정을 맞춘다

        showcaseCamera.depth = (prev != null ? prev.depth : 0f) + 10f;   // 위에 그린다
        showcaseCamera.gameObject.SetActive(true);
        showcaseCamera.enabled = true;
        return prev;
    }

    // 새로 만든 카메라는 URP 포스트 프로세싱이 꺼져 있어, 그대로 쓰면 톤매핑·컬러그레이딩·블룸이
    // 빠진 화면이 나온다(전환 순간 색감이 확 달라짐). 메인 카메라의 표현 설정을 그대로 복사한다.
    private static void CopyCameraLook(Camera from, Camera to)
    {
        if (from == null || to == null) return;

        to.allowHDR = from.allowHDR;
        to.allowMSAA = from.allowMSAA;
        to.clearFlags = from.clearFlags;
        to.backgroundColor = from.backgroundColor;
        to.cullingMask = from.cullingMask;

        var src = from.GetUniversalAdditionalCameraData();
        var dst = to.GetUniversalAdditionalCameraData();
        if (src == null || dst == null) return;

        dst.renderPostProcessing = src.renderPostProcessing;   // ★색감의 핵심
        dst.antialiasing         = src.antialiasing;
        dst.antialiasingQuality  = src.antialiasingQuality;
        dst.renderShadows        = src.renderShadows;
        dst.volumeLayerMask      = src.volumeLayerMask;
        dst.volumeTrigger        = src.volumeTrigger;
        // 렌더러(인덱스)는 읽어올 공개 API가 없어 복사하지 않는다.
        // 기본 렌더러를 쓰는 한 문제없고, 다른 렌더러를 써야 하면 카메라 인스펙터에서 직접 지정할 것.
    }

    private void RestoreCamera(Camera prev)
    {
        if (showcaseCamera == null) return;
        showcaseCamera.enabled = false;
        showcaseCamera.gameObject.SetActive(false);
        // prev 는 애초에 끄지 않았으므로 되돌릴 것이 없다.
    }

    // ── UI 숨김 ──────────────────────────────────────────────────────────────
    // GameObject 가 아니라 Canvas 컴포넌트만 끈다 → 자식 OnDisable 연쇄가 없다.
    private List<Canvas> HideGameCanvases()
    {
        var hidden = new List<Canvas>();
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c == null || !c.enabled) continue;
            if (c.isRootCanvas == false) continue;               // 루트만 끄면 자식은 따라 사라진다
            if (_fadeGO != null && c.transform.IsChildOf(_fadeGO.transform)) continue;   // 페이드는 유지
            c.enabled = false;
            hidden.Add(c);
        }
        return hidden;
    }

    private static void RestoreCanvases(List<Canvas> hidden)
    {
        foreach (var c in hidden) if (c != null) c.enabled = true;
    }

    // ── 페이드 ───────────────────────────────────────────────────────────────
    private void EnsureFader()
    {
        if (_fadeCg != null) return;

        _fadeGO = new GameObject("ShipStageFade", typeof(Canvas), typeof(CanvasGroup));
        _fadeGO.transform.SetParent(transform, false);

        var canvas = _fadeGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;   // 모든 UI 위

        var imgGO = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imgGO.transform.SetParent(_fadeGO.transform, false);
        var rt = (RectTransform)imgGO.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = imgGO.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;

        _fadeCg = _fadeGO.GetComponent<CanvasGroup>();
        _fadeCg.alpha = 0f;
        _fadeCg.blocksRaycasts = false;
        _fadeCg.interactable = false;
    }

    private IEnumerator Fade(float from, float to, float dur)
    {
        if (_fadeCg == null) yield break;
        _fadeCg.blocksRaycasts = true;

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;   // 정지 중에도 진행
            _fadeCg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            yield return null;
        }
        _fadeCg.alpha = to;
        _fadeCg.blocksRaycasts = to > 0.99f;
    }
}
