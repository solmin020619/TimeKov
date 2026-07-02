using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class ButtonAccentHoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] TMP_Text label;
    [SerializeField] bool isPrimary;

    static readonly Color TextNormal  = new Color32(0xBB, 0xC4, 0xCE, 0xFF);
    static readonly Color TextPrimary = new Color32(0xD8, 0xE2, 0xEC, 0xFF);
    static readonly Color TextHover   = new Color32(0xF0, 0xF4, 0xF8, 0xFF);
    static readonly Color BgNormal    = new Color32(0x62, 0x66, 0x6C, 0xFF);
    static readonly Color BgHover     = new Color32(0x76, 0x7A, 0x80, 0xFF);

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
        if (label != null) label.color = hover ? TextHover : (isPrimary ? TextPrimary : TextNormal);
        if (_bg != null)   _bg.color   = hover ? BgHover : BgNormal;
    }
}
