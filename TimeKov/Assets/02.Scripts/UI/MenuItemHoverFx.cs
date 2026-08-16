using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 메인메뉴 세로 메뉴 아이템의 호버 색/배경 전환 + 살짝 커지는 스케일 연출.
/// MainMenu 씬의 메뉴 항목에 실물로 붙어 있다(부착하던 에디터 빌더는 08-03 에 제거).
///
/// 눌림 연출도 여기서 같이 한다. 전역 자동 부착(UIButtonPressInstaller)은 이 컴포넌트가
/// 붙은 오브젝트를 일부러 건너뛴다 - 같은 transform 의 스케일을 둘이 쓰면 서로 밟기 때문.
/// 그래서 스케일 주인인 이쪽이 눌림까지 책임진다.
/// </summary>
public class MenuItemHoverFx : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private const float ScaleDuration = 0.18f;
    private const float ColorDuration = 0.15f;
    private const float HoverScale = 1.18f;

    // 눌림. 이미 커져 있는(호버) 상태 위에 겹치는 축소라 폭을 조금 더 준다.
    private const float PressScale = 0.94f;
    private const float PressDownDuration = 0.05f;
    private const float PressUpDuration = 0.11f;

    private TMP_Text _text;
    private Image _bg;
    private Color _normalTextColor;
    private Color _hoverTextColor;
    private Color _hoverBgColor;
    private Color _normalBgColor;
    private bool _hovered;

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
        _hovered = true;
        transform.DOKill();
        transform.DOScale(HoverScale, ScaleDuration).SetEase(Ease.OutBack).SetUpdate(true);

        if (_text != null) { _text.DOKill(); _text.DOColor(_hoverTextColor, ColorDuration).SetUpdate(true); }
        if (_bg != null) { _bg.DOKill(); _bg.DOColor(_hoverBgColor, ColorDuration).SetUpdate(true); }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        transform.DOKill();
        transform.DOScale(1f, ScaleDuration).SetEase(Ease.OutCubic).SetUpdate(true);

        if (_text != null) { _text.DOKill(); _text.DOColor(_normalTextColor, ColorDuration).SetUpdate(true); }
        if (_bg != null) { _bg.DOKill(); _bg.DOColor(_normalBgColor, ColorDuration).SetUpdate(true); }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

        // ★클릭음은 여기서 내지 않는다. 항목마다 무슨 소리를 낼지가 다르기 때문 —
        //   '게임 시작'은 전용 시작음(SfxId.TitleStart)을 내고 그 길이만큼 기다렸다 넘어간다.
        //   여기서 공통 클릭음까지 내면 그 항목만 소리가 두 번 겹친다.
        //   각 항목의 처리부에서 자기 소리를 낸다.
        transform.DOKill();
        transform.DOScale(HoverScale * PressScale, PressDownDuration).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 누른 채로 밖으로 나갔다 뗄 수도 있다. 지금 호버 중인지로 돌아갈 크기를 정한다.
        transform.DOKill();
        transform.DOScale(_hovered ? HoverScale : 1f, PressUpDuration).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    private void OnDisable()
    {
        // 비활성화 중 트윈이 끊겨 스케일/색이 어중간한 값으로 고정되는 것 방지.
        _hovered = false;
        transform.DOKill();
        transform.localScale = Vector3.one;
        if (_text != null) { _text.DOKill(); _text.color = _normalTextColor; }
        if (_bg != null) { _bg.DOKill(); _bg.color = _normalBgColor; }
    }
}
