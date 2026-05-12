using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDropTable", menuName = "ItemPickUp/Drop Table")]
public class ItemDropTable : ScriptableObject
{
    [System.Serializable]
    public class DropEntry
    {
        [Tooltip("DataStore의 ItemRow id")]
        public int itemId;

        [Tooltip("뽑힐 확률 가중치 (상대값). 0이면 절대 안 나옴")]
        public float weight = 1f;

        [Tooltip("한 번 굴렸을 때 나오는 개수 최소")]
        public int minCount = 1;

        [Tooltip("한 번 굴렸을 때 나오는 개수 최대")]
        public int maxCount = 1;

        [Tooltip("월드에 떨어진 후 플레이어가 주울 수 있는지")]
        public bool canPickup = true;

        [Tooltip("기본 프리팹 대신 쓸 전용 프리팹 (null이면 ItemDropTable.defaultDropPrefab 사용)")]
        public GameObject overridePrefab;
    }

    [Header("Drop Pool")]
    [Tooltip("이 풀에서 weight 기반으로 무작위 추출")]
    public List<DropEntry> entries = new List<DropEntry>();

    [Header("Roll Count (종류 수)")]
    [Tooltip("한 번의 사망에서 굴리는 최소 횟수")]
    public int minRolls = 1;

    [Tooltip("한 번의 사망에서 굴리는 최대 횟수")]
    public int maxRolls = 1;

    [Tooltip("ON: 같은 entry가 여러 번 나올 수 있음 / OFF: 한 entry는 1회만")]
    public bool allowDuplicateRolls = true;

    [Header("Default Prefab")]
    [Tooltip("entry.overridePrefab이 비어있을 때 사용할 기본 드롭 프리팹 (예: TestItem.prefab)")]
    public GameObject defaultDropPrefab;

    [Header("VFX")]
    [Tooltip("몬스터 사망 위치에서 1회 재생할 VFX (null OK)")]
    public GameObject deathVFX;

    [Tooltip("각 아이템 드롭 위치에서 재생할 VFX (null OK)")]
    public GameObject perItemVFX;

    [Header("Launch (포물선)")]
    [Tooltip("위로 튀는 힘")]
    public float launchUpForce = 5f;

    [Tooltip("옆으로 흩어지는 힘 (0이면 수직만)")]
    public float launchSideForce = 2f;

    [Tooltip("아이템 회전 토크")]
    public float spinTorque = 3f;

    [Tooltip("아이템 Rigidbody에 적용할 중력 배율 (기본 1)")]
    public float gravityScale = 1f;

    [Header("Pickup")]
    [Tooltip("떨어진 직후 픽업 불가 시간 (포물선 끝나기 전 방지)")]
    public float pickupDelay = 0.5f;
}
