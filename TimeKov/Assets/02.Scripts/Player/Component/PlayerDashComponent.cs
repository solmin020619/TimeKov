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

        // 제자리(이동입력 없음)에서도 우클릭하면 정면으로 대시한다 (#20).
        // GetDashDirection()이 입력 없으면 transform.forward 를 반환하므로 방향은 안전.
        if (IsOnCooldown) return;
        if (_player.Stat.CurrentStamina < DashCost) return;
        if (_player.Skill.IsExecuting) return;

        StartCoroutine(DashRoutine());
    }

    IEnumerator DashRoutine()
    {
        IsDashing = true;

        _player.Stat.UseStamina(DashCost);
        _cooldownTimer = DashCooldown;
        // LockMovement이 X/Z velocity를 0으로 박지만, 같은 frame 직후 dashForce 적용 → 정상
        // HandleSlopeStabilize는 actualXZ < 0.5f 가드가 있어서 dashForce(15)는 보존됨
        _player.Movement.LockMovement(true);

        Vector3 dashDir = _player.Movement.GetDashDirection();

        _rb.linearVelocity = new Vector3(
            dashDir.x * DashForce,
            _rb.linearVelocity.y,
            dashDir.z * DashForce
        );

        _player.Anim.PlayDash(dashDir);
        _player.Audio?.PlayDash();

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

        // [미끄러짐 수정] 대시 물리 이동이 끝나면 대시 끝자세(자세 낮춘) 클립이 길게
        // 재생되며 미끄러지던 문제 — 클립 끝까지 기다리지 않고 즉시 이동으로 부드럽게 전환.
        // (기존엔 WaitUntil(!IsInActionAnim) 로 끝자세 클립이 끝날 때까지 잠가서 미끄러짐 발생.)
        _rb.linearVelocity = new Vector3(0, _rb.linearVelocity.y, 0);
        _player.Anim.EndDash();   // 대시 클립 -> 이동(Blend Tree) 짧게 블렌딩 (즉시 컷 아님)
        _player.Movement.LockMovement(false, applyPostUnlockDelay: false);
        IsDashing = false;
    }
}