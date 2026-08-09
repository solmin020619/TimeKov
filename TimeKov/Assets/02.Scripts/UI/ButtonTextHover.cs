using UnityEngine;
using UnityEngine.EventSystems; // 마우스 이벤트 감지용
using TMPro; // TextMeshPro 사용 시 필수

public class ButtonTextHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public TextMeshProUGUI buttonText;

    [Header("텍스트 색상 설정")]
    public Color normalColor = Color.black;      // 평소 (검은색)
    public Color hoverColor = Color.white;       // 마우스 올렸을 때 (흰색)
    public Color pressedColor = Color.gray;      // 눌렀을 때 (회색)

    private void Start()
    {
        if (buttonText != null)
            buttonText.color = normalColor;
    }

    // 마우스가 버튼 위로 올라왔을 때
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (buttonText != null) buttonText.color = hoverColor;
    }

    // 마우스가 버튼 밖으로 나갔을 때
    public void OnPointerExit(PointerEventData eventData)
    {
        if (buttonText != null) buttonText.color = normalColor;
    }

    // 마우스를 클릭하는 순간
    public void OnPointerDown(PointerEventData eventData)
    {
        if (buttonText != null) buttonText.color = pressedColor;
    }

    // 마우스 클릭을 떼는 순간
    public void OnPointerUp(PointerEventData eventData)
    {
        // 뗐을 때 여전히 마우스가 버튼 위에 있다면 Hover 색상으로, 아니면 Normal로
        if (buttonText != null)
            buttonText.color = eventData.pointerCurrentRaycast.gameObject == gameObject ? hoverColor : normalColor;
    }
}