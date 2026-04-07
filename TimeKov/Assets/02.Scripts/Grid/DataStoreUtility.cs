using System.Collections.Generic;

public static class DataStoreUtility
{
    public static List<RecipeRow> GetRecipesByFacilityId(int facilityId)
    {
        List<RecipeRow> result = new List<RecipeRow>();

        foreach (var kv in DataStore.RecipeById)
        {
            RecipeRow recipe = kv.Value;

            if (recipe.facilityId == facilityId)
                result.Add(recipe);
        }

        return result;
    }

    public static FacilityLevelRow GetFacilityLevelRow(int facilityId, int level)
    {
        List<FacilityLevelRow> levels = DataStore.GetFacilityLevels(facilityId);

        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i].level == level)
                return levels[i];
        }

        return null;
    }
}