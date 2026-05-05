using System.Collections;
using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    public Transform RespawnPoint;
    public float RespawnDelay = 2f;

    private Player _player;

    void Start()
    {
        _player = FindAnyObjectByType<Player>();
        _player.Stat.OnDead += HandleDead;
    }

    void HandleDead()
    {
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        // 죽음 애니메이션 재생
        _player.Anim.PlayDie();

        // 이동 잠금
        _player.Movement.LockMovement(true);

        // 애니메이션 대기
        yield return new WaitForSeconds(RespawnDelay);


        // 기지로 이동
        var rb = _player.GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        _player.transform.position = RespawnPoint.position;

        // 애니메이션 초기화 -> Idle 상태로 복귀
        _player.Anim.ResetToIdle();

        // 이동 잠금 해제
        _player.Movement.LockMovement(false);
        // 스탯 초기화
        _player.Stat.Respawn();
    }

    void OnDestroy() => _player.Stat.OnDead -= HandleDead;
}