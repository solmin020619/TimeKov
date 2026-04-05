using UnityEngine;

/// <summary>
/// 게임 시작 시 DataStore를 로드하고 검증하는 부트 스크립트
/// 빈 GameObject에 붙여서 사용
/// </summary>
public class DataBoot : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log("=== Data Load Start ===");

        // 모든 CSV 로드
        DataStore.LoadAll();

        // 로드된 개수 확인용 로그
        Debug.Log($"Item Count: {DataStore.ItemById.Count}");
        Debug.Log($"FactoryItem Count: {DataStore.FactoryItemById.Count}");
        Debug.Log($"Weapon Count: {DataStore.WeaponByItemId.Count}");
        Debug.Log($"Equipment Count: {DataStore.EquipmentByItemId.Count}");
        Debug.Log($"Facility Count: {DataStore.FacilityById.Count}");
        Debug.Log($"Recipe Count: {DataStore.RecipeById.Count}");
        Debug.Log($"Mining Vein Type Count: {DataStore.MiningOutputsByVeinType.Count}");
        Debug.Log($"Drop Group Count: {DataStore.DropRowsByDropId.Count}");

        // 참조 무결성 검사
        DataStoreValidator.ValidateAll();

        Debug.Log("=== Data Load End ===");
    }
}