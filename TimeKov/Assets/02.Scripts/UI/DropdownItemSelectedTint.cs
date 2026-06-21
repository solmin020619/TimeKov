using UnityEngine;
using UnityEngine.UI;

// TMP_Dropdown의 Item 템플릿에 붙여서, 펼침 목록에서 현재 선택된 항목만 다른 배경색으로 강조한다.
// TMP_Dropdown은 목록을 열 때마다 이 템플릿을 복제하면서 Toggle.isOn을 선택 상태에 맞게 미리 설정해두므로,
// OnEnable 시점에 그 값을 읽어 배경색을 바로 적용하면 된다.
public class DropdownItemSelectedTint : MonoBehaviour
{
    public Image background;
    public Toggle toggle;
    public Color selectedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
    public Color unselectedColor = new Color(0.93f, 0.93f, 0.93f, 1f);

    void OnEnable()
    {
        if (toggle == null || background == null) return;
        Apply(toggle.isOn);
        toggle.onValueChanged.AddListener(Apply);
    }

    void OnDisable()
    {
        if (toggle != null) toggle.onValueChanged.RemoveListener(Apply);
    }

    void Apply(bool isOn)
    {
        background.color = isOn ? selectedColor : unselectedColor;
    }
}
