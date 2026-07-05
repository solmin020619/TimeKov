using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DropdownItemSelectedTint : MonoBehaviour
{
    public Image    background;
    public Toggle   toggle;
    public TMP_Text label;
    public Color selectedColor        = new Color(0.42f, 0.44f, 0.47f, 1f);
    public Color unselectedColor      = new Color(0.93f, 0.93f, 0.93f, 1f);
    public Color selectedTextColor    = new Color(0.88f, 0.90f, 0.92f, 1f);
    public Color unselectedTextColor  = new Color(0.12f, 0.12f, 0.14f, 1f);

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
        if (label != null) label.color = isOn ? selectedTextColor : unselectedTextColor;
    }
}
