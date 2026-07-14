using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class EmergencyConsole : MonoBehaviour, IInteractable, IInteractHint
{
    [SerializeField] string interactId = "cockpit_console";
    [SerializeField] GameObject _hintUI;
    [SerializeField] string _hintLabel = "비상 제어 장치";
    [SerializeField] Sprite _hintIcon;
    [SerializeField] GameObject activatedEffect;

    [SerializeField] Outline[] _outlines;
    bool _activated;

    void Start()
    {
        SetOutlines(false);
    }

    public bool CanInteract => !_activated;

    public void ShowHint(bool show)
    {
        if (_activated) return;
        if (_hintUI != null)
        {
            if (show) ApplyLabel();
            _hintUI.SetActive(show);
        }
        SetOutlines(show);
    }

    void SetOutlines(bool enable)
    {
        foreach (var o in _outlines) if (o != null) o.enabled = enable;
    }

    /// <summary>시네마틱 등 외부에서 outline을 강제 제어할 때 사용.</summary>
    public void ForceOutline(bool enable) => SetOutlines(enable);

    void ApplyLabel()
    {
        var tmp = _hintUI.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = _hintLabel;
        var iconImg = _hintUI.transform.Find("PanelRoot/RowContainer/FacilitySelectRow/Icon")?.GetComponent<Image>();
        if (iconImg != null)
        {
            iconImg.sprite = _hintIcon;
            iconImg.enabled = _hintIcon != null;
        }
    }

    public void Interact(Player player)
    {
        if (_activated) return;
        _activated = true;
        ShowHint(false);
        if (activatedEffect != null) activatedEffect.SetActive(true);
        GameEvents.RaiseInteracted(interactId);
    }
}
