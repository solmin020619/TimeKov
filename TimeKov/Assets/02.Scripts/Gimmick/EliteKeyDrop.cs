using System.Collections.Generic;
using UnityEngine;

// ── 정예 처치 → 열쇠 100% 드랍 ────────────────────────────────────────────────
// 정예(엘리트) 몬스터가 죽으면 '퍼즐 열쇠' 아이템을 확정으로 떨군다.
//   기존 확률 드랍(EnemyDropOnDeath, DropTable)과 달리 퍼즐 필수템이라 반드시 나오게 한다.
//   드랍 상자는 기존 LootBox 프리팹을 그대로 재사용 → 줍는 흐름/연출은 일반 드랍과 동일.
//
//   ★스폰 포인트에서 나오는 '정예 프리팹' 인스턴스에 이 컴포넌트를 붙인다(EnemyHealth 필요).
//     같은 프리팹으로 스폰되는 몹은 전부 열쇠를 떨구니, 정예 전용 프리팹에만 붙일 것.
//   플레이어가 이미 열쇠를 갖고 있으면(재처치/중복) 기본적으로 또 떨구지 않는다(skipIfPlayerHasKey).
[RequireComponent(typeof(EnemyHealth))]
public class EliteKeyDrop : MonoBehaviour
{
    [Header("열쇠 드랍")]
    [Tooltip("떨굴 열쇠 아이템 ID(퍼즐 전용 키). 자물쇠(KeyLock)의 Key Item Id 와 같아야 한다.")]
    [SerializeField] private int keyItemId;
    [Tooltip("떨굴 개수. 보통 1.")]
    [Min(1)] [SerializeField] private int keyCount = 1;

    [Tooltip("스폰할 드랍 상자 프리팹(LootBox 포함). 일반 몹 드랍과 같은 프리팹을 쓰면 된다.")]
    [SerializeField] private GameObject boxPrefab;
    [Tooltip("죽은 자리에서 위로 띄우는 높이(m).")]
    [SerializeField] private float spawnHeightOffset = 0.5f;

    [Tooltip("체크: 플레이어가 이미 이 열쇠를 갖고 있으면 다시 안 떨군다(중복 방지).")]
    [SerializeField] private bool skipIfPlayerHasKey = true;

    private EnemyHealth _health;
    private bool _dropped;   // 이 인스턴스가 이미 떨궜는지(사망 이벤트 중복 방지)

    private void Awake() => _health = GetComponent<EnemyHealth>();

    private void OnEnable()  { if (_health != null) _health.OnDeath += HandleDeath; }
    private void OnDisable() { if (_health != null) _health.OnDeath -= HandleDeath; }

    private void HandleDeath()
    {
        if (_dropped) return;
        _dropped = true;

        if (boxPrefab == null) { Debug.LogWarning("[EliteKeyDrop] boxPrefab 이 비어 있다.", this); return; }
        if (keyItemId <= 0)    { Debug.LogWarning("[EliteKeyDrop] keyItemId 가 지정되지 않았다.", this); return; }

        // 이미 보유 중이면(재처치 등) 중복 지급 방지.
        if (skipIfPlayerHasKey && InventoryManager.Instance != null &&
            InventoryManager.Instance.GetTotalItemCount(keyItemId) > 0)
            return;

        Vector3 pos = transform.position + Vector3.up * spawnHeightOffset;
        var go = Instantiate(boxPrefab, pos, Quaternion.identity);
        var box = go.GetComponentInChildren<LootBox>();
        if (box != null) box.Initialize(new List<(int, int)> { (keyItemId, keyCount) });
        else Debug.LogWarning("[EliteKeyDrop] 스폰한 상자에서 LootBox 를 못 찾았다.", this);
    }
}
