using UnityEngine;

public class PlayerInteractComponent : MonoBehaviour
{
    [Header("Settings")]
    public float InteractRadius = 2f;   // 상호작용 감지 반경 (m)

    private Player _player;

    void Awake()
    {
        _player = GetComponent<Player>();
    }

    void Update()
    {
        if (_player.Input.InteractPressed) TryInteract();
    }

    void TryInteract()
    {
        // 반경 내 상호작용 가능한 오브젝트 탐색
        Collider[] hits = Physics.OverlapSphere(
            transform.position, InteractRadius);

        IInteractable closest = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<IInteractable>(out var interactable)) continue;
            if (!interactable.CanInteract) continue;

            float dist = Vector3.Distance(transform.position, hit.transform.position);

            if (dist < closestDist)
            {
                closestDist = dist;
                closest = interactable;
            }
        }

        // 가장 가까운 오브젝트와 상호작용
        closest?.Interact(_player);
    }

    // 상호작용 범위 시각화
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, InteractRadius);
    }
}