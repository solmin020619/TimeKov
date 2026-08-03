using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 메인메뉴 세로 메뉴 아이템의 호버 색/배경 전환 + 살짝 커지는 스케일 연출.
/// MainMenu 씬의 메뉴 항목에 실물로 붙어 있다(부착하던 에디터 빌더는 08-03 에 제거).
/// </summary>
public class MenuItemHoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float ScaleDuration = 0.18f;
    private const float ColorDuration = 0.15f;
    private const float HoverScale = 1.18f;

    private TMP_Text _text;
    private Image _bg;
    private Color _normalTextColor;
    private Color _hoverTextColor;
    private Color _hoverBgColor;
    private Color _normalBgColor;

    public void Setup(TMP_Text text, Image bg, Color normalTextColor, Color hoverTextColor, Color hoverBgColor)
    {
        _text = text;
        _bg = bg;
        _normalTextColor = normalTextColor;
        _hoverTextColor = hoverTextColor;
        _hoverBgColor = hoverBgColor;
        _normalBgColor = bg != null ? bg.color : default;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.DOKill();
        transform.DOScale(HoverScale, ScaleDuration).SetEase(Ease.OutBack).SetUpdate(true);

        if (_text != null) { _text.DOKill(); _text.DOColor(_hoverTextColor, ColorDuration).SetUpdate(true); }
        if (_bg != null) { _bg.DOKill(); _bg.DOColor(_hoverBgColor, ColorDuration).SetUpdate(true); }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.DOKill();
        transform.DOScale(1f, ScaleDuration).SetEase(Ease.OutCubic).SetUpdate(true);

        if (_text != null) { _text.DOKill(); _text.DOColor(_normalTextColor, ColorDuration).SetUpdate(true); }
        if (_bg != null) { _bg.DOKill(); _bg.DOColor(_normalBgColor, ColorDuration).SetUpdate(true); }
    }

    private void OnDisable()
    {
        // 비활성화 중 트윈이 끊겨 스케일/색이 어중간한 값으로 고정되는 것 방지.
        transform.DOKill();
        transform.localScale = Vector3.one;
        if (_text != null) { _text.DOKill(); _text.color = _normalTextColor; }
        if (_bg != null) { _bg.DOKill(); _bg.color = _normalBgColor; }
    }
}
