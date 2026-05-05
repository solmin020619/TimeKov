using System.Collections;
using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    public Transform RespawnPoint;          // 기지 내 리스폰 위치
    public float RespawnDelay = 2f;     // 죽음 연출 대기 시간

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
        // 죽음 연출 대기 (애니메이션, 페이드 등 추가 예정)
        yield return new WaitForSeconds(RespawnDelay);

        // 플레이어 기지 위치로 이동
        var rb = _player.GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        _player.transform.position = RespawnPoint.position;

        // 스탯 초기화
        _player.Stat.Respawn();
    }

    void OnDestroy() => _player.Stat.OnDead -= HandleDead;
}