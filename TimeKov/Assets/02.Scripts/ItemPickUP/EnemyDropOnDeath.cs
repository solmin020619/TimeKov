using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyDropOnDeath : MonoBehaviour
{
    [Header("Drop Table")]
    [Tooltip("이 몬스터가 죽을 때 사용할 드롭 테이블")]
    public ItemDropTable table;

    [Header("Spawn Offset")]
    [Tooltip("아이템 스폰 시 몬스터 위치에서 위로 띄울 값")]
    public float spawnHeightOffset = 1.0f;

    private EnemyHealth _health;

    void Awake()
    {
        _health = GetComponent<EnemyHealth>();
    }

    void OnEnable()
    {
        if (_health != null)
            _health.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        if (_health != null)
            _health.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        if (table == null) return;
        if (table.defaultDropPrefab == null && !HasAnyOverridePrefab()) return;

        Vector3 spawnPos = transform.position + Vector3.up * spawnHeightOffset;

        if (table.deathVFX != null)
            Instantiate(table.deathVFX, spawnPos, Quaternion.identity);

        List<ItemDropTable.DropEntry> rolled = RollEntries();
        for (int i = 0; i < rolled.Count; i++)
        {
            SpawnOne(rolled[i], spawnPos);
        }
    }

    private bool HasAnyOverridePrefab()
    {
        if (table == null || table.entries == null) return false;
        for (int i = 0; i < table.entries.Count; i++)
        {
            if (table.entries[i] != null && table.entries[i].overridePrefab != null)
                return true;
        }
        return false;
    }

    private List<ItemDropTable.DropEntry> RollEntries()
    {
        List<ItemDropTable.DropEntry> result = new List<ItemDropTable.DropEntry>();
        if (table.entries == null || table.entries.Count == 0) return result;

        int rollCount = Random.Range(
            Mathf.Max(0, table.minRolls),
            Mathf.Max(0, table.maxRolls) + 1
        );
        if (rollCount <= 0) return result;

        List<ItemDropTable.DropEntry> pool = new List<ItemDropTable.DropEntry>(table.entries);

        for (int i = 0; i < rollCount; i++)
        {
            if (pool.Count == 0) break;

            ItemDropTable.DropEntry picked = WeightedPick(pool);
            if (picked == null) break;

            result.Add(picked);

            if (!table.allowDuplicateRolls)
                pool.Remove(picked);
        }

        return result;
    }

    private ItemDropTable.DropEntry WeightedPick(List<ItemDropTable.DropEntry> pool)
    {
        float total = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && pool[i].weight > 0f)
                total += pool[i].weight;
        }
        if (total <= 0f) return null;

        float r = Random.value * total;
        float acc = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] == null || pool[i].weight <= 0f) continue;
            acc += pool[i].weight;
            if (r <= acc) return pool[i];
        }
        return pool[pool.Count - 1];
    }

    private void SpawnOne(ItemDropTable.DropEntry entry, Vector3 spawnPos)
    {
        GameObject prefab = entry.overridePrefab != null
                          ? entry.overridePrefab
                          : table.defaultDropPrefab;
        if (prefab == null) return;

        int amount = Random.Range(
            Mathf.Max(1, entry.minCount),
            Mathf.Max(1, entry.maxCount) + 1
        );

        GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);

        if (table.perItemVFX != null)
            Instantiate(table.perItemVFX, spawnPos, Quaternion.identity);

        DroppedItem dropped = go.GetComponent<DroppedItem>();
        if (dropped == null) dropped = go.AddComponent<DroppedItem>();

        dropped.Initialize(entry.itemId, amount, entry.canPickup, table.pickupDelay);

        Vector3 randomDir = new Vector3(
            Random.Range(-1f, 1f),
            0f,
            Random.Range(-1f, 1f)
        ).normalized;

        Vector3 force = Vector3.up * table.launchUpForce
                      + randomDir * table.launchSideForce;

        dropped.Launch(force, table.spinTorque, table.gravityScale);
    }
}
