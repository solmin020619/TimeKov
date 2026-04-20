using System.Collections.Generic;
using UnityEngine;

// DataStore에 로드된 데이터들의 참조 무결성을 검사하는 검증기
public static class DataStoreValidator
{
    /// <summary>
    /// 전체 검증 실행
    /// 하나라도 실패하면 false 반환
    /// </summary>
    public static bool ValidateAll()
    {
        bool ok = true;

        ok &= ValidateWeapons();
        ok &= ValidateEquipment();
        ok &= ValidateFactoryItems();
        ok &= ValidateFacilities();
        ok &= ValidateRecipes();
        ok &= ValidateMining();
        ok &= ValidateDrops();

        if (ok)
            Debug.Log("[DataStoreValidator] All validations passed.");
        else
            Debug.LogError("[DataStoreValidator] Validation failed.");

        return ok;
    }

    /// <summary>
    /// 무기 데이터 검증
    /// - 무기 itemId가 itemData에 존재하는지
    /// - itemType이 weapon인지
    /// - ammoItemId가 itemData에 존재하는지
    /// </summary>
    private static bool ValidateWeapons()
    {
        bool ok = true;

        foreach (var kv in DataStore.WeaponByItemId)
        {
            WeaponRow weapon = kv.Value;

            if (!DataStore.ItemById.TryGetValue(weapon.itemId, out var item))
            {
                Debug.LogError($"[Weapon] Missing itemData itemId={weapon.itemId}");
                ok = false;
                continue;
            }

            if (item.itemType != "weapon")
            {
                Debug.LogError($"[Weapon] itemType mismatch itemId={weapon.itemId}, itemType={item.itemType}");
                ok = false;
            }

            if (!DataStore.ItemById.ContainsKey(weapon.ammoItemId))
            {
                Debug.LogError($"[Weapon] Missing ammo itemId={weapon.ammoItemId}, weaponItemId={weapon.itemId}");
                ok = false;
            }
        }

        return ok;
    }

    /// <summary>
    /// 장비 데이터 검증
    /// 현재 기준:
    /// helmet / vest -> itemType = armor
    /// bag / backpack -> itemType = backpack
    /// </summary>
    private static bool ValidateEquipment()
    {
        bool ok = true;

        foreach (var kv in DataStore.EquipmentByItemId)
        {
            EquipmentRow equip = kv.Value;

            if (!DataStore.ItemById.TryGetValue(equip.itemId, out var item))
            {
                Debug.LogError($"[Equipment] Missing itemData itemId={equip.itemId}");
                ok = false;
                continue;
            }

            if (equip.equipType == "helmet" || equip.equipType == "vest")
            {
                if (item.itemType != "armor")
                {
                    Debug.LogError($"[Equipment] armor mismatch itemId={equip.itemId}, equipType={equip.equipType}, itemType={item.itemType}");
                    ok = false;
                }
            }
            else if (equip.equipType == "bag" || equip.equipType == "backpack")
            {
                if (item.itemType != "backpack")
                {
                    Debug.LogError($"[Equipment] backpack mismatch itemId={equip.itemId}, equipType={equip.equipType}, itemType={item.itemType}");
                    ok = false;
                }
            }
            else
            {
                Debug.LogError($"[Equipment] Unknown equipType={equip.equipType}, itemId={equip.itemId}");
                ok = false;
            }
        }

        return ok;
    }

    /// <summary>
    /// 공장 아이템 검증
    /// itemData와 factoryItemData의 중복 정보가 일치하는지 확인
    /// </summary>
    private static bool ValidateFactoryItems()
    {
        bool ok = true;

        foreach (var kv in DataStore.FactoryItemById)
        {
            FactoryItemRow factory = kv.Value;

            if (!DataStore.ItemById.TryGetValue(factory.itemId, out var item))
            {
                Debug.LogError($"[FactoryItem] Missing itemData itemId={factory.itemId}");
                ok = false;
                continue;
            }

            if (factory.factoryUsage != item.factoryUsage)
            {
                Debug.LogError($"[FactoryItem] factoryUsage mismatch itemId={factory.itemId}, factory={factory.factoryUsage}, item={item.factoryUsage}");
                ok = false;
            }

            if (factory.isProcessed != item.isProcessed)
            {
                Debug.LogError($"[FactoryItem] isProcessed mismatch itemId={factory.itemId}, factory={factory.isProcessed}, item={item.isProcessed}");
                ok = false;
            }

            if (factory.isFinalProduct != item.isFinalProduct)
            {
                Debug.LogError($"[FactoryItem] isFinalProduct mismatch itemId={factory.itemId}, factory={factory.isFinalProduct}, item={item.isFinalProduct}");
                ok = false;
            }
        }

        return ok;
    }

    /// <summary>
    /// 시설 데이터 검증
    /// - facilityLevel이 facilityData와 연결되는지
    /// - maxLevel과 실제 레벨 수가 맞는지
    /// - 레벨이 1부터 연속적인지
    /// </summary>
    private static bool ValidateFacilities()
    {
        bool ok = true;

        foreach (var kv in DataStore.FacilityById)
        {
            FacilityRow facility = kv.Value;
            List<FacilityLevelRow> levels = DataStore.GetFacilityLevels(facility.facilityId);

            if (facility.maxLevel <= 0)
            {
                Debug.LogError($"[Facility] Invalid maxLevel facilityId={facility.facilityId}");
                ok = false;
            }

            if (facility.maxLevel > 1)
            {
                if (levels.Count == 0)
                {
                    Debug.LogError($"[Facility] Missing facilityLevel rows facilityId={facility.facilityId}");
                    ok = false;
                    continue;
                }

                if (levels.Count != facility.maxLevel)
                {
                    Debug.LogError($"[Facility] level count mismatch facilityId={facility.facilityId}, expected={facility.maxLevel}, actual={levels.Count}");
                    ok = false;
                }

                for (int i = 0; i < levels.Count; i++)
                {
                    int expectedLevel = i + 1;

                    if (levels[i].level != expectedLevel)
                    {
                        Debug.LogError($"[Facility] non-contiguous level facilityId={facility.facilityId}, expected={expectedLevel}, actual={levels[i].level}");
                        ok = false;
                    }
                }
            }
        }

        // facilityLevel 쪽에만 있고 facilityData에는 없는 ID가 있는지도 검사
        foreach (var kv in DataStore.FacilityLevelsByFacilityId)
        {
            if (!DataStore.FacilityById.ContainsKey(kv.Key))
            {
                Debug.LogError($"[FacilityLevel] Missing facilityData facilityId={kv.Key}");
                ok = false;
            }
        }

        return ok;
    }

    /// <summary>
    /// 레시피 데이터 검증
    /// - recipe가 시설과 연결되는지
    /// - input/output이 존재하는지
    /// - input/output 아이템이 실제 itemData에 존재하는지
    /// </summary>
    private static bool ValidateRecipes()
    {
        bool ok = true;

        foreach (var kv in DataStore.RecipeById)
        {
            RecipeRow recipe = kv.Value;

            if (!DataStore.FacilityById.ContainsKey(recipe.facilityId))
            {
                Debug.LogError($"[Recipe] Missing facilityId={recipe.facilityId}, recipeId={recipe.recipeId}");
                ok = false;
            }

            List<RecipeInputRow> inputs = DataStore.GetRecipeInputs(recipe.recipeId);
            List<RecipeOutputRow> outputs = DataStore.GetRecipeOutputs(recipe.recipeId);

            if (inputs.Count == 0)
            {
                Debug.LogError($"[Recipe] Missing input rows recipeId={recipe.recipeId}");
                ok = false;
            }

            if (outputs.Count == 0)
            {
                Debug.LogError($"[Recipe] Missing output rows recipeId={recipe.recipeId}");
                ok = false;
            }

            for (int i = 0; i < inputs.Count; i++)
            {
                if (!DataStore.ItemById.ContainsKey(inputs[i].inputItemId))
                {
                    Debug.LogError($"[RecipeInput] Missing itemId={inputs[i].inputItemId}, recipeId={recipe.recipeId}");
                    ok = false;
                }
            }

            for (int i = 0; i < outputs.Count; i++)
            {
                if (!DataStore.ItemById.ContainsKey(outputs[i].outputItemId))
                {
                    Debug.LogError($"[RecipeOutput] Missing itemId={outputs[i].outputItemId}, recipeId={recipe.recipeId}");
                    ok = false;
                }
            }
        }

        return ok;
    }

    /// <summary>
    /// 채굴 데이터 검증
    /// - facilityId가 존재하는지
    /// - outputItemId가 존재하는지
    /// </summary>
    private static bool ValidateMining()
    {
        bool ok = true;

        foreach (var kv in DataStore.MiningOutputsByVeinType)
        {
            List<MiningOutputRow> rows = kv.Value;

            for (int i = 0; i < rows.Count; i++)
            {
                MiningOutputRow row = rows[i];

                if (!DataStore.FacilityById.ContainsKey(row.facilityId))
                {
                    Debug.LogError($"[Mining] Missing facilityId={row.facilityId}, veinType={row.veinType}");
                    ok = false;
                }

                if (!DataStore.ItemById.ContainsKey(row.outputItemId))
                {
                    Debug.LogError($"[Mining] Missing outputItemId={row.outputItemId}, veinType={row.veinType}");
                    ok = false;
                }
            }
        }

        return ok;
    }

    /// <summary>
    /// 드랍 데이터 검증
    /// - itemId 존재 여부
    /// - min/max 범위 정상 여부
    /// - 같은 dropId 내부에서 pickCount/sourceType/sourceId가 일관적인지
    /// </summary>
    private static bool ValidateDrops()
    {
        bool ok = true;

        foreach (var kv in DataStore.DropRowsByDropId)
        {
            int dropId = kv.Key;
            List<DropRow> rows = kv.Value;

            if (rows.Count == 0)
                continue;

            int pickCount = rows[0].pickCount;
            string sourceType = rows[0].sourceType;
            string sourceId = rows[0].sourceId;

            for (int i = 0; i < rows.Count; i++)
            {
                DropRow row = rows[i];

                if (!DataStore.ItemById.ContainsKey(row.itemId))
                {
                    Debug.LogError($"[Drop] Missing itemId={row.itemId}, dropId={dropId}");
                    ok = false;
                }

                if (row.minCount > row.maxCount)
                {
                    Debug.LogError($"[Drop] Invalid count range dropId={dropId}, itemId={row.itemId}, min={row.minCount}, max={row.maxCount}");
                    ok = false;
                }

                if (row.pickCount != pickCount)
                {
                    Debug.LogError($"[Drop] pickCount mismatch inside same dropId={dropId}");
                    ok = false;
                }

                if (row.sourceType != sourceType || row.sourceId != sourceId)
                {
                    Debug.LogError($"[Drop] source mismatch inside same dropId={dropId}");
                    ok = false;
                }
            }
        }

        return ok;
    }
}
