using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 튜토리얼 기획서 기준 SO 자산 21 QuestSO + 24 ObjectiveSO + 1 CategorySO + 1 TutorialSO 자동 생성.
/// 기획서 21번 (Time 회복 확인)은 Q1=(a) 결정에 따라 생략 → 총 20 Quest.
/// 메뉴: Tools > Quest > Generate Tutorial Assets
/// </summary>
public static class TutorialAssetBuilder
{
    const string RootFolder = "Assets/06.ScriptableObjects/Quest";
    const string ObjectivesFolder = RootFolder + "/Objectives/Tutorial";
    const string QuestsFolder = RootFolder + "/Quests/Tutorial";
    const string CategoriesFolder = RootFolder + "/Categories";
    const string TutorialsFolder = RootFolder + "/Tutorials";

    // FacilityData 시트 확정값 (BioExtractor=1, BioInjector=6). ItemData 시트와도 일치 확인됨.
    const int BioExtractorId = 1;          // 생체 추출기 (3x3, 입력2/출력1)
    const int BioInjectorId = 6;           // 생체 주입기 (5x5, 입력2/출력2)
    const int ItemLeafId = 1101;           // 변이 식물 잎사귀 (Common, RawMaterial)
    const int ItemSapId = 1102;            // 끈적한 수액 (Common, RawMaterial)
    const int ItemMedicalGelId = 1201;     // 의료용 겔 (Advanced, ProcessedTier1) — R1201: 1101x2+1102x1 → 1201
    const int ItemHealingAmpouleId = 4101; // 소형 나노 힐링 앰플 (Advanced, TacticalConsumable) — R4101: 1201x1 → 4101, Heal/Time/Flat/+50

