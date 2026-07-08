using UnityEngine;

/// <summary>
/// 조종석 비상 제어 장치. 플레이어가 F키로 상호작용 가능.
/// PressKeyObjective(F)가 InputBus를 통해 자동으로 퀘스트 완료를 잡아내므로
/// 여기서는 시각/상태 피드백만 처리한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EmergencyConsole : MonoBehaviour, IInteractable
{
    [Tooltip("활성화 후 켜질 VFX/UI 오브젝트 (선택)")]
    [SerializeField] GameObject activatedEffect;

    [Tooltip("상호작용 전 보여줄 힌트 오브젝트 (\"[F] 비상 제어 장치\" 라벨 등, 선택)")]
    [SerializeField] GameObject interactHint;

    bool _activated;

    public bool CanInteract => !_activated;

    void OnEnable()  => ShowHint(false);
    void OnDisable() => ShowHint(false);

    public void Interact(Player player)
    {
        if (_activated) return;
        _activated = true;

        ShowHint(false);

        if (activatedEffect != null)
            activatedEffect.SetActive(true);

        // InputBus.RaiseKeyDown(F)는 PlayerMovementWatcher가 같은 프레임에
        // 자동으로 발화하므로 PressKeyObjective(F)가 별도 코드 없이 완료된다.
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) ShowHint(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) ShowHint(false);
    }

    void ShowHint(bool show)
    {
        if (interactHint != null) interactHint.SetActive(show && !_activated);
    }
}
