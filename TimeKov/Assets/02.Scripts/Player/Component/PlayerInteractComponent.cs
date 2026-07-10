using UnityEngine;

public class PlayerInteractComponent : MonoBehaviour
{
    [Header("Settings")]
    public float InteractRadius = 4f;
    [Tooltip("인터랙션 감지 레이어 마스크. 설정하지 않으면 모든 콜라이더를 검사해 성능 낭비 발생")]
    public LayerMask InteractLayer = ~0; // 기본값: 전체 (Inspector에서 반드시 지정 권장)

    // IInteractHint 방식: 오브젝트 자신이 World Space 힌트를 켜고 끔
    IInteractable _lastHinted;

    private Player _player;

    void Awake()
    {
        _player = GetComponent<Player>();
    }

    void Update()
    {
        // 매 프레임 nearest 파악 → 힌트 갱신
        IInteractable nearest = FindClosest();
        UpdateHint(nearest);

        // 상호작용 입력은 Idle/Move/Run 상태에서만
        if (_player.Skill.IsExecuting) return;
        if (_player.Movement.IsJumping) return;
        if (_player.Dash != null && _player.Dash.IsDashing) return;
        if (_player.Stat.IsDead) return;
        if (_player.Stat.IsHurt) return;

        bool fPressed = _player.Input.InteractPressed;
        bool gPressed = _player.Input.InstantPressed;
        if (!fPressed && !gPressed) return;

        if (nearest == null) return;

        // G: 즉시완료 (가능한 대상만)
        if (gPressed && nearest is IInstantInteractable inst && inst.CanInstantComplete(_player))
        {
            inst.OnInstantComplete(_player);
            return;
        }

        // F: 일반 상호작용
        if (fPressed)
            nearest.Interact(_player);
    }

    void UpdateHint(IInteractable nearest)
    {
        if (nearest == _lastHinted) return;

        if (_lastHinted is IInteractHint prev) prev.ShowHint(false);
        if (nearest    is IInteractHint cur)  cur.ShowHint(true);

        _lastHinted = nearest;
    }

    IInteractable FindClosest()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position, InteractRadius, InteractLayer, QueryTriggerInteraction.Collide);

        IInteractable closest = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<IInteractable>(out var interactable)) continue;
            if (!interactable.CanInteract) continue;

            float dist = (transform.position - hit.transform.position).sqrMagnitude;
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = interactable;
            }
        }

        return closest;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, InteractRadius);
    }
}
