using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "Attack3Skill", menuName = "Skills/Attack3")]
public class Attack3Skill : ComboAttackBase
{
    [Header("Slash")]
    public float SlashForce = 12f;   // ���� ������ ��
    public float SlashDuration = 0.3f;  // ������ ���� �ð� (��)

    protected override float GetAnimDuration() => AnimDuration;
    public float AnimDuration = 1.0f;   // ��ü �ִϸ��̼� ���� (��)

    public override IEnumerator ExecuteRoutine(GameObject caster)
    {
        var anim = caster.GetComponent<PlayerAnimatorComponent>();
        var movement = caster.GetComponent<PlayerMovementComponent>();
        var rb = caster.GetComponent<Rigidbody>();

        // ���� �� �̵� ���
        movement.LockMovement(true);

        anim?.PlayAttack(ComboIndex);

        // ������ ����
        movement.StartSlash(SlashForce, SlashDuration);

        // ��ü �ִϸ��̼� ���� �̵� ��� ����
        yield return new WaitForSeconds(AnimDuration);

        OnAttackHit(caster);

        // ���� ������ ���� �ӵ� �ʱ�ȭ
        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);

        // �ִϸ��̼� ������ ���� �� �̵� ����
        movement.LockMovement(false);
    }

    public override void OnInterrupt(GameObject caster)
    {
        // 중단 시 슬래시 강제 종료 + 이동 잠금 해제
        var movement = caster.GetComponent<PlayerMovementComponent>();
        movement?.CancelSlash();        // _isSlashing 강제 초기화 (슬라이딩 방지)
        movement?.LockMovement(false);
    }
}