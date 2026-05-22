using System.Collections;
using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    public Transform RespawnPoint;
    public float RespawnDelay = 2f;

    public Player _player;

    void Start()
    {
        _player.Stat.OnDead += HandleDead;
    }

    void HandleDead()
    {
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        // 사망 애니메이션 재생
        _player.Anim.PlayDie();

        // 이동 잠금
        _player.Movement.LockMovement(true);

        yield return new WaitForSeconds(RespawnDelay);

        // 위치 초기화
        var rb = _player.GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        _player.transform.position = RespawnPoint.position;

        // 애니메이션 Idle 복귀
        _player.Anim.ResetToIdle();

        // 이동 잠금 해제
        _player.Movement.LockMovement(false);

        // 스탯 회복
        _player.Stat.Respawn();

        // 스킬 게이지·쿨타임 초기화 (3단계)
        _player.Skill.ResetAll();
    }

    void OnDestroy() => _player.Stat.OnDead -= HandleDead;
}