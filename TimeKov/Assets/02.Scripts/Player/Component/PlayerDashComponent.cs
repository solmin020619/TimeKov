using System.Collections;
using UnityEngine;

public class PlayerDashComponent : MonoBehaviour
{
    [Header("Dash")]
    public float DashCost = 40f;    // 스태미나 소모량
    public float DashCooldown = 0.8f;   // 쿨타임 (초)
    public float DashForce = 15f;    // 대시 힘
    public float DashDuration = 0.2f;   // 대시 지속 시간 (초)

    private Player _player;
    private Rigidbody _rb;
    private float _cooldownTimer;   // 현재 쿨타임 잔여 시간

    public bool IsDashing { get; private set; }  // 대시 중 여부
    public bool IsOnCooldown => _cooldownTimer > 0; // 쿨타임 중 여부

    void Awake()
    {
        _player = GetComponent<Player>();
        _rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        TickCooldown();

        if (_player.Input.DashPressed) TryDash();
    }

    void TickCooldown()
    {
        if (_cooldownTimer > 0)
            _cooldownTimer -= Time.deltaTime;
    }

    void TryDash()
    {
        // 이동 입력 없으면 대시 불가
        if (_player.Input.MoveInput.magnitude < 0.1f) return;

        // 쿨타임 중이면 불가
        if (IsOnCooldown) return;

        // 스태미나 부족하면 불가
        if (_player.Stat.CurrentStamina < DashCost) return;

        // 스킬 실행 중이면 불가
        if (_player.Skill.IsExecuting) return;

        StartCoroutine(DashRoutine());
    }

    IEnumerator DashRoutine()
    {
        IsDashing = true;

        // 스태미나 소모
        _player.Stat.UseStamina(DashCost);

        // 쿨타임 시작
        _cooldownTimer = DashCooldown;

        // 이동 방향으로 대시
        _player.Movement.LockMovement(true);

        Vector3 dashDir = _player.Movement.GetDashDirection();
        _rb.linearVelocity = new Vector3(
            dashDir.x * DashForce,
            _rb.linearVelocity.y,
            dashDir.z * DashForce
        );

        // 애니메이션
        _player.Anim.PlayDash(dashDir);

        yield return new WaitForSeconds(DashDuration);

        // 대시 종료
        _rb.linearVelocity = new Vector3(0, _rb.linearVelocity.y, 0);
        _player.Movement.LockMovement(false);
        IsDashing = false;
    }
}