using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

// 모든 CSV 데이터를 로드하고, 게임 중 쉽게 꺼내 쓸 수 있도록 Dictionary 캐시를 보관하는 정적 저장소
public static class DataStore
{
    // itemId -> ItemRow
    public static Dictionary<int, ItemRow> ItemById { get; private set; } = new Dictionary<int, ItemRow>();

    // itemId -> FactoryItemRow
    public static Dictionary<int, FactoryItemRow> FactoryItemById { get; private set; } = new Dictionary<int, FactoryItemRow>();

    // itemId -> WeaponRow
    public static Dictionary<int, WeaponRow> WeaponByItemId { get; private set; } = new Dictionary<int, WeaponRow>();

    // itemId -> EquipmentRow
    public static Dictionary<int, EquipmentRow> EquipmentByItemId { get; private set; } = new Dictionary<int, EquipmentRow>();

    // facilityId -> FacilityRow
    public static Dictionary<int, FacilityRow> FacilityById { get; private set; } = new Dictionary<int, FacilityRow>();

    // facilityId -> 해당 시설의 레벨 데이터 리스트
    public static Dictionary<int, List<FacilityLevelRow>> FacilityLevelsByFacilityId { get; private set; } = new Dictionary<int, List<FacilityLevelRow>>();

    // recipeId -> RecipeRow
    public static Dictionary<int, RecipeRow> RecipeById { get; private set; } = new Dictionary<int, RecipeRow>();

    // recipeId -> 입력 재료 목록
    public static Dictionary<int, List<RecipeInputRow>> RecipeInputsByRecipeId { get; private set; } = new Dictionary<int, List<RecipeInputRow>>();

    // recipeId -> 결과물 목록
    public static Dictionary<int, List<RecipeOutputRow>> RecipeOutputsByRecipeId { get; private set; } = new Dictionary<int, List<RecipeOutputRow>>();

    // veinType -> 채굴 결과 목록
    public static Dictionary<string, List<MiningOutputRow>> MiningOutputsByVeinType { get; private set; } = new Dictionary<string, List<MiningOutputRow>>();

    // dropId -> 드랍 그룹 행 목록
    public static Dictionary<int, List<DropRow>> DropRowsByDropId { get; private set; } = new Dictionary<int, List<DropRow>>();

    // 전체 로드 완료 여부
    public static bool IsLoaded { get; private set; }

    // 모든 테이블을 한 번에 로드
    public static void LoadAll()
    {
        ItemById = LoadItemTable("Tables/itemDataTable");
        FactoryItemById = LoadFactoryItemTable("Tables/factoryItemDataTable");
        WeaponByItemId = LoadWeaponTable("Tables/weaponDataTable");
        EquipmentByItemId = LoadEquipmentTable("Tables/equipmentDataTable");
        FacilityById = LoadFacilityTable("Tables/facilityDataTable");
        FacilityLevelsByFacilityId = LoadFacilityLevelTable("Tables/facilityLevelDataTable");
        RecipeById = LoadRecipeTable("Tables/recipeDataTable");
        RecipeInputsByRecipeId = LoadRecipeInputTable("Tables/recipeInputDataTable");
        RecipeOutputsByRecipeId = LoadRecipeOutputTable("Tables/recipeOutputDataTable");
        MiningOutputsByVeinType = LoadMiningOutputTable("Tables/miningOutputTable");
        DropRowsByDropId = LoadDropTable("Tables/dropTable");

        IsLoaded = true;
    }

    // itemId로 ItemRow 조회
    public static ItemRow GetItem(int itemId)
    {
        ItemById.TryGetValue(itemId, out var row);
        return row;
    }

    // itemId로 FactoryItemRow 조회
    public static FactoryItemRow GetFactoryItem(int itemId)
    {
        FactoryItemById.TryGetValue(itemId, out var row);
        return row;
    }

    // itemId로 WeaponRow 조회
    public static WeaponRow GetWeapon(int itemId)
    {
        WeaponByItemId.TryGetValue(itemId, out var row);
        return row;
    }

    // itemId로 EquipmentRow 조회
    public static EquipmentRow GetEquipment(int itemId)
    {
        EquipmentByItemId.TryGetValue(itemId, out var row);
        return row;
    }

    // facilityId로 FacilityRow 조회
    public static FacilityRow GetFacility(int facilityId)
    {
        FacilityById.TryGetValue(facilityId, out var row);
        return row;
    }

    // recipeId로 RecipeRow 조회
    public static RecipeRow GetRecipe(int recipeId)
    {
        RecipeById.TryGetValue(recipeId, out var row);
        return row;
    }

    /// recipeId로 입력 재료 목록 조회
    // 없으면 빈 리스트 반환
    public static List<RecipeInputRow> GetRecipeInputs(int recipeId)
    {
        if (RecipeInputsByRecipeId.TryGetValue(recipeId, out var list))
            return list;

        return new List<RecipeInputRow>();
    }

