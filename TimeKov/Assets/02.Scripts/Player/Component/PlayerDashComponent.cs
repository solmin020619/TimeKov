using System.Collections;
using UnityEngine;

public class PlayerDashComponent : MonoBehaviour
{
    [Header("Dash")]
    public float DashCost = 40f;
    public float DashCooldown = 0.8f;
    public float DashForce = 15f;
    public float DashDuration = 0.2f;

    [Header("Dash VFX")]
    public GameObject DashVfxPrefab;
    public Vector3 DashVfxOffset = new Vector3(0f, 0.2f, 0f);
    public Vector3 DashVfxRotationOffset = Vector3.zero;
    public float DashVfxLifeTime = 1f;
    public bool DashVfxParentToPlayer = true;

    private Player _player;
    private Rigidbody _rb;
    private float _cooldownTimer;

    public bool IsDashing { get; private set; }
    public bool IsOnCooldown => _cooldownTimer > 0;

    void Awake()
    {
        _player = GetComponent<Player>();
        _rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        TickCooldown();

        if (_player.Input.DashPressed)
            TryDash();
    }

    void TickCooldown()
    {
        if (_cooldownTimer > 0)
            _cooldownTimer -= Time.deltaTime;
    }

    void TryDash()
    {
        // ������Dead��Hurt ���� ����
        if (_player.Movement.IsJumping) return;
        if (_player.Stat.IsDead) return;
        if (_player.Stat.IsHurt) return;

        if (_player.Input.MoveInput.magnitude < 0.1f) return;
        if (IsOnCooldown) return;
        if (_player.Stat.CurrentStamina < DashCost) return;
        if (_player.Skill.IsExecuting) return;

        StartCoroutine(DashRoutine());
    }

    IEnumerator DashRoutine()
    {
        IsDashing = true;
        _player.Movement.SetDashing(true);

        _player.Stat.UseStamina(DashCost);
        _cooldownTimer = DashCooldown;
        // preserveVelocity=true → LockMovement이 velocity 0으로 박지 않음 (직후 dashForce 적용 보존)
        _player.Movement.LockMovement(true, preserveVelocity: true);

        Vector3 dashDir = _player.Movement.GetDashDirection();

        _rb.linearVelocity = new Vector3(
            dashDir.x * DashForce,
            _rb.linearVelocity.y,
            dashDir.z * DashForce
        );

        _player.Anim.PlayDash(dashDir);

        // ��� ����Ʈ ����
        VfxUtils.SpawnAtCaster(
            DashVfxPrefab,
            gameObject,
            DashVfxOffset,
            DashVfxRotationOffset,
            DashVfxLifeTime,
            DashVfxParentToPlayer
        );

        yield return new WaitForSeconds(DashDuration);

        _rb.linearVelocity = new Vector3(0, _rb.linearVelocity.y, 0);
        _player.Movement.SetDashing(false);
        _player.Movement.LockMovement(false);
        IsDashing = false;
    }
}