    [MenuItem("Tools/Quest/Generate Tutorial Assets")]
    public static void Generate()
    {
        bool ok = EditorUtility.DisplayDialog(
            "튜토리얼 자산 생성",
            $"기획서 기준 SO 자산 일괄 생성:\n" +
            $"  - Objective: {ObjectivesFolder}\n" +
            $"  - Quest: {QuestsFolder}\n" +
            $"  - Category: {CategoriesFolder}\n" +
            $"  - Tutorial: {TutorialsFolder}\n\n" +
            $"기존 자산은 덮어씁니다 (같은 파일명).",
            "생성", "취소");
        if (!ok) return;

        EnsureFolder("Assets/06.ScriptableObjects", "Quest");
        EnsureFolder(RootFolder, "Objectives");
        EnsureFolder(RootFolder + "/Objectives", "Tutorial");
        EnsureFolder(RootFolder, "Quests");
        EnsureFolder(RootFolder + "/Quests", "Tutorial");
        EnsureFolder(RootFolder, "Categories");
        EnsureFolder(RootFolder, "Tutorials");

        var quests = new List<QuestSO>();

        // 1. 몸을 움직여보기 (MoveDistance 3f)
        quests.Add(BuildQuest("quest_tutorial_01_move", "몸을 움직여보기",
            CreateMoveDistance("obj_move_distance", "WASD로 이동해보세요.", 3f)));

        // 2. 점프해보기 (Space)
        quests.Add(BuildQuest("quest_tutorial_02_jump", "점프해보기",
            CreatePressKey("obj_press_jump", "Space로 점프하세요.", KeyCode.Space, 1)));

        // 3. 빠르게 움직여보기 (Mouse1 = 우클릭 대시)
        quests.Add(BuildQuest("quest_tutorial_03_dash", "빠르게 움직여보기",
            CreatePressKey("obj_press_dash", "우클릭으로 대시하세요.", KeyCode.Mouse1, 1)));

        // 4. 코어 확인하기 (Trigger)
        quests.Add(BuildQuest("quest_tutorial_04_core", "코어 확인하기",
            CreateReachTrigger("obj_reach_core", "중앙 코어를 확인하세요.", "core_trigger")));

        // 5. 기지 출구로 이동하기 (Trigger)
        quests.Add(BuildQuest("quest_tutorial_05_exit", "기지 출구로 이동하기",
            CreateReachTrigger("obj_reach_base_exit", "기지 출구로 이동하세요.", "base_exit_trigger")));

        // 6. 외부 구역 진입하기 (Trigger)
        quests.Add(BuildQuest("quest_tutorial_06_outside", "외부 구역 진입하기",
            CreateReachTrigger("obj_enter_outside_area", "외부 구역으로 진입하세요.", "outside_area_trigger")));

        // 7. 공격해보기 (Mouse0)
        quests.Add(BuildQuest("quest_tutorial_07_attack", "공격해보기",
            CreatePressKey("obj_press_attack", "좌클릭으로 공격하세요.", KeyCode.Mouse0, 1)));

        // 8. 첫 전투 (EnemyKill tutorial_enemy)
        quests.Add(BuildQuest("quest_tutorial_08_kill_enemy", "첫 전투",
            CreateEnemyKill("obj_kill_tutorial_enemy", "외부 구역의 적을 처치하세요.", "tutorial_enemy", 1)));

        // 9. 드랍 아이템 획득하기 (1101 x2, 1102 x1) — 두 Objective
        quests.Add(BuildQuest("quest_tutorial_09_pickup_drop", "드랍 아이템 획득하기",
            CreateItemAcquire("obj_pickup_tutorial_drop_leaf", "<color=#FFCC00>변이 식물 잎사귀</color>를 획득하세요.", ItemLeafId, 2),
            CreateItemAcquire("obj_pickup_tutorial_drop_sap", "<color=#FFCC00>끈적한 수액</color>을 획득하세요.", ItemSapId, 1)));

        // 10. 기지로 복귀하기 (Trigger)
        quests.Add(BuildQuest("quest_tutorial_10_return", "기지로 복귀하기",
            CreateReachTrigger("obj_return_base", "기지로 복귀하세요.", "base_return_trigger")));

        // 11. 설비 설치 구역으로 이동하기 (Trigger)
        quests.Add(BuildQuest("quest_tutorial_11_build_area", "설비 설치 구역으로 이동하기",
            CreateReachTrigger("obj_reach_facility_build_area", "설비 설치 구역으로 이동하세요.", "facility_build_area_trigger")));

        // 12. 생체 추출기 설치하기 (FacilityPlace BioExtractor)
        quests.Add(BuildQuest("quest_tutorial_12_place_bio_extractor", "생체 추출기 설치하기",
            CreateFacilityPlace("obj_place_bio_extractor", "<color=#FFCC00>생체 추출기</color>를 설치하세요.", BioExtractorId, 1)));

        // 13. 생체 추출기와 상호작용하기 (F)
        quests.Add(BuildQuest("quest_tutorial_13_interact_bio_extractor", "생체 추출기와 상호작용하기",
            CreatePressKey("obj_interact_bio_extractor", "F키로 생체 추출기와 상호작용하세요.", KeyCode.F, 1)));

        // 14. 재료 투입하기 (FacilityInput 1101 x2, 1102 x1) — 두 Objective
        quests.Add(BuildQuest("quest_tutorial_14_input_raw_materials", "재료 투입하기",
            CreateFacilityInput("obj_input_bio_extractor_leaf", "<color=#FFCC00>변이 식물 잎사귀</color>를 생체 추출기에 투입하세요.", BioExtractorId, ItemLeafId, 2),
            CreateFacilityInput("obj_input_bio_extractor_sap", "<color=#FFCC00>끈적한 수액</color>을 생체 추출기에 투입하세요.", BioExtractorId, ItemSapId, 1)));

        // 15. 의료용 겔 출력 확인하기 (FacilityProcessComplete 1201)
        quests.Add(BuildQuest("quest_tutorial_15_output_medical_gel", "의료용 겔 출력 확인하기",
            CreateFacilityProcessComplete("obj_output_medical_gel", "<color=#FFCC00>의료용 겔</color> 출력을 확인하세요.", BioExtractorId, ItemMedicalGelId, 1)));

        // 16. 생체 주입기 설치하기 (FacilityPlace BioInjector)
        quests.Add(BuildQuest("quest_tutorial_16_place_bio_injector", "생체 주입기 설치하기",
            CreateFacilityPlace("obj_place_bio_injector", "<color=#FFCC00>생체 주입기</color>를 설치하세요.", BioInjectorId, 1)));

        // 17. 생체 주입기와 상호작용하기 (F)
        quests.Add(BuildQuest("quest_tutorial_17_interact_bio_injector", "생체 주입기와 상호작용하기",
            CreatePressKey("obj_interact_bio_injector", "F키로 생체 주입기와 상호작용하세요.", KeyCode.F, 1)));

        // 18. 의료용 겔 투입하기 (FacilityInput 1201)
        quests.Add(BuildQuest("quest_tutorial_18_input_medical_gel", "의료용 겔 투입하기",
            CreateFacilityInput("obj_input_medical_gel", "<color=#FFCC00>의료용 겔</color>을 생체 주입기에 투입하세요.", BioInjectorId, ItemMedicalGelId, 1)));

        // 19. 소형 나노 힐링 앰플 출력 확인하기 (FacilityProcessComplete 4101)
        quests.Add(BuildQuest("quest_tutorial_19_output_healing_ampoule", "소형 나노 힐링 앰플 출력 확인하기",
            CreateFacilityProcessComplete("obj_output_healing_ampoule", "<color=#FFCC00>소형 나노 힐링 앰플</color> 출력을 확인하세요.", BioInjectorId, ItemHealingAmpouleId, 1)));

        // 20. 소형 나노 힐링 앰플 사용하기 (ItemUse 4101)
        quests.Add(BuildQuest("quest_tutorial_20_use_healing_ampoule", "소형 나노 힐링 앰플 사용하기",
            CreateItemUse("obj_use_healing_ampoule", "<color=#FFCC00>소형 나노 힐링 앰플</color>을 사용하세요.", ItemHealingAmpouleId, 1)));

        // 21번 (Time 회복 확인)은 Q1=(a) 결정에 따라 생략

        // CategorySO
        var cat = ScriptableObject.CreateInstance<CategorySO>();
        cat.id = "tutorial_main";
        cat.title = "튜토리얼";
        cat.quests = quests.ToArray();
        AssetDatabase.CreateAsset(cat, $"{CategoriesFolder}/Cat_Tutorial_Main.asset");

        // TutorialSO
        var tut = ScriptableObject.CreateInstance<TutorialSO>();
        tut.savePrefix = "tutorial";
        tut.categories = new[] { cat };
        AssetDatabase.CreateAsset(tut, $"{TutorialsFolder}/Tutorial_Main.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[TutorialAssetBuilder] 자산 생성 완료.\n" +
            $"  - Quest: {quests.Count}개\n" +
            $"  - Category: 1개 ({CategoriesFolder}/Cat_Tutorial_Main.asset)\n" +
            $"  - Tutorial: 1개 ({TutorialsFolder}/Tutorial_Main.asset)\n" +
            $"\n" +
            $"다음 단계:\n" +
            $"1. World_HJW의 QuestSystem GameObject의 QuestManager.tutorial 슬롯에 새 Tutorial_Main.asset 드래그\n" +
            $"2. 씬에 Trigger 5개 배치 (core/base_exit/outside_area/base_return/facility_build_area)\n" +
            $"3. PlayerMovementWatcher.watchedKeys에 F 추가 (없으면)\n" +
            $"4. BioExtractor/BioInjector facilityId가 실제 데이터와 다르면 인스펙터에서 조정\n" +
            $"5. tutorial_enemy ID로 적 Prefab 셋업 + EnemyDropOnDeath의 sourceId 매칭");
    }

