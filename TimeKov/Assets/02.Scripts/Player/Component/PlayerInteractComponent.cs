using UnityEngine;

public class PlayerInteractComponent : MonoBehaviour
{
    [Header("Settings")]
    public float InteractRadius = 2f;
    [Tooltip("인터랙션 감지 레이어 마스크. 설정하지 않으면 모든 콜라이더를 검사해 성능 낭비 발생")]
    public LayerMask InteractLayer = ~0; // 기본값: 전체 (Inspector에서 반드시 지정 권장)

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
        // Idle��Move��Run ���¿����� ���
        if (_player.Skill.IsExecuting) return;
        if (_player.Movement.IsJumping) return;
        if (_player.Dash.IsDashing) return;
        if (_player.Stat.IsDead) return;
        if (_player.Stat.IsHurt) return;

        Collider[] hits = Physics.OverlapSphere(
            transform.position, InteractRadius, InteractLayer, QueryTriggerInteraction.Collide);

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

        closest?.Interact(_player);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, InteractRadius);
    }
}