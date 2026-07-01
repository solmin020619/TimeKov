using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class ButtonAccentHoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Image accentBar;
    [SerializeField] TMP_Text label;
    [SerializeField] bool isPrimary;

    static readonly Color AccentNormal = new Color32(0x1E, 0x3A, 0x50, 0xFF);
    static readonly Color AccentActive = new Color32(0x4D, 0xC8, 0xFF, 0xFF);
    static readonly Color TextNormal   = new Color32(0xC5, 0xD5, 0xE5, 0xFF);
    static readonly Color BgNormal     = new Color32(0x0F, 0x1E, 0x2D, 0xEB);
    static readonly Color BgHover      = new Color32(0x0A, 0x18, 0x25, 0xEB);

    Image _bg;

    void Awake()
    {
        _bg = GetComponent<Image>();
        Apply(false);
    }

    public void OnPointerEnter(PointerEventData e) => Apply(true);
    public void OnPointerExit(PointerEventData e)  => Apply(false);

    void Apply(bool hover)
    {
        bool accent = hover || isPrimary;
        if (accentBar != null) accentBar.color = accent ? AccentActive : AccentNormal;
        if (label != null)    label.color    = accent ? AccentActive : TextNormal;
        if (_bg != null)      _bg.color      = hover  ? BgHover      : BgNormal;
    }
}