    // ----- QuestSO 빌더 -----
    static QuestSO BuildQuest(string id, string title, params ObjectiveSO[] objectives)
    {
        var q = ScriptableObject.CreateInstance<QuestSO>();
        q.id = id;
        q.title = title;
        q.objectives = objectives;
        AssetDatabase.CreateAsset(q, $"{QuestsFolder}/{id}.asset");
        return q;
    }

    // ----- Objective 빌더 -----
    static MoveDistanceObjective CreateMoveDistance(string name, string label, float distance)
    {
        var o = ScriptableObject.CreateInstance<MoveDistanceObjective>();
        o.label = label;
        o.requiredDistance = distance;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static PressKeyObjective CreatePressKey(string name, string label, KeyCode key, int count)
    {
        var o = ScriptableObject.CreateInstance<PressKeyObjective>();
        o.label = label;
        o.key = key;
        o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static ReachTriggerObjective CreateReachTrigger(string name, string label, string triggerId)
    {
        var o = ScriptableObject.CreateInstance<ReachTriggerObjective>();
        o.label = label;
        o.targetTriggerId = triggerId;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static EnemyKillObjective CreateEnemyKill(string name, string label, string enemyId, int count)
    {
        var o = ScriptableObject.CreateInstance<EnemyKillObjective>();
        o.label = label;
        o.enemyId = enemyId;
        o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static ItemAcquireObjective CreateItemAcquire(string name, string label, int itemId, int count)
    {
        var o = ScriptableObject.CreateInstance<ItemAcquireObjective>();
        o.label = label;
        o.itemId = itemId;
        o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static FacilityPlaceObjective CreateFacilityPlace(string name, string label, int facilityId, int count)
    {
        var o = ScriptableObject.CreateInstance<FacilityPlaceObjective>();
        o.label = label;
        o.facilityId = facilityId;
        o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static FacilityInputObjective CreateFacilityInput(string name, string label, int facilityId, int itemId, int count)
    {
        var o = ScriptableObject.CreateInstance<FacilityInputObjective>();
        o.label = label;
        o.facilityId = facilityId;
        o.inputItemId = itemId;
        o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static FacilityProcessCompleteObjective CreateFacilityProcessComplete(string name, string label, int facilityId, int outputItemId, int count)
    {
        var o = ScriptableObject.CreateInstance<FacilityProcessCompleteObjective>();
        o.label = label;
        o.facilityId = facilityId;
        o.outputItemId = outputItemId;
        o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static ItemUseObjective CreateItemUse(string name, string label, int itemId, int count)
    {
        var o = ScriptableObject.CreateInstance<ItemUseObjective>();
        o.label = label;
        o.itemId = itemId;
        o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(parent, name);
    }
}
