using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(VattalusDoorController))]
public class VattalusDoorBridge : MonoBehaviour, IInteractable, IInteractHint
{
    [SerializeField] GameObject _hintUI;
    [SerializeField] Sprite _hintIcon;

    VattalusDoorController _door;
    bool _intendedOpen; // isDoorOpen()은 애니메이션 종료 후 갱신 → 의도 상태로 직접 추적

    void Awake() => _door = GetComponent<VattalusDoorController>();

    void Start() => _intendedOpen = _door != null && _door.isDoorOpen();

    public bool CanInteract => true;

    public void ShowHint(bool show)
    {
        if (_hintUI == null) return;
        if (show) ApplyLabel();
        _hintUI.SetActive(show);
    }

    void ApplyLabel()
    {
        var tmp = _hintUI.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = _intendedOpen ? "문 닫기" : "문 열기";
        var iconImg = _hintUI.transform.Find("PanelRoot/RowContainer/FacilitySelectRow/Icon")?.GetComponent<Image>();
        if (iconImg != null)
        {
            iconImg.sprite = _hintIcon;
            iconImg.enabled = _hintIcon != null;
        }
    }

    public void Interact(Player player)
    {
        if (_intendedOpen) { _door.CloseDoor(); _intendedOpen = false; }
        else               { _door.OpenDoor();  _intendedOpen = true;  }
        if (_hintUI != null && _hintUI.activeSelf) ApplyLabel();
    }
}
