// UISlideEffect.cs
// UI 패널 열기/닫기 = 가로 슬라이드 (열기: 오른쪽->왼쪽 슬라이드 인, 닫기: 왼쪽->오른쪽 슬라이드 아웃) + 페이드.
// 엔드필드 인벤 느낌. UIUnfoldEffect(공용 Y스케일)와 달리 인벤 전용.
//
// [블러 동기화] 블러는 별도 캔버스(Screen Space-Camera)라, 패널과 "같은 X 오프셋"으로 함께 슬라이드시켜야
//   패널만 움직이고 블러는 중앙에 뜨는 어긋남이 안 생긴다. SetExtras(syncSlide)로 블러 영역 RectTransform 전달.
//
// [연타 멈춤 버그 방지] 컨트롤러가 Open()을 명시 호출 + Open/Close가 서로 코루틴 StopCoroutine.
//   닫기 도중 다시 열면 Open()이 닫기 코루틴을 끊어 SetActive(false)가 안 불리므로 '논리상 열림+화면없음' 멈춤이 안 생긴다.

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UISlideEffect : MonoBehaviour
{
    [Header("슬라이드")]
    [Tooltip("애니메이션 시간(초, 언스케일드)")]
    public float duration = 0.2f;
    [Tooltip("오른쪽 오프셋(px). 작게 = 살짝만 슬라이드(페이드가 주). 엔드필드처럼 안 거슬리게 40 부근.")]
    public float offsetX = 40f;

    [Header("같이 페이드/끌/슬라이드 대상 (선택)")]
    [SerializeField] private CanvasGroup extraFade;        // 블러 캔버스 CanvasGroup
    [SerializeField] private GameObject extraDeactivate;   // 닫힘 완료 시 같이 끌 오브젝트 (블러 캔버스)
    [SerializeField] private RectTransform syncSlide;      // 같이 슬라이드시킬 것 (블러 영역) - 정렬 유지

    private RectTransform _rt;
    private CanvasGroup _cg;
    private Vector2 _shownPos;
    private Vector2 _syncShownPos;
    private Coroutine _co;
    private bool _init;

    private void Awake() => EnsureInit();

    private void EnsureInit()
    {
        if (_init) return;
        _rt = (RectTransform)transform;
        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        _shownPos = _rt.anchoredPosition;
        _init = true;
    }

    private void OnEnable()
    {
        if (_init) Open();
    }

    // 외부에서 같이 페이드/끌/슬라이드 대상 지정 (컨트롤러가 블러 캔버스/영역 연결)
    public void SetExtras(CanvasGroup fade, GameObject deactivate, RectTransform sync)
    {
        extraFade = fade;
        extraDeactivate = deactivate;
        syncSlide = sync;
        if (syncSlide != null) _syncShownPos = syncSlide.anchoredPosition;
    }

    public void Open()
    {
        EnsureInit();
        if (syncSlide != null && _syncShownPos == Vector2.zero) _syncShownPos = syncSlide.anchoredPosition;
        if (_co != null) StopCoroutine(_co);
        if (_cg != null) _cg.blocksRaycasts = true;
        _co = StartCoroutine(Slide(true));
    }

    public void Close()
    {
        if (!gameObject.activeSelf) return;
        EnsureInit();
        if (_co != null) StopCoroutine(_co);
        if (_cg != null) _cg.blocksRaycasts = false;
        _co = StartCoroutine(Slide(false));
    }

    private IEnumerator Slide(bool opening)
    {
        float a0 = opening ? 0f : 1f;
        float a1 = opening ? 1f : 0f;

        ApplyState(opening ? offsetX : 0f, a0);   // 시작 상태 즉시 (1프레임 깜빡임 방지)

        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float e = opening ? 1f - (1f - t) * (1f - t)   // ease-out
                              : t * t;                       // ease-in
            float off = opening ? Mathf.LerpUnclamped(offsetX, 0f, e)
                                : Mathf.LerpUnclamped(0f, offsetX, e);
            ApplyState(off, Mathf.Lerp(a0, a1, t));
            yield return null;
        }

        _co = null;
        ApplyState(opening ? 0f : offsetX, a1);

        if (!opening)
        {
            // 다음 열기를 위해 위치/알파 복원해두고 비활성화
            ApplyState(0f, 1f);
            if (extraDeactivate != null) extraDeactivate.SetActive(false);
            gameObject.SetActive(false);
        }
        else
        {
            _cg.blocksRaycasts = true;
        }
    }

    // 패널 + 블러영역을 같은 X 오프셋으로, 알파 동기
    private void ApplyState(float offX, float alpha)
    {
        _rt.anchoredPosition = _shownPos + new Vector2(offX, 0f);
        if (syncSlide != null) syncSlide.anchoredPosition = _syncShownPos + new Vector2(offX, 0f);
        _cg.alpha = alpha;
        if (extraFade != null) extraFade.alpha = alpha;
    }
}
