// =====================================================================
// GhostHpBar.cs
// HP 바 피격 잔상 효과
// OnHurt 이벤트로 피격 순간만 감지 → 자연 감소(Decay)에는 반응 안 함
// =====================================================================

using UnityEngine;
using UnityEngine.UI;

public class GhostHpBar : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private RectTransform ghostRect;  // 빨간 잔상 RectTransform
    [SerializeField] private Slider         hpSlider;  // 메인 HP 슬라이더

    [Header("잔상 설정")]
    [Tooltip("피격 후 잔상이 유지되는 시간 (초)")]
    [SerializeField] private float holdDuration  = 1.2f;

    [Tooltip("잔상이 줄어드는 속도 (전체 HP 비율 / 초). 낮을수록 천천히 사라짐")]
    [SerializeField] private float decreaseSpeed = 0.25f;

    [Header("색상")]
    [SerializeField] private Color ghostColor = new Color(0.85f, 0.1f, 0.1f, 0.85f);

    // ── 내부 ──────────────────────────────────────────────────────────
    private float  _ghostFill;    // 잔상 fillAmount (0~1)
    private float  _holdTimer;
    private bool   _isActive;     // 잔상 활성 여부
    private Player _player;

    // ── 초기화 ────────────────────────────────────────────────────────

    private void Awake()
    {
        var img = ghostRect?.GetComponent<Image>();
        if (img != null) img.color = ghostColor;

        ghostRect?.gameObject.SetActive(false);
    }

    private void Start()
    {
        _player = FindAnyObjectByType<Player>();
        if (_player != null)
            _player.Stat.OnHurt += OnHurt;
        else
            Debug.LogWarning("[GhostHP] Player를 찾을 수 없음");
    }

    private void OnDestroy()
    {
        if (_player != null)
            _player.Stat.OnHurt -= OnHurt;
    }

    // ── 피격 이벤트 ───────────────────────────────────────────────────

    private void OnHurt()
    {
        // 피격 순간의 HP를 고스트 시작값으로 저장
        // (이벤트는 HP 차감 직후 발생하므로, 아직 이 프레임에선 이전 fill 사용)
        _ghostFill = GetCurrentFill() + GetDamageEstimate();
        _ghostFill = Mathf.Clamp01(_ghostFill);

        _holdTimer = 0f;
        _isActive  = true;
        ghostRect?.gameObject.SetActive(true);
    }

    // ── 매 프레임 ─────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isActive) return;
        if (ghostRect == null || hpSlider == null) return;

        float currentFill = GetCurrentFill();

        _holdTimer += Time.deltaTime;

        if (_holdTimer >= holdDuration)
        {
            // hold 끝 → 서서히 현재 HP까지 감소
            _ghostFill = Mathf.MoveTowards(
                _ghostFill, currentFill,
                decreaseSpeed * Time.deltaTime);
        }

        // 잔상과 현재 HP가 거의 같아지면 숨기기
        if (_ghostFill <= currentFill + 0.005f)
        {
            _isActive = false;
            ghostRect.gameObject.SetActive(false);
            return;
        }

        ApplyGhostFill(currentFill);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────

    private void ApplyGhostFill(float currentFill)
    {
        if (ghostRect == null) return;

        // Fill Area 기준 좌표계로 정확히 위치 설정
        var fillAreaRect = ghostRect.parent as RectTransform;
        if (fillAreaRect == null) return;

        float areaWidth = fillAreaRect.rect.width;
        if (areaWidth <= 0f) return;

        // 앵커는 전체 stretch
        ghostRect.anchorMin = new Vector2(0f, 0f);
        ghostRect.anchorMax = new Vector2(1f, 1f);

        // offset으로 currentFill ~ ghostFill 구간만 표시
        ghostRect.offsetMin = new Vector2(areaWidth * currentFill, 0f);
        ghostRect.offsetMax = new Vector2(-areaWidth * (1f - _ghostFill), 0f);
    }

    private float GetCurrentFill()
    {
        if (hpSlider == null || hpSlider.maxValue <= 0f) return 1f;
        return hpSlider.value / hpSlider.maxValue;
    }

    // 피격 직후 실제 데미지 추정 (이전 프레임 fill - 현재 fill)
    private float _prevFill;
    private float GetDamageEstimate()
    {
        float cur    = GetCurrentFill();
        float damage = Mathf.Max(0f, _prevFill - cur);
        _prevFill    = cur;
        return damage;
    }
}
