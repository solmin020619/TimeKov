using UnityEngine;

public class PlayerInteractComponent : MonoBehaviour
{
    [Header("Settings")]
    public float InteractRadius = 2f;
    [Tooltip("인터랙션 감지 레이어 마스크. 설정하지 않으면 모든 콜라이더를 검사해 성능 낭비 발생")]
    public LayerMask InteractLayer = ~0; // 기본값: 전체 (Inspector에서 반드시 지정 권장)

    private Player _player;

    // 홀드 진행 상태
    private IHoldInteractable _holdingTarget;
    private float _holdElapsed;

    void Awake()
    {
        _player = GetComponent<Player>();
    }

    void Update()
    {
        UpdateInteraction();
    }

    void UpdateInteraction()
    {
        // Idle/Move/Run 상태에서만 상호작용. 그 외 상태면 진행 중인 홀드 취소.
        if (_player.Skill.IsExecuting
            || _player.Movement.IsJumping
            || (_player.Dash != null && _player.Dash.IsDashing)
            || _player.Stat.IsDead
            || _player.Stat.IsHurt)
        {
            CancelHold();
            return;
        }

        IInteractable closest = FindClosest();

        // 홀드형 대상 (HoldDuration > 0) → 꾹 누르기 / G 즉시완료
        if (closest is IHoldInteractable hold && hold.HoldDuration > 0f)
        {
            // 즉시완료 (HP 등 비용 소모)
            if (_player.Input.InstantPressed && hold.CanInstantComplete(_player))
            {
                CancelHold();
                hold.OnInstantComplete(_player);
                return;
            }

            if (_player.Input.InteractHeld)
            {
                if (!ReferenceEquals(_holdingTarget, hold))
                {
                    CancelHold();
                    _holdingTarget = hold;
                    _holdElapsed = 0f;
                }

                _holdElapsed += Time.deltaTime;
                hold.OnHoldProgress(Mathf.Clamp01(_holdElapsed / hold.HoldDuration));

                if (_holdElapsed >= hold.HoldDuration)
                {
                    var done = _holdingTarget;
                    _holdingTarget = null;
                    _holdElapsed = 0f;
                    done.OnHoldComplete(_player);
                }
            }
            else
            {
                CancelHold();
            }
            return;
        }

        // 일반(즉시) 상호작용 — 기존처럼 F 누른 순간 1회
        CancelHold();
        if (_player.Input.InteractPressed)
            closest?.Interact(_player);
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

    void CancelHold()
    {
        if (_holdingTarget != null)
        {
            _holdingTarget.OnHoldCancel();
            _holdingTarget = null;
            _holdElapsed = 0f;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, InteractRadius);
    }
}