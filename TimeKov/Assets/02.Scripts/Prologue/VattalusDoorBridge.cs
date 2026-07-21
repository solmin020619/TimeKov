using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(VattalusDoorController))]
public class VattalusDoorBridge : MonoBehaviour, IInteractable, IInteractHint
{
    [SerializeField] GameObject _hintUI;
    [SerializeField] Sprite _hintIcon;

    VattalusDoorController _door;
    bool _intendedOpen;
    bool _waitingForLeave; // 상호작용 후 범위 밖으로 나갈 때까지 힌트 숨김

    void Awake() => _door = GetComponent<VattalusDoorController>();

    void Start() => _intendedOpen = _door != null && _door.isDoorOpen();

    public bool CanInteract => !_waitingForLeave;

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
        if (_hintUI != null) _hintUI.SetActive(false);
        _waitingForLeave = true;
        StartCoroutine(WaitForPlayerLeave(player.transform));
    }

    IEnumerator WaitForPlayerLeave(Transform player)
    {
        // PlayerInteractComponent.InteractRadius(4f)보다 조금 큰 값으로 체크
        const float leaveRadius = 5f;
        while (Vector3.Distance(transform.position, player.position) < leaveRadius)
            yield return null;
        _waitingForLeave = false;
    }
}
