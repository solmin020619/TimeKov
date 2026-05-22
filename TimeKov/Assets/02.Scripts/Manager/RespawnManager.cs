using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    public Transform RespawnPoint;
    public float RespawnDelay = 2f;

    public Player _player;

    [Header("아이템 드롭")]
    [Tooltip("죽으면 인벤토리의 모든 아이템을 필드에 드롭합니다")]
    public bool DropItemsOnDeath = true;

    [Tooltip("드롭할 LootBox 프리팹 (Inspector에서 연결)")]
    [SerializeField] private GameObject lootBoxPrefab;

    // 리스폰 중복 실행 방지 플래그
    // OnDead 이벤트가 짧은 시간에 여러 번 호출돼도 코루틴이 하나만 돌도록 보장
    private bool _isRespawning = false;

    void Start()
    {
        _player.Stat.OnDead += HandleDead;
    }

    void HandleDead()
    {
        if (_isRespawning) return;   // 이미 리스폰 진행 중이면 무시
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        _isRespawning = true;

        // 1. 죽는 애니메이션 + 이동 잠금
        _player.Anim.PlayDie();
        _player.Movement.LockMovement(true);

        // 2. 사망 즉시 아이템 드롭
        if (DropItemsOnDeath)
            DropInventoryItems();

        yield return new WaitForSeconds(RespawnDelay);

        // 2. 스탯 회복 (IsDead → false)
        _player.Stat.Respawn();

        // 3. 리지드바디 위치 직접 설정
        //    transform.position 대신 rb.position 사용:
        //    Unity Physics가 transform 변경을 즉시 동기화하지 않는 경우가 있어
        //    rb.position은 물리 엔진의 내부 위치를 직접 갱신하므로 신뢰할 수 있음
        if (RespawnPoint == null)
        {
            Debug.LogError("[RespawnManager] RespawnPoint가 null입니다! Inspector에서 연결하세요.");
        }
        else
        {
            var rb = _player.GetComponent<Rigidbody>();
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position        = RespawnPoint.position;  // 물리 공간 위치 직접 갱신
        }

        // 4. 애니메이션 리셋 (Base Layer → Blend Tree, Action Layer → Empty)
        _player.Anim.ResetToIdle();

        // 5. 이동 잠금 해제
        _player.Movement.LockMovement(false);

        // 6. 스킬 쿨다운타이머 초기화
        _player.Skill.ResetAll();

        _isRespawning = false;
    }

    // 인벤토리 아이템 전부 드롭
    void DropInventoryItems()
    {
        var inv = InventoryManager.Instance;
        if (inv == null)
        {
            Debug.LogWarning("[RespawnManager] InventoryManager.Instance 없음 — 드롭 스킵");
            return;
        }

        // lootBoxPrefab 미설정 시 씬의 EnemyDropOnDeath에서 자동으로 가져옴
        if (lootBoxPrefab == null)
        {
            var enemyDrop = Object.FindAnyObjectByType<EnemyDropOnDeath>();
            if (enemyDrop != null)
            {
                lootBoxPrefab = enemyDrop.BoxPrefab;
                Debug.Log("[RespawnManager] lootBoxPrefab 자동 탐색 완료 → EnemyDropOnDeath.BoxPrefab 사용");
            }
        }

        if (lootBoxPrefab == null)
        {
            Debug.LogWarning("[RespawnManager] lootBoxPrefab을 찾을 수 없음 — Inspector에서 직접 연결하세요.");
            return;
        }

        // 인벤토리 전체 수거 & 클리어
        List<(int itemId, int amount)> items = inv.TakeAll();
        if (items.Count == 0) return;   // 빈 인벤토리면 드롭 없음

        // 플레이어 발 위치에 LootBox 스폰
        Vector3 spawnPos = _player.transform.position + Vector3.up * 0.2f;
        var go = Instantiate(lootBoxPrefab, spawnPos, Quaternion.identity);

        var lootBox = go.GetComponentInChildren<LootBox>(true);
        if (lootBox != null)
            lootBox.Initialize(items);
        else
            Debug.LogWarning("[RespawnManager] 생성된 LootBox 프리팹에 LootBox 컴포넌트가 없습니다.");

        Debug.Log($"[RespawnManager] 사망 드롭: {items.Count}종 아이템 드롭 완료");
    }

    void OnDestroy() => _player.Stat.OnDead -= HandleDead;
}