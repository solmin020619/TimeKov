using UnityEngine;
using UnityEngine.EventSystems;

public class HoverOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    static readonly Color HoverColor = new Color32(0x4D, 0xC8, 0xFF, 0xCC);

    UnityEngine.UI.Outline _outline;

    void Awake()
    {
        _outline = GetComponent<UnityEngine.UI.Outline>();
        if (_outline == null)
        {
            _outline = gameObject.AddComponent<UnityEngine.UI.Outline>();
            _outline.effectDistance = new Vector2(2.5f, 2.5f);
            _outline.useGraphicAlpha = false;
        }
        _outline.effectColor = Color.clear;
    }

    public void OnPointerEnter(PointerEventData e) { if (_outline) _outline.effectColor = HoverColor; }
    public void OnPointerExit(PointerEventData e)  { if (_outline) _outline.effectColor = Color.clear; }
}
