using System.Reflection;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

[RequireComponent(typeof(TMP_Dropdown))]
public class DropdownStyleSwitcher : MonoBehaviour
{
    public Sprite closedSprite;
    public Sprite openSprite;

    // 닫힌 상태: 흰색 버튼 / 열린 상태: 어두운 배경과 동일한 회색
    public Color closedColor = new Color(0.93f, 0.93f, 0.93f, 1f);
    public Color openColor   = new Color(0.32f, 0.33f, 0.35f, 1f);
    public Color closedTextColor = new Color(0.12f, 0.12f, 0.14f, 1f);
    public Color openTextColor   = new Color(0.76f, 0.78f, 0.80f, 1f);

    const float ItemHeight = 64f;
    const float TopPad     = 24f;
    const float BotPad     = 8f;
    const float MaxVisible = 3f;

    TMP_Dropdown _dd;
    Image        _rootImg;
    TMP_Text     _label;
    Image        _arrow;
    bool         _wasOpen;
    FieldInfo    _ddInstanceField;

    void Awake()
    {
        _dd    = GetComponent<TMP_Dropdown>();
        _rootImg = GetComponent<Image>();
        _label = _dd.transform.Find("Label")?.GetComponent<TMP_Text>();
        _arrow = _dd.transform.Find("Arrow")?.GetComponent<Image>();
        _ddInstanceField = typeof(TMP_Dropdown).GetField("m_Dropdown",
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    void Start()
    {
        // 초기(닫힌) 상태 색 설정 — 첫 LateUpdate에서는 변화 없으면 스킵하기 때문에
        if (_rootImg != null) { _rootImg.sprite = closedSprite; _rootImg.color = closedColor; }
        if (_label != null) _label.color = closedTextColor;
        if (_arrow != null) _arrow.color = closedTextColor;
    }

    void LateUpdate()
    {
        if (_dd == null) return;

        GameObject ddGO = _ddInstanceField?.GetValue(_dd) as GameObject;
        bool isOpen = ddGO != null && ddGO.activeInHierarchy;

        if (isOpen && ddGO != null)
        {
            int   count      = _dd.options.Count;
            float visible    = Mathf.Min(count, MaxVisible);
            float targetH    = visible * ItemHeight + TopPad + BotPad;

            var rt = ddGO.GetComponent<RectTransform>();
            if (rt != null)
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetH);

            var vpRt = ddGO.transform.Find("Viewport")?.GetComponent<RectTransform>();
            if (vpRt != null)
            {
                vpRt.offsetMin = new Vector2(0f, BotPad);
                vpRt.offsetMax = new Vector2(0f, -TopPad);
            }
        }

        if (isOpen == _wasOpen) return;
        _wasOpen = isOpen;

        // 스프라이트 + 색 동시 전환
        if (_rootImg != null)
        {
            _rootImg.sprite = isOpen ? openSprite   : closedSprite;
            _rootImg.color  = isOpen ? openColor    : closedColor;
        }
        if (_label != null) _label.color = isOpen ? openTextColor : closedTextColor;
        if (_arrow != null) _arrow.color = isOpen ? openTextColor : closedTextColor;
    }
}
