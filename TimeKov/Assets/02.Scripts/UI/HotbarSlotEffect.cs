using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HotbarSlotEffect : MonoBehaviour
{
    public Image slotBackground;

    private Color originalColor;

    [Header("색상 효과")]
    public Color selectedColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    public Color flashColor    = new Color(0f,   0f,   0f,   0.9f);
    public float flashDuration = 0.05f;

    [Header("위로 올라오는 효과")]
    [Tooltip("선택 시 위로 올라올 픽셀 거리")]
    public float liftAmount   = 20f;
    [Tooltip("올라가고 내려오는 데 걸리는 시간 (초)")]
    public float liftDuration = 0.12f;

    [Header("설비 이름 팝업")]
    [Tooltip("이름 팝업 루트 오브젝트 (CanvasGroup 컴포넌트 필요)")]
    [SerializeField] private CanvasGroup namePopupGroup;
    [Tooltip("이름 텍스트 (TextMeshProUGUI)")]
    [SerializeField] private TextMeshProUGUI nameLabel;
    [Tooltip("팝업 페이드 시간 (초)")]
    [SerializeField] private float nameFadeDuration = 0.15f;

    // ─────────────────────────────────────────────────────────────

    private RectTransform _parentRect;
    private float         _originalY;
    private float         _liftProgress = 0f;

    private Coroutine _colorCoroutine;
    private Coroutine _nameFadeCoroutine;
    private bool      _isSelected = false;

    // ── 초기화 ───────────────────────────────────────────────────

    void Awake()
    {
        _parentRect = transform.parent as RectTransform;
        if (_parentRect != null)
            _originalY = _parentRect.localPosition.y;

        if (slotBackground != null)
            originalColor = slotBackground.color;

        // 팝업 초기 상태: 숨김
        if (namePopupGroup != null)
        {
            namePopupGroup.alpha = 0f;
            namePopupGroup.gameObject.SetActive(false);
        }
    }

    // ── 매 프레임: 부모 위치 업데이트 ────────────────────────────

    void Update()
    {
        if (_parentRect == null) return;

        float targetProgress = _isSelected ? 1f : 0f;
        float speed          = 1f / Mathf.Max(0.001f, liftDuration);
        _liftProgress = Mathf.MoveTowards(_liftProgress, targetProgress,
                                          Time.unscaledDeltaTime * speed);

        float easedT = Mathf.SmoothStep(0f, 1f, _liftProgress);

        Vector3 lp = _parentRect.localPosition;
        lp.y = _originalY + liftAmount * easedT;
        _parentRect.localPosition = lp;
    }

    // ── 선택 상태 변경 ────────────────────────────────────────────

    /// <summary>
    /// 슬롯 선택 상태 설정.
    /// facilityName: 선택 시 표시할 설비 이름 (빈 문자열이면 팝업 비표시)
    /// </summary>
    public void SetSelected(bool selected, string facilityName = "")
    {
        if (_isSelected == selected) return;
        _isSelected = selected;

        // 비활성('Quick Slot' 꺼짐) 오브젝트에선 코루틴을 못 켜므로 즉시 상태만 반영
        bool canAnimate = isActiveAndEnabled;

        // 색상 효과
        if (slotBackground != null)
        {
            if (_colorCoroutine != null) StopCoroutine(_colorCoroutine);
            if (_isSelected)
            {
                if (canAnimate) _colorCoroutine = StartCoroutine(FlashAndKeepColor());
                else slotBackground.color = selectedColor;
            }
            else
                slotBackground.color = originalColor;
        }

        // 이름 팝업 페이드
        if (namePopupGroup != null)
        {
            if (_nameFadeCoroutine != null) StopCoroutine(_nameFadeCoroutine);

            bool show = _isSelected && !string.IsNullOrEmpty(facilityName);
            if (show && nameLabel != null) nameLabel.text = facilityName;

            if (canAnimate)
                _nameFadeCoroutine = StartCoroutine(FadeNamePopup(show));
            else
            {
                namePopupGroup.alpha = show ? 1f : 0f;
                namePopupGroup.gameObject.SetActive(show);
            }
        }
    }

    // ── 코루틴 ───────────────────────────────────────────────────

    IEnumerator FlashAndKeepColor()
    {
        slotBackground.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        slotBackground.color = selectedColor;
    }

    IEnumerator FadeNamePopup(bool show)
    {
        if (show)
            namePopupGroup.gameObject.SetActive(true);

        float startAlpha = namePopupGroup.alpha;
        float endAlpha   = show ? 1f : 0f;
        float elapsed    = 0f;

        while (elapsed < nameFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            namePopupGroup.alpha = Mathf.Lerp(startAlpha, endAlpha,
                                              elapsed / nameFadeDuration);
            yield return null;
        }
        namePopupGroup.alpha = endAlpha;

        if (!show)
            namePopupGroup.gameObject.SetActive(false);
    }
}
