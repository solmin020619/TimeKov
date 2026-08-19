// StatPanelRevealEffect.cs
// 플레이어 스탯창(좌하단 HUD) 전용 열기 애니메이션.
// 패널이 SetActive(true) 될 때(OnEnable) 자동 재생 — GameUIController.TogglePlayerStat()이
// statPanel.SetActive 로 여닫으므로 별도 호출 없이도 C키로 열 때마다 동작한다.
//
// 연출: 좌하단 코너에서 "솟아오르며 펼쳐지는" 느낌.
//   - 아래로 살짝 내려간 위치에서 제자리로 슬라이드 업
//   - 0.9 배에서 1.0 배로 살짝 커짐(코너 기준 grow) + ease-out-back 미세 오버슈트
//   - 알파 0 -> 1 페이드
// 모든 보간은 unscaledTime 기반 — 시간정지(일시정지) 중에도 매끄럽게 열린다.
//
// pivot 을 좌하단(0,0)으로 두면 스케일이 코너에 고정돼 "왼쪽 아래에서 자라나는" 모양이 된다.
// (Awake 에서 자동 보정. 보정 시 anchoredPosition 도 함께 옮겨 화면상 위치는 그대로 유지)

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class StatPanelRevealEffect : MonoBehaviour
{
    [Header("타이밍")]
    [Tooltip("애니메이션 시간(초, 언스케일드)")]
    public float duration = 0.28f;

    [Header("슬라이드 업")]
    [Tooltip("시작 시 아래로 내려가 있는 거리(px). 여기서 제자리로 솟아오른다.")]
    public float riseOffset = 36f;

    [Header("스케일")]
    [Tooltip("시작 스케일(코너 기준). 1보다 작으면 코너에서 커지며 열린다.")]
    public float startScale = 0.9f;
    [Tooltip("끝에서 살짝 튀는 오버슈트 양(0이면 없음). 0.05 정도가 자연스럽다.")]
    public float overshoot = 0.045f;

    [Header("pivot 자동 보정")]
    [Tooltip("켜면 Awake 에서 pivot 을 좌하단(0,0)으로 옮겨 코너 grow 가 되게 한다. 위치는 유지.")]
    public bool forceBottomLeftPivot = true;

    private RectTransform _rt;
    private CanvasGroup _cg;
    private Vector2 _shownPos;
    private Coroutine _co;
    private bool _init;

    /// <summary>패널 크기·위치를 바꾼 뒤 부른다. 열림 위치(_shownPos)를 다시 잡는다.
    /// ★Awake 에서 한 번 캡처하기 때문에, 나중에 옮기고 이걸 안 부르면 열 때마다 옛 자리로
    ///   되돌아간다(같은 오브젝트의 컴포넌트끼리도 Awake 순서는 믿을 게 못 된다).</summary>
    public void RecaptureShownPos()
    {
        EnsureInit();
        _shownPos = ((RectTransform)transform).anchoredPosition;
    }

    private void Awake() => EnsureInit();

    private void EnsureInit()
    {
        if (_init) return;
        _rt = (RectTransform)transform;
        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

        if (forceBottomLeftPivot)
            ApplyBottomLeftPivot();

        _shownPos = _rt.anchoredPosition;
        _init = true;
    }

    // pivot 을 (0,0)으로 옮기되, 옮긴 만큼 anchoredPosition 을 보정해 화면상 위치는 그대로 둔다.
    private void ApplyBottomLeftPivot()
    {
        Vector2 oldPivot = _rt.pivot;
        Vector2 newPivot = Vector2.zero;
        if (oldPivot == newPivot) return;

        Vector2 size = _rt.rect.size;
        Vector2 deltaPivot = newPivot - oldPivot;
        // pivot 이동에 따른 위치 보정(스케일 1 기준).
        Vector2 offset = new Vector2(deltaPivot.x * size.x, deltaPivot.y * size.y);
        _rt.pivot = newPivot;
        _rt.anchoredPosition += offset;
    }

    private void OnEnable()
    {
        // Awake 전에 OnEnable 이 먼저 올 수 있어 방어적으로 초기화.
        EnsureInit();
        Play();
    }

    private void OnDisable()
    {
        // 다음 열기를 위해 정상 상태로 복원(코루틴 중단 시 어정쩡한 상태로 남지 않게).
        if (_co != null) { StopCoroutine(_co); _co = null; }
        ApplyState(0f, 1f, 1f);
    }

    public void Play()
    {
        if (!gameObject.activeInHierarchy) return;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        // 시작 상태 즉시 적용(첫 프레임 깜빡임 방지)
        ApplyState(-riseOffset, startScale, 0f);

        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);

            float ePos = EaseOutCubic(t);                 // 위치/페이드: 부드러운 ease-out
            float eScale = EaseOutBack(t, overshoot);     // 스케일: 살짝 오버슈트

            float yOff  = Mathf.Lerp(-riseOffset, 0f, ePos);
            float scale = Mathf.LerpUnclamped(startScale, 1f, eScale);
            float alpha = ePos;

            ApplyState(yOff, scale, alpha);
            yield return null;
        }

        _co = null;
        ApplyState(0f, 1f, 1f);
    }

    private void ApplyState(float yOffset, float scale, float alpha)
    {
        _rt.anchoredPosition = _shownPos + new Vector2(0f, yOffset);
        _rt.localScale = new Vector3(scale, scale, 1f);
        if (_cg != null) _cg.alpha = alpha;
    }

    private static float EaseOutCubic(float t)
    {
        float u = 1f - t;
        return 1f - u * u * u;
    }

    // 끝에서 살짝 튀는 ease-out-back. amount=오버슈트 세기(0이면 일반 ease-out).
    private static float EaseOutBack(float t, float amount)
    {
        float c1 = 1.70158f * Mathf.Max(0f, amount) / 0.10158f; // amount≈0.045 → c1≈0.75
        float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