    // recipeId로 결과물 목록 조회
    // 없으면 빈 리스트 반환
    public static List<RecipeOutputRow> GetRecipeOutputs(int recipeId)
    {
        if (RecipeOutputsByRecipeId.TryGetValue(recipeId, out var list))
            return list;

        return new List<RecipeOutputRow>();
    }

    // facilityId로 레벨 정보 목록 조회
    // 없으면 빈 리스트 반환
    public static List<FacilityLevelRow> GetFacilityLevels(int facilityId)
    {
        if (FacilityLevelsByFacilityId.TryGetValue(facilityId, out var list))
            return list;

        return new List<FacilityLevelRow>();
    }

    // veinType으로 채굴 결과 목록 조회
    // 없으면 빈 리스트 반환
    public static List<MiningOutputRow> GetMiningOutputs(string veinType)
    {
        if (MiningOutputsByVeinType.TryGetValue(veinType, out var list))
            return list;

        return new List<MiningOutputRow>();
    }

    // dropId로 드랍 그룹 행 목록 조회
    // 없으면 빈 리스트 반환
    public static List<DropRow> GetDropRows(int dropId)
    {
        if (DropRowsByDropId.TryGetValue(dropId, out var list))
            return list;

        return new List<DropRow>();
    }

    // 개별 테이블 로더
    // 각 CSV 파일을 읽어서 해당 Row 클래스로 변환한 뒤 Dictionary로 만든다.

    private static Dictionary<int, ItemRow> LoadItemTable(string resourcePath)
    {
        var rows = ParseCsv(resourcePath, cols => new ItemRow
        {
            itemId = ToInt(cols[0]),
            itemName = cols[1],
            itemType = cols[2],
            subType = cols[3],
            description = cols[4],
            iconKey = cols[5],
            stackable = ToInt(cols[6]),
            maxStack = ToInt(cols[7]),
            weight = ToFloat(cols[8]),
            sellValue = ToInt(cols[9]),
            rarityTier = ToInt(cols[10]),
            isDroppable = ToInt(cols[11]),
            isCraftable = ToInt(cols[12]),
            factoryUsage = cols[13],
            isProcessed = ToInt(cols[14]),
            isFinalProduct = ToInt(cols[15]),
        });

        return rows.ToDictionary(x => x.itemId, x => x);
    }

    private static Dictionary<int, FactoryItemRow> LoadFactoryItemTable(string resourcePath)
    {
        var rows = ParseCsv(resourcePath, cols => new FactoryItemRow
        {
            itemId = ToInt(cols[0]),
            factoryUsage = cols[1],
            powerMinutes = ToInt(cols[2]),
            tradeValue = ToInt(cols[3]),
            isProcessed = ToInt(cols[4]),
            isFinalProduct = ToInt(cols[5]),
        });

        return rows.ToDictionary(x => x.itemId, x => x);
    }

    private static Dictionary<int, WeaponRow> LoadWeaponTable(string resourcePath)
    {
        var rows = ParseCsv(resourcePath, cols => new WeaponRow
        {
            itemId = ToInt(cols[0]),
            ammoItemId = ToInt(cols[1]),
            magazineSize = ToInt(cols[2]),
            isAutomatic = ToInt(cols[3]),
            damage = ToFloat(cols[4]),
            fireRate = ToFloat(cols[5]),
            reloadTime = ToFloat(cols[6]),
            effectiveRange = ToFloat(cols[7]),
            useRecoilPattern = ToInt(cols[8]),
            randomRecoilAngle = ToFloat(cols[9]),
            recoilResetTime = ToFloat(cols[10]),
            pelletsPerShot = ToInt(cols[11]),
            spreadAngle = ToFloat(cols[12]),
        });

        return rows.ToDictionary(x => x.itemId, x => x);
    }

    private static Dictionary<int, EquipmentRow> LoadEquipmentTable(string resourcePath)
    {
        var rows = ParseCsv(resourcePath, cols => new EquipmentRow
        {
            itemId = ToInt(cols[0]),
            equipType = cols[1],
            equipLevel = ToInt(cols[2]),
            defense = ToInt(cols[3]),
            addSlotCount = ToInt(cols[4]),
            durability = ToInt(cols[5]),
        });

        return rows.ToDictionary(x => x.itemId, x => x);
    }

    private static Dictionary<int, FacilityRow> LoadFacilityTable(string resourcePath)
    {
        var rows = ParseCsv(resourcePath, cols => new FacilityRow
        {
            facilityId = ToInt(cols[0]),
            facilityName = cols[1],
            facilityType = cols[2],
            gridW = ToInt(cols[3]),
            gridH = ToInt(cols[4]),
            inputCount = ToInt(cols[5]),
            outputCount = ToInt(cols[6]),
            requiresPower = ToInt(cols[7]),
            canRotate = ToInt(cols[8]),
            installRule = cols[9],
            maxLevel = ToInt(cols[10]),
        });

        return rows.ToDictionary(x => x.facilityId, x => x);
    }

