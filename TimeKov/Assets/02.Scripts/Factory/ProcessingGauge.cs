// =====================================================================
// ProcessingGauge.cs
// 가공 진행 게이지 (Arrow Gauge) — 기존 ">>" 화살표 대체
//
// 동작:
//   - 외부에서 SetProgress(0~1)로 진행도 동기 (MachineUI가 _machine.Progress 매 프레임 전달)
//   - 또는 loop=true로 자체 타이머 사이클 (3.5초 반복, 가공 시간 무관)
//
// UI 계층 (인스펙터 작업):
//   ProcessingGauge (RectTransform, 빈 GO + 이 컴포넌트)
//   ├── ArrowTrack  (Image, arrow_track.png) — 배경 화살표
//   └── ArrowFill   (Image, arrow_fill.png, Type=Filled / Horizontal / Origin=Left)
//
// 인스펙터:
//   arrowFill 슬롯에 자식 ArrowFill 의 Image 컴포넌트 드래그
//
// 외부 API:
//   StartProcessing()  — 0%부터 시작
//   SetProgress(t01)   — 외부 진행도 강제 동기
//   Complete()         — 100% 채운 채로 멈춤
//   StopAndHide()      — 0%로 비우고 GO 비활성
//   Pause() / Resume() — 자체 타이머 일시정지/재개
// =====================================================================

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ProcessingGauge : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("채워지는 화살표 Image. Image Type=Filled, Method=Horizontal, Origin=Left 권장.")]
    [SerializeField] private Image arrowFill;

    [Tooltip("(선택) 전체 페이드용 CanvasGroup. 없으면 무시.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Timing")]
    [Tooltip("자체 타이머 1사이클 시간(초). 외부 SetProgress 사용 시 무관.")]
    [SerializeField] private float cycleDuration = 3.5f;

    [Tooltip("자체 타이머 자동 반복 여부. MachineUI에서 외부 동기 쓸 거면 false 권장.")]
    [SerializeField] private bool loop = false;

    [Header("Optional FX")]
    [Tooltip("0%일 때 살짝 투명하게 시작 (자연스러운 페이드 인)")]
    [SerializeField] private bool fadeInOnEmpty = false;

    [Header("State (read-only)")]
    [SerializeField, Range(0f, 1f)] private float progress01 = 0f;

    private float _elapsed;
    private bool _running = false;

    // ── Editor 보조 ───────────────────────────────────────────────

    void Reset()
    {
        // 컴포넌트 처음 부착 시 자식의 ArrowFill 자동 잡기 (두 번째 자식 가정)
        if (arrowFill == null && transform.childCount > 1)
            arrowFill = transform.GetChild(1).GetComponent<Image>();
    }

    void OnValidate()
    {
        // 인스펙터에서 progress01 슬라이드하면 에디터에서 즉시 미리보기
        if (arrowFill != null) arrowFill.fillAmount = progress01;
    }

    // ── 매 프레임 (자체 타이머 모드) ─────────────────────────────

    void Update()
    {
        if (!_running || arrowFill == null) return;

        _elapsed += Time.deltaTime;
        progress01 = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, cycleDuration));
        arrowFill.fillAmount = progress01;

        if (progress01 >= 1f)
        {
            if (loop)
            {
                _elapsed = 0f;
            }
            else
            {
                _running = false;
            }
        }

        if (fadeInOnEmpty && canvasGroup != null)
            canvasGroup.alpha = Mathf.Lerp(0.6f, 1f, progress01);
    }

    // ── 외부 API ─────────────────────────────────────────────────

    /// <summary>가공 시작 — 0%부터 자체 타이머 진행 (loop=true 면 자동 반복).</summary>
    public void StartProcessing()
    {
        _elapsed = 0f;
        progress01 = 0f;
        if (arrowFill != null) arrowFill.fillAmount = 0f;
        _running = true;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    /// <summary>자체 타이머 일시정지 (SetProgress 외부 동기 모드와 무관).</summary>
    public void Pause() => _running = false;

    /// <summary>자체 타이머 재개.</summary>
    public void Resume() => _running = true;

    /// <summary>100% 채운 상태로 멈춤 (결과 수령 대기 표시).</summary>
    public void Complete()
    {
        progress01 = 1f;
        _elapsed = cycleDuration;
        if (arrowFill != null) arrowFill.fillAmount = 1f;
        _running = false;
    }

    /// <summary>0%로 비우고 GO 비활성. 가공 중단/완료 후 정리용.</summary>
    public void StopAndHide()
    {
        _running = false;
        progress01 = 0f;
        if (arrowFill != null) arrowFill.fillAmount = 0f;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    /// <summary>외부 진행도(예: ProcessingMachine.Progress)로 강제 동기.
    /// 매 프레임 호출하면 자체 타이머 무시하고 외부 값만 따름.
    /// 호출 즉시 내부 타이머는 강제 OFF — 외부 동기 모드 진입 신호.</summary>
    public void SetProgress(float t01)
    {
        // [Sync Fix] 외부 동기 모드 — 내부 타이머가 같이 돌면서 fillAmount 덮어쓰는 충돌 차단
        _running = false;
        progress01 = Mathf.Clamp01(t01);
        if (arrowFill != null) arrowFill.fillAmount = progress01;
    }
}
