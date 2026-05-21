// =====================================================================
// ButtonColorEffect.cs
// 버튼 아이콘(Image)에 붙여 사용
// - 마우스 올리면 회색으로 변함
// - 클릭하면 검은색 → 원래 색으로 부드럽게 복귀
// =====================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class ButtonColorEffect : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("색상 설정")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor  = new Color(0.65f, 0.65f, 0.65f, 1f);
    [SerializeField] private Color clickColor  = Color.black;

    [Header("클릭 후 복귀 속도 (초)")]
    [SerializeField] private float clickFadeDuration = 0.2f;

    private Graphic   _graphic;
    private Coroutine _fadeRoutine;
    private bool      _isHovering;

    private void Awake()
    {
        _graphic = GetComponent<Graphic>();
        _graphic.color = normalColor;
    }

    // ── 이벤트 핸들러 ────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovering = true;
        StopFade();
        _graphic.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
        StopFade();
        _graphic.color = normalColor;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        StopFade();
        _graphic.color = clickColor;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 마우스를 뗐을 때 호버 중이면 hoverColor, 아니면 normalColor로 복귀
        Color target = _isHovering ? hoverColor : normalColor;
        StopFade();
        _fadeRoutine = StartCoroutine(FadeToColor(clickColor, target, clickFadeDuration));
    }

    // ── 내부 유틸 ────────────────────────────────────────────

    private IEnumerator FadeToColor(Color from, Color to, float duration)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(duration, 0.001f);
            _graphic.color = Color.Lerp(from, to, t);
            yield return null;
        }
        _graphic.color = to;
        _fadeRoutine = null;
    }

    private void StopFade()
    {
        if (_fadeRoutine == null) return;
        StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;
    }
}
