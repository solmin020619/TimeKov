using UnityEngine;

public class DevSpawnCorpseBox : MonoBehaviour
{
    public KeyCode spawnKey = KeyCode.K;

    [Header("시체 박스 프리팹 (CorpseBox_Test)")]
    public GameObject corpseBoxPrefab;

    [Header("레이캐스트 기준 (보통 MainCamera)")]
    public Camera rayCamera;

    [Header("레이 거리")]
    public float rayDistance = 20f;

    private void Awake()
    {
        if (rayCamera == null) rayCamera = Camera.main;
        Debug.Log("[DevSpawnCorpseBox] Awake OK - K로 몬스터 사망 테스트");
    }

    private void Update()
    {
        if (Input.GetKeyDown(spawnKey))
        {
            TryKillAndSpawnCorpse();
        }
    }

    private void TryKillAndSpawnCorpse()
    {
        if (corpseBoxPrefab == null)
        {
            Debug.LogError("[DevSpawnCorpseBox] Corpse Box Prefab 비어있음 (CorpseBox_Test 넣어야 함)");
            return;
        }

        if (rayCamera == null)
        {
            Debug.LogError("[DevSpawnCorpseBox] rayCamera 없음 (MainCamera 지정 필요)");
            return;
        }

        Ray ray = new Ray(rayCamera.transform.position, rayCamera.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance))
        {
            Debug.Log("[DevSpawnCorpseBox] 레이가 아무것도 안맞음");
            return;
        }

        var target = hit.collider.transform.root;

        if (!target.CompareTag("Enemy"))
        {
            Debug.Log($"[DevSpawnCorpseBox] 맞춘 대상이 Enemy 태그가 아님: {target.name}");
            return;
        }

        // 1) 시체 박스 생성
        Vector3 spawnPos = target.position;
        var corpse = Instantiate(corpseBoxPrefab, spawnPos, Quaternion.identity);
        corpse.name = $"CorpseBox_{target.name}";

        // 2) 몬스터쪽 MonsterLoot 설정을 시체로 복사(있으면)
        var srcLoot = target.GetComponent<MonsterLoot>();
        var dstLoot = corpse.GetComponent<MonsterLoot>();
        if (srcLoot != null && dstLoot != null)
        {
            dstLoot.monsterType = srcLoot.monsterType;
            dstLoot.tableId = srcLoot.tableId;
            dstLoot.minRoll = srcLoot.minRoll;
            dstLoot.maxRoll = srcLoot.maxRoll;
            dstLoot.dropDb = srcLoot.dropDb;
            dstLoot.itemDb = srcLoot.itemDb;
        }

        // 3) 몬스터 “사망 처리” (임시)
        target.gameObject.SetActive(false);

        Debug.Log($"[DevSpawnCorpseBox] K로 몬스터 처리 + 시체박스 생성 완료: {target.name} -> {corpse.name}");
    }
}
