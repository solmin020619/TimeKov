using System.Reflection;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

[RequireComponent(typeof(TMP_Dropdown))]
public class DropdownStyleSwitcher : MonoBehaviour
{
    public Sprite closedSprite;
    public Sprite openSprite;

    TMP_Dropdown _dd;
    Image _rootImg;
    bool _wasOpen;
    FieldInfo _ddInstanceField;

    void Awake()
    {
        _dd = GetComponent<TMP_Dropdown>();
        _rootImg = GetComponent<Image>();
        // TMP_Dropdown.Show()는 template을 잠깐 켰다가 instance 생성 후 다시 끔.
        // activeSelf로는 감지 불가 → 실제 인스턴스(m_Dropdown)를 reflection으로 직접 확인.
        _ddInstanceField = typeof(TMP_Dropdown).GetField("m_Dropdown",
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    void LateUpdate()
    {
        if (_dd == null) return;

        bool isOpen;
        if (_ddInstanceField != null)
        {
            var ddGO = _ddInstanceField.GetValue(_dd) as GameObject;
            isOpen = ddGO != null && ddGO.activeInHierarchy;
        }
        else
        {
            isOpen = _dd.template != null && _dd.template.gameObject.activeSelf;
        }

        if (isOpen == _wasOpen) return;
        _wasOpen = isOpen;
        if (_rootImg != null)
            _rootImg.sprite = isOpen ? openSprite : closedSprite;
    }
}
