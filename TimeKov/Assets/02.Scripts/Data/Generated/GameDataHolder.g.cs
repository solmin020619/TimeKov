// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System.Collections.Generic;

public partial class GameDataHolder
{
    public DataHolder<ItemDataSheetData> ItemData { get; } = new DataHolder<ItemDataSheetData>();
    public DataHolder<ConsumableEffectSheetData> ConsumableEffect { get; } = new DataHolder<ConsumableEffectSheetData>();
    public DataHolder<FacilityDataSheetData> FacilityData { get; } = new DataHolder<FacilityDataSheetData>();
    public DataHolder<FacilityLevelDataSheetData> FacilityLevelData { get; } = new DataHolder<FacilityLevelDataSheetData>();
    public DataHolder<RecipeDataSheetData> RecipeData { get; } = new DataHolder<RecipeDataSheetData>();
    public DataHolder<RecipeInputDataSheetData> RecipeInputData { get; } = new DataHolder<RecipeInputDataSheetData>();
    public DataHolder<DropTableSheetData> DropTable { get; } = new DataHolder<DropTableSheetData>();
    public DataHolder<CoreLevelDataSheetData> CoreLevelData { get; } = new DataHolder<CoreLevelDataSheetData>();

    partial void LoadAll(Dictionary<string, CsvTable> tables)
    {
        if (tables.TryGetValue("ItemData", out var itemDataTable))
            foreach (var pair in ItemDataParser.Parse(itemDataTable))
                ItemData.Add(pair.Key, pair.Value);
        if (tables.TryGetValue("ConsumableEffect", out var consumableEffectTable))
            foreach (var pair in ConsumableEffectParser.Parse(consumableEffectTable))
                ConsumableEffect.Add(pair.Key, pair.Value);
        if (tables.TryGetValue("FacilityData", out var facilityDataTable))
            foreach (var pair in FacilityDataParser.Parse(facilityDataTable))
                FacilityData.Add(pair.Key, pair.Value);
        if (tables.TryGetValue("FacilityLevelData", out var facilityLevelDataTable))
            foreach (var pair in FacilityLevelDataParser.Parse(facilityLevelDataTable))
                FacilityLevelData.Add(pair.Key, pair.Value);
        if (tables.TryGetValue("RecipeData", out var recipeDataTable))
            foreach (var pair in RecipeDataParser.Parse(recipeDataTable))
                RecipeData.Add(pair.Key, pair.Value);
        if (tables.TryGetValue("RecipeInputData", out var recipeInputDataTable))
            foreach (var pair in RecipeInputDataParser.Parse(recipeInputDataTable))
                RecipeInputData.Add(pair.Key, pair.Value);
        if (tables.TryGetValue("DropTable", out var dropTableTable))
            foreach (var pair in DropTableParser.Parse(dropTableTable))
                DropTable.Add(pair.Key, pair.Value);
        if (tables.TryGetValue("CoreLevelData", out var coreLevelDataTable))
            foreach (var pair in CoreLevelDataParser.Parse(coreLevelDataTable))
                CoreLevelData.Add(pair.Key, pair.Value);
    }

    partial void ClearAll()
    {
        ItemData.Clear();
        ConsumableEffect.Clear();
        FacilityData.Clear();
        FacilityLevelData.Clear();
        RecipeData.Clear();
        RecipeInputData.Clear();
        DropTable.Clear();
        CoreLevelData.Clear();
    }
}
