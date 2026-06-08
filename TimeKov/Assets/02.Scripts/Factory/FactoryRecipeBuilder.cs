// =====================================================================
// FactoryRecipeBuilder.cs
// 시트 데이터(RecipeData + RecipeInputData) -> 런타임 FactoryRecipe 변환.
// ProcessingMachine 이 설치 시 FacilityId 로 호출해 인스펙터 하드코딩 대체.
// =====================================================================

using System.Collections.Generic;

namespace TIMEKOV.Factory
{
    public static class FactoryRecipeBuilder
    {
        // facilityId 의 모든 레시피를 FactoryRecipe 리스트로 빌드.
        public static List<FactoryRecipe> BuildForFacility(int facilityId)
        {
            var list = new List<FactoryRecipe>();
            foreach (var recipe in GameDataUtility.GetRecipesByFacilityId(facilityId))
            {
                var built = Build(recipe);
                if (built != null) list.Add(built);
            }
            return list;
        }

        // RecipeData 한 행 + 그 입력 재료들을 FactoryRecipe 로 변환.
        public static FactoryRecipe Build(RecipeDataSheetData recipe)
        {
            if (recipe == null) return null;

            string recipeId = recipe.SheetId;   // implicit -> string

            var fr = new FactoryRecipe
            {
                recipeId  = recipeId,
                craftTime = recipe.craftTime,
            };

            // 출력 (단일 산출물)
            int outputId = ParseId(recipe.outputItemId);
            fr.outputs = new[] { new FactorySlot { itemId = outputId, amount = recipe.outputCount } };

            // UI 표시 이름 = 산출물 아이템 이름 (없으면 recipeId)
            var outItem = GameDataUtility.GetItem(outputId);
            fr.recipeName = outItem != null ? outItem.itemName : recipeId;

            // 입력 재료 (GetRecipeInputs 가 복합키를 파싱해 (itemId, count) 로 반환)
            var inputs = new List<FactorySlot>();
            foreach (var (itemId, count) in GameDataUtility.GetRecipeInputs(recipeId))
                inputs.Add(new FactorySlot { itemId = itemId, amount = count });
            fr.inputs = inputs.ToArray();

            return fr;
        }

        private static int ParseId(string s)
            => int.TryParse(s, out int v) ? v : 0;
    }
}
