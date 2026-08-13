// =====================================================================
// ContentWiringCheck.cs
// 시트에 콘텐츠를 넣었는데 '받아 주는 쪽'을 빠뜨려 조용히 안 나오는 경우를 잡는다.
//
// [왜 필요한가]
//   설비나 아이템은 시트로 늘리는데, 그걸 받아 주는 것들(해금 표, 건축바 칸 수,
//   도감 보상 설정, 아이콘 파일)은 코드와 에셋에 따로 산다. 한쪽만 늘리면
//   게임은 멀쩡히 돌고 콘솔도 조용한데 그 콘텐츠만 안 나온다.
//
//   실제로 연마기를 넣었을 때 세 군데가 한꺼번에 어긋났다.
//     - 전송률 해금 표에 없어서 정식 플레이로는 영영 안 열렸다
//     - 도감 설비 목록이 1..8 로 박혀 있어 목록에 안 떴다
//     - 도감 레시피 보상 설정에 없어서 다 만들어도 보상이 0이었다
//   셋 다 에러 한 줄 없이 조용했다. 그래서 이 검사를 만든다.
//
// [로그 정책] 통과는 아무것도 찍지 않는다. 문제가 있을 때만 한 줄씩.
// 에디터에서만 돈다 - 데이터는 빌드와 같으므로 여기서 잡히면 빌드도 안전하다.
// =====================================================================

#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;

public static class ContentWiringCheck
{
    /// 설비 UI 의 재료 칸 수. 레시피 재료가 이보다 많으면 화면에서 겹친다.
    private const int MaxRecipeInputs = 2;

    public static void Run()
    {
        var holder = GameDataHolder.I;
        if (holder == null || holder.FacilityData == null) return;

        CheckRecipeInputCount();
        CheckFacilityWiring();
        CheckMonsterRegion();
    }

    // 재료가 3개 이상이면 설비 UI 에서 재료 칸이 겹쳐 보인다(칸이 2개뿐).
    private static void CheckRecipeInputCount()
    {
        var recipes = GameDataHolder.I.RecipeData;
        if (recipes == null) return;

        foreach (var r in recipes.All)
        {
            if (r == null) continue;
            var inputs = GameDataUtility.GetRecipeInputs(r.SheetId);
            if (inputs == null || inputs.Count <= MaxRecipeInputs) continue;

            Debug.LogError($"[배선검사] 레시피 '{r.SheetId}' 의 재료가 {inputs.Count}개다. " +
                           $"설비 UI 는 재료 칸이 {MaxRecipeInputs}개라 화면에서 겹친다. " +
                           "재료를 줄이거나, 중간 가공품을 하나 만들어 단계를 나눠라.");
        }
    }

    // 설비 하나가 실제로 손에 들어오려면 시트 말고도 세 군데가 맞아야 한다.
    private static void CheckFacilityWiring()
    {
        var unlockable = CollectUnlockableFacilityIds();

        foreach (var fd in GameDataHolder.I.FacilityData.All)
        {
            if (fd == null || !int.TryParse(fd.SheetId, out int id)) continue;
            string label = $"설비 {id}({fd.facilityName})";

            // 1) 건축바 칸 - buildSlot 이 칸 수를 넘으면 아이콘이 안 보인다(설치는 되는데 못 고른다).
            if (int.TryParse(fd.buildSlot, out int slot) && slot > FacilityUnlockManager.MaxSlots)
                Debug.LogError($"[배선검사] {label} 의 buildSlot 이 {slot} 인데 건축바 칸은 " +
                               $"{FacilityUnlockManager.MaxSlots}개다. 칸을 늘리거나 buildSlot 을 낮춰라.");

            // 2) 해금 경로 - 어디에도 없으면 정식 플레이에서 영영 안 열린다.
            //    (에디터는 백쿼트 전체해금 때문에 멀쩡해 보여서 더 늦게 발견된다.)
            if (!unlockable.Contains(id))
                Debug.LogError($"[배선검사] {label} 를 해금해 주는 곳이 없다. " +
                               "TransmissionManager 의 전송률 보상 표에 넣거나 튜토리얼 지급 목록에 넣어라.");

            // 3) 아이콘 - 없으면 건축바/도감/설비 UI 헤더가 빈칸이 된다.
            if (string.IsNullOrEmpty(fd.iconKey) || Resources.Load<Sprite>("Facilities/" + fd.iconKey) == null)
                Debug.LogWarning($"[배선검사] {label} 의 아이콘을 못 찾았다(iconKey='{fd.iconKey}'). " +
                                 "Resources/Facilities 에 파일이 있는지 확인해라.");

            // 4) 도감 레시피 마스터 보상 - 레시피가 있는 설비인데 개수가 비면 보상 박스가 아예 안 뜬다.
            var recipes = GameDataUtility.GetRecipesByFacilityId(id);
            bool hasReward = int.TryParse(fd.masterReward, out int rw) && rw > 0;
            if (recipes != null && recipes.Count > 0 && !hasReward)
                Debug.LogWarning($"[배선검사] {label} 는 레시피가 {recipes.Count}개인데 도감 마스터 보상이 비어 있다. " +
                                 "설비 시트의 masterReward 컬럼을 채워라.");
        }
    }

    // 몬스터 지역이 비면 도감 액자가 이름 규칙 폴백으로 칠해진다.
    // 이름에 지역이 없는 몹(본드래곤/자이언트웜/자폭거미)은 그 폴백에서 자연으로 잘못 잡힌다.
    private static void CheckMonsterRegion()
    {
        var table = GameDataHolder.I.MonsterStatData;
        if (table == null) return;

        foreach (var row in table.All)
        {
            if (row == null) continue;
            if (RegionPalette.TryFromSheet(row.SheetId, out _)) continue;

            Debug.LogWarning($"[배선검사] 몬스터 '{row.SheetId}'({row.monsterName}) 의 지역이 비어 있거나 " +
                             $"알 수 없는 값이다(region='{row.region}'). " +
                             "몬스터 시트에 자연/설원/사막/용암 중 하나를 적어라.");
        }
    }

    // 어떤 경로로든 손에 들어올 수 있는 설비 id 전부.
    private static HashSet<int> CollectUnlockableFacilityIds()
    {
        var set = new HashSet<int>();
        foreach (int id in TransmissionManager.TutorialGrantedFacilityIds) set.Add(id);
        foreach (int id in TransmissionManager.AllMilestoneFacilityIds()) set.Add(id);
        return set;
    }
}

#endif