    private static Dictionary<int, List<FacilityLevelRow>> LoadFacilityLevelTable(string resourcePath)
    {
        var rows = ParseCsv(resourcePath, cols => new FacilityLevelRow
        {
            facilityId = ToInt(cols[0]),
            level = ToInt(cols[1]),
            processTimeMultiplier = ToFloat(cols[2]),
            powerEfficiencyMultiplier = ToFloat(cols[3]),
            capacityBonus = ToInt(cols[4]),
        });

        return rows
            .GroupBy(x => x.facilityId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.level).ToList());
    }

    private static Dictionary<int, RecipeRow> LoadRecipeTable(string resourcePath)
    {
        var rows = ParseCsv(resourcePath, cols => new RecipeRow
        {
            recipeId = ToInt(cols[0]),
            facilityId = ToInt(cols[1]),
            craftTechTier = ToInt(cols[2]),
            craftTime = ToFloat(cols[3]),
            powerCost = ToInt(cols[4]),
            recipeGroup = cols[5],
        });

        return rows.ToDictionary(x => x.recipeId, x => x);
    }

    private static Dictionary<int, List<RecipeInputRow>> LoadRecipeInputTable(string resourcePath)
    {
        var rows = ParseCsv(resourcePath, cols => new RecipeInputRow
        {
            recipeId = ToInt(cols[0]),
            inputItemId = ToInt(cols[1]),
            inputCount = ToInt(cols[2]),
        });

        return rows
            .GroupBy(x => x.recipeId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private static Dictionary<int, List<RecipeOutputRow>> LoadRecipeOutputTable(string resourcePath)
    {
        var rows = ParseCsv(resourcePath, cols => new RecipeOutputRow
        {
            recipeId = ToInt(cols[0]),
            outputItemId = ToInt(cols[1]),
            outputCount = ToInt(cols[2]),
        });

        return rows
            .GroupBy(x => x.recipeId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private static Dictionary<string, List<MiningOutputRow>> LoadMiningOutputTable(string resourcePath)
    {
        var rows = ParseCsv(resourcePath, cols => new MiningOutputRow
        {
            veinType = cols[0],
            facilityId = ToInt(cols[1]),
            outputItemId = ToInt(cols[2]),
            outputCount = ToInt(cols[3]),
            baseCycleTime = ToFloat(cols[4]),
        });

        return rows
            .GroupBy(x => x.veinType)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private static Dictionary<int, List<DropRow>> LoadDropTable(string resourcePath)
    {
        var rows = ParseCsv(resourcePath, cols => new DropRow
        {
            dropId = ToInt(cols[0]),
            sourceType = cols[1],
            sourceId = cols[2],
            itemId = ToInt(cols[3]),
            dropTier = ToInt(cols[4]),
            dropWeight = ToInt(cols[5]),
            minCount = ToInt(cols[6]),
            maxCount = ToInt(cols[7]),
            pickCount = ToInt(cols[8]),
        });

        return rows
            .GroupBy(x => x.dropId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    // 공통 CSV 파서 현재 기준: 1행 = 헤더 2행부터 = 실제 데이터
    // 따라서 인덱스 1부터 시작한다.
    private static List<T> ParseCsv<T>(string resourcePath, Func<string[], T> converter)
    {
        TextAsset csv = Resources.Load<TextAsset>(resourcePath);

        if (csv == null)
        {
            Debug.LogError("[DataStore] CSV not found: " + resourcePath);
            return new List<T>();
        }

        string text = csv.text.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = text.Split('\n');

        List<T> result = new List<T>();

        // lines[0]은 헤더이므로 건너뛰고
        // lines[1]부터 실제 데이터로 처리
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] cols = SplitCsvLine(line);

            try
            {
                result.Add(converter(cols));
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataStore] Parse failed | path={resourcePath} | line={i + 1} | raw={line}\n{e}");
            }
        }

        return result;
    }

    // CSV 한 줄을 쉼표 기준으로 분리한다.
    // 큰따옴표 안의 쉼표도 최대한 안전하게 처리한다.
    private static string[] SplitCsvLine(string line)
    {
        List<string> result = new List<string>();
        StringBuilder sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                // 큰따옴표 안에서 "" 형태는 실제 " 문자 하나로 본다.
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        result.Add(sb.ToString().Trim());
        return result.ToArray();
    }

    // 문자열을 int로 변환
    // 빈칸이면 0 처리
    private static int ToInt(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return 0;

        return int.Parse(s, CultureInfo.InvariantCulture);
    }

    // 문자열을 float로 변환
    // 빈칸이면 0 처리
    private static float ToFloat(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return 0f;

        return float.Parse(s, CultureInfo.InvariantCulture);
    }
}