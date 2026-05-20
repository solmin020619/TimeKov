using UnityEngine;

public class PlayerInteractComponent : MonoBehaviour
{
    [Header("Settings")]
    public float InteractRadius = 2f;

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
        // Idle·Move·Run 상태에서만 허용
        if (_player.Skill.IsExecuting) return;
        if (_player.Movement.IsJumping) return;
        if (_player.Dash.IsDashing) return;
        if (_player.Stat.IsDead) return;
        if (_player.Stat.IsHurt) return;

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

        closest?.Interact(_player);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, InteractRadius);
    }
}