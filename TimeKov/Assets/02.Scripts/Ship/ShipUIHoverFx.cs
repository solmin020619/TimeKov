using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 우주선 수리 UI 호버 연출 (살짝 확대 + 밝아짐). ShipRepairUI 가 코드로 부착 - 씬 세팅 불필요.
// 밝기는 CanvasRenderer 배율(CrossFadeColor)로만 올려서 Refresh 가 칠하는 상태색(초록/시안/회색)과 안 싸운다.
// 버튼처럼 자체 ColorTint 가 있는 대상은 scaleOnly=true 로 확대만 준다(밝기 이중 구동 방지).
public class ShipUIHoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float hoverScale = 1.015f;
    public bool scaleOnly = false;

    private const float Duration = 0.12f;
    private static readonly Color HoverBright = new Color(1.10f, 1.10f, 1.10f, 1f);

    private Image _bg;

    private void Awake() => _bg = GetComponent<Image>();

    public void OnPointerEnter(PointerEventData e)
    {
        transform.DOKill();
        transform.DOScale(hoverScale, Duration).SetEase(Ease.OutCubic).SetUpdate(true);
        if (!scaleOnly && _bg != null) _bg.CrossFadeColor(HoverBright, Duration, true, true);
    }

    public void OnPointerExit(PointerEventData e)
    {
        transform.DOKill();
        transform.DOScale(1f, Duration).SetEase(Ease.OutCubic).SetUpdate(true);
        if (!scaleOnly && _bg != null) _bg.CrossFadeColor(Color.white, Duration, true, true);
    }

    private void OnDisable()
    {
        // 닫히는 중 트윈이 끊겨 어중간한 크기/밝기로 고정되는 것 방지
        transform.DOKill();
        transform.localScale = Vector3.one;
        if (_bg != null) _bg.CrossFadeColor(Color.white, 0f, true, true);
    }
}
