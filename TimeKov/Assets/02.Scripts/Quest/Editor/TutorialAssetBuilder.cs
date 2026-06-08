using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 튜토리얼 SO 자산 일괄 생성 (최종 시트 데이터 기준 전면 재작성).
/// 핵심 체인(추출기->배양기->회복앰플) + F해금/레일자동화/저장고-창고/코어강화 체험을
/// 엔드필드식 단계(병렬 묶음 + ContinueObjective 스포트라이트)로 구성.
///
/// 메뉴: Tools > Quest > Generate Tutorial Assets
/// 주의: Objective/Quest 폴더는 통째 삭제 후 재생성 → 인스펙터에서 다듬은 라벨은 사라짐.
///       Category/Tutorial 은 GUID 유지(씬 슬롯 안 끊김).
/// </summary>
public static class TutorialAssetBuilder
{
    const string RootFolder = "Assets/06.ScriptableObjects/Quest";
    const string ObjectivesFolder = RootFolder + "/Objectives/Tutorial";
    const string QuestsFolder = RootFolder + "/Quests/Tutorial";
    const string CategoriesFolder = RootFolder + "/Categories";
    const string TutorialsFolder = RootFolder + "/Tutorials";

    // ── 최종 FacilityData 시트 기준 ──────────────────────────────────────
    const int BioExtractorId = 1;   // 생체 추출기 (3x3)
    const int BioCultivatorId = 2;  // 생체 배양기 (5x5) — 옛 "생체 주입기" 대체
    const int StorageId = 8;        // 저장고 (창고 용량 제공)

    // ── 최종 ItemData 시트 기준 ──────────────────────────────────────────
    const int ItemSpiderVenom = 1102;  // 거미 독액 (드롭, 원료)
    const int ItemCorrosive   = 3101;  // 부식액 (드롭, 원료)
    const int ItemHealGel     = 1201;  // 회복 젤 (R1201 @추출기1: 1102+3101)
    const int ItemHealAmpoule = 5101;  // 초급 회복 앰플 (R5101 @배양기2: 1201)
    const int CoreKitId       = 6101;  // 코어 키트 I (코어 1단계 강화 재료)
    const int CoreKitAmount   = 3;     // CoreLevelData lv1 requiredAmount
    const int ItemTwig        = 4101;  // 나뭇가지 = 설비 연료 (FuelConfig.fuelItemId). OakTreeEnt 단독 드롭.

    // ── 스포트라이트 타깃 id (씬 UI 요소의 TutorialHighlightTarget 와 매칭) ──
    const string TargetMachineInput = "machine_input";   // 머신 재료 슬롯
    const string TargetCoreUpgrade  = "core_upgrade";    // 코어 강화 버튼/패널
    const string TargetFuelSlot     = "fuel_slot";       // 설비 연료 슬롯

    const string Y = "<color=#FFCC00>";  // 강조 색 열기
    const string E = "</color>";          // 닫기

    [MenuItem("Tools/Quest/Generate Tutorial Assets")]
    public static void Generate()
    {
        bool ok = EditorUtility.DisplayDialog(
            "튜토리얼 자산 생성 (전면 재작성)",
            "최종 시트 기준으로 SO 일괄 생성.\n" +
            "  - Objective/Quest 폴더 통째 삭제 후 재생성 (다듬은 라벨 사라짐)\n" +
            "  - Category/Tutorial 은 GUID 유지 (씬 슬롯 유지)",
            "생성", "취소");
        if (!ok) return;

        if (AssetDatabase.IsValidFolder(ObjectivesFolder))
            AssetDatabase.DeleteAsset(ObjectivesFolder);
        if (AssetDatabase.IsValidFolder(QuestsFolder))
            AssetDatabase.DeleteAsset(QuestsFolder);

        EnsureFolder("Assets/06.ScriptableObjects", "Quest");
        EnsureFolder(RootFolder, "Objectives");
        EnsureFolder(RootFolder + "/Objectives", "Tutorial");
        EnsureFolder(RootFolder, "Quests");
        EnsureFolder(RootFolder + "/Quests", "Tutorial");
        EnsureFolder(RootFolder, "Categories");
        EnsureFolder(RootFolder, "Tutorials");

        var quests = new List<QuestSO>();

        // Q1. 기본 조작 [병렬]
        quests.Add(BuildQuest("quest_tut_01_basics", "기본 조작 익히기",
            CreateMoveDistance("obj_move", $"{Y}WASD{E}로 {Y}이동{E}하세요.", 3f),
            CreatePressKey("obj_jump", $"{Y}Space{E}로 {Y}점프{E}하세요.", KeyCode.Space, 1),
            CreatePressKey("obj_dash", $"{Y}우클릭{E}으로 {Y}대시{E}하세요.", KeyCode.Mouse1, 1)));

        // Q2. 전투 [병렬]
        quests.Add(BuildQuest("quest_tut_02_combat", "전투",
            CreatePressKey("obj_attack", $"{Y}좌클릭{E}으로 {Y}공격{E}하세요.", KeyCode.Mouse0, 1),
            CreateEnemyKill("obj_kill", $"외부의 {Y}적{E}을 {Y}처치{E}하세요.", "tutorial_enemy", 1)));

        // Q3. 드랍 획득 + 인벤 [병렬] (나뭇가지=설비 연료, OakTreeEnt 드롭)
        quests.Add(BuildQuest("quest_tut_03_loot", "전리품 획득",
            CreateItemAcquire("obj_loot_venom", $"{Y}거미 독액{E}을 {Y}획득{E}하세요.", ItemSpiderVenom, 1),
            CreateItemAcquire("obj_loot_corrosive", $"{Y}부식액{E}을 {Y}획득{E}하세요.", ItemCorrosive, 1),
            CreateItemAcquire("obj_loot_twig", $"{Y}오크 트리{E}를 잡아 연료 {Y}나뭇가지{E}를 {Y}획득{E}하세요.", ItemTwig, 1),
            CreatePressKey("obj_inventory", $"{Y}Tab{E}으로 {Y}인벤토리{E}를 확인하세요.", KeyCode.Tab, 1)));

        // Q4. [안내] 시간 시스템
        quests.Add(BuildQuest("quest_tut_04_time_info", "시간 시스템",
            CreateContinue("obj_time_info",
                $"{Y}결계 안{E}에서는 체력(시간)이 {Y}회복{E}되고, {Y}결계 밖{E}에서는 시간이 점점 {Y}줄어듭니다{E}.")));

        // Q5. 설비 해금 (F로 줍기)
        quests.Add(BuildQuest("quest_tut_05_unlock_extractor", "설비 해금",
            CreateFacilityUnlock("obj_unlock_extractor", $"바닥의 {Y}생체 추출기{E}를 {Y}F{E}로 주워 {Y}해금{E}하세요.", BioExtractorId)));

        // Q6. 건설 모드 + 설치 [병렬]
        quests.Add(BuildQuest("quest_tut_06_build_extractor", "생체 추출기 설치",
            CreatePressKey("obj_build_mode", $"{Y}B{E}로 {Y}건설 모드{E}에 진입하세요.", KeyCode.B, 1),
            CreateFacilityPlace("obj_place_extractor", $"{Y}생체 추출기{E}를 {Y}설치{E}하세요.", BioExtractorId, 1)));

        // Q6b. [안내] 해제 모드 (B 건설모드 -> X 해제, Shift 드래그 연속 해제)
        quests.Add(BuildQuest("quest_tut_06b_demolish_info", "해제 모드",
            CreateContinue("obj_demolish_info",
                $"잘못 지었다면 건설 모드에서 {Y}X{E}로 {Y}해제 모드{E}. {Y}클릭{E}으로 제거, {Y}Shift 드래그{E}로 여러 개를 {Y}한 번에 해제{E}할 수 있어요.")));

        // Q7. 상호작용
        quests.Add(BuildQuest("quest_tut_07_interact_extractor", "설비 열기",
            CreateFacilityInteract("obj_interact_extractor", $"{Y}F{E}로 {Y}생체 추출기{E}를 여세요.", BioExtractorId, 1)));

        // Q7b. [안내 + 스포트라이트] 연료 설명
        quests.Add(BuildQuest("quest_tut_07b_fuel_info", "연료 안내",
            CreateContinue("obj_fuel_info",
                $"설비는 {Y}연료{E}가 있어야 가동됩니다. {Y}연료 슬롯{E}에 {Y}나뭇가지{E}를 넣으세요. (연료는 {Y}오크 트리{E} 처치로 보충)", TargetFuelSlot)));

        // Q7c. 연료 투입 (게이트)
        quests.Add(BuildQuest("quest_tut_07c_fuel_add", "연료 투입",
            CreateFuelAdd("obj_fuel_add", $"{Y}나뭇가지{E}를 {Y}연료 슬롯{E}에 {Y}투입{E}하세요.", BioExtractorId)));

        // Q8. [안내 + 스포트라이트] 재료 슬롯 강조
        quests.Add(BuildQuest("quest_tut_08_input_info", "재료 투입 안내",
            CreateContinue("obj_input_info",
                $"이곳에 {Y}재료{E}를 넣으면 설비가 {Y}가공{E}을 시작합니다.", TargetMachineInput)));

        // Q9. 재료 투입 [병렬] (R1201: 거미독액 + 부식액 -> 회복젤)
        quests.Add(BuildQuest("quest_tut_09_input_materials", "재료 투입",
            CreateFacilityInput("obj_in_venom", $"{Y}거미 독액{E}을 {Y}투입{E}하세요.", BioExtractorId, ItemSpiderVenom, 1),
            CreateFacilityInput("obj_in_corrosive", $"{Y}부식액{E}을 {Y}투입{E}하세요.", BioExtractorId, ItemCorrosive, 1)));

        // Q10. 회복젤 회수
        quests.Add(BuildQuest("quest_tut_10_collect_gel", "결과물 회수",
            CreateItemAcquire("obj_collect_gel", $"{Y}출력 슬롯{E}에서 {Y}회복 젤{E}을 {Y}회수{E}하세요.", ItemHealGel, 1)));

        // Q11. 배양기 해금 + 설치 [병렬]
        quests.Add(BuildQuest("quest_tut_11_build_cultivator", "생체 배양기 설치",
            CreateFacilityUnlock("obj_unlock_cultivator", $"{Y}생체 배양기{E}를 {Y}F{E}로 주워 {Y}해금{E}하세요.", BioCultivatorId),
            CreateFacilityPlace("obj_place_cultivator", $"{Y}생체 배양기{E}를 {Y}설치{E}하세요.", BioCultivatorId, 1)));

        // Q12. 배양기 가공 [병렬]
        quests.Add(BuildQuest("quest_tut_12_cultivate", "회복 젤 가공",
            CreateFacilityInteract("obj_interact_cultivator", $"{Y}F{E}로 {Y}생체 배양기{E}를 여세요.", BioCultivatorId, 1),
            CreateFacilityInput("obj_in_gel", $"{Y}회복 젤{E}을 {Y}투입{E}하세요.", BioCultivatorId, ItemHealGel, 1)));

        // Q13. 앰플 회수 + 사용 [병렬]
        quests.Add(BuildQuest("quest_tut_13_ampoule", "회복 앰플 완성",
            CreateItemAcquire("obj_collect_ampoule", $"{Y}초급 회복 앰플{E}을 {Y}회수{E}하세요.", ItemHealAmpoule, 1),
            CreateItemUse("obj_use_ampoule", $"{Y}초급 회복 앰플{E}을 {Y}사용{E}하세요.", ItemHealAmpoule, 1)));

        // Q14. [안내] 앰플 일반화
        quests.Add(BuildQuest("quest_tut_14_ampoule_info", "다른 앰플",
            CreateContinue("obj_ampoule_info",
                $"{Y}공격 / 방어 / 스태미나{E} 앰플도 {Y}같은 방식{E}으로 다른 설비에서 만들 수 있습니다.")));

        // Q15. [안내] 레일 자동화
        quests.Add(BuildQuest("quest_tut_15_rail_info", "자동화 안내",
            CreateContinue("obj_rail_info",
                $"{Y}레일{E}로 설비를 이으면 아이템이 {Y}자동{E}으로 다음 설비로 이동합니다.")));

        // Q16. 레일 연결 (액션)
        quests.Add(BuildQuest("quest_tut_16_rail_connect", "레일 연결",
            CreateRailConnect("obj_rail_connect", $"두 설비를 {Y}레일{E}로 {Y}연결{E}하세요.", 1)));

        // Q17. 저장고 해금 + 설치 [병렬]
        quests.Add(BuildQuest("quest_tut_17_build_storage", "저장고 설치",
            CreateFacilityUnlock("obj_unlock_storage", $"{Y}저장고{E}를 {Y}F{E}로 주워 {Y}해금{E}하세요.", StorageId),
            CreateFacilityPlace("obj_place_storage", $"{Y}저장고{E}를 {Y}설치{E}하세요.", StorageId, 1)));

        // Q18. [안내] 창고
        quests.Add(BuildQuest("quest_tut_18_storage_info", "창고 안내",
            CreateContinue("obj_storage_info",
                $"{Y}저장고{E}를 설치하면 거점 {Y}창고{E} 용량이 늘어납니다. {Y}창고{E}에 아이템을 보관해 두세요.")));

        // Q19. [안내 + 보상] 코어 키트 지급
        var qCoreIntro = BuildQuest("quest_tut_19_core_intro", "코어 강화 안내",
            CreateContinue("obj_core_info",
                $"{Y}코어{E}를 {Y}강화{E}하면 {Y}최대 체력{E}이 늘어납니다. 체험용 {Y}코어 키트 I{E}를 지급합니다.",
                TargetCoreUpgrade));
        qCoreIntro.rewards = new[] { new QuestSO.QuestReward { itemId = CoreKitId, amount = CoreKitAmount } };
        EditorUtility.SetDirty(qCoreIntro);
        quests.Add(qCoreIntro);

        // Q20. 코어 강화 (액션)
        quests.Add(BuildQuest("quest_tut_20_core_upgrade", "코어 강화",
            CreateCoreUpgrade("obj_core_upgrade", $"받은 키트로 {Y}코어{E}를 {Y}강화{E}하세요.", 0)));

        // Q21. [안내] 마무리
        quests.Add(BuildQuest("quest_tut_21_finish", "튜토리얼 완료",
            CreateContinue("obj_finish",
                $"이후 {Y}코어 키트{E}는 {Y}코어 합성기{E}에서 직접 제작하세요. {Y}튜토리얼 완료!{E} 자유롭게 기지를 키워보세요.")));

        // CategorySO (GUID 유지)
        string catPath = $"{CategoriesFolder}/Cat_Tutorial_Main.asset";
        var cat = AssetDatabase.LoadAssetAtPath<CategorySO>(catPath);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<CategorySO>();
            AssetDatabase.CreateAsset(cat, catPath);
        }
        cat.id = "tutorial_main";
        cat.title = "튜토리얼";
        cat.quests = quests.ToArray();
        EditorUtility.SetDirty(cat);

        // TutorialSO (GUID 유지)
        string tutPath = $"{TutorialsFolder}/Tutorial_Main.asset";
        var tut = AssetDatabase.LoadAssetAtPath<TutorialSO>(tutPath);
        if (tut == null)
        {
            tut = ScriptableObject.CreateInstance<TutorialSO>();
            AssetDatabase.CreateAsset(tut, tutPath);
        }
        tut.savePrefix = "tutorial";
        tut.categories = new[] { cat };
        EditorUtility.SetDirty(tut);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[TutorialAssetBuilder] 생성 완료 — Quest {quests.Count}개.\n" +
            $"씬 세팅 체크:\n" +
            $"1. QuestManager.tutorial = Tutorial_Main.asset\n" +
            $"2. FacilityUnlockPickup 배치: facilityId {BioExtractorId}(추출기)/{BioCultivatorId}(배양기)/{StorageId}(저장고)\n" +
            $"3. 튜토 스폰 몹 드롭(EnemyDropOnDeath.sourceId): 거미독액{ItemSpiderVenom}/부식액{ItemCorrosive}, OakTreeEnt가 연료 나뭇가지{ItemTwig} → 스폰풀에 OakTreeEnt 꼭 포함\n" +
            $"4. 스포트라이트 TutorialHighlightTarget: 재료슬롯 id='{TargetMachineInput}', 연료슬롯 id='{TargetFuelSlot}', 코어강화 id='{TargetCoreUpgrade}'\n" +
            $"5. PlayerMovementWatcher.watchedKeys 에 Space/Mouse0/Mouse1/Tab/B 포함 확인");
    }

    // ── QuestSO 빌더 ──────────────────────────────────────────────────
    static QuestSO BuildQuest(string id, string title, params ObjectiveSO[] objectives)
    {
        var q = ScriptableObject.CreateInstance<QuestSO>();
        q.id = id;
        q.title = title;
        q.objectives = objectives;
        AssetDatabase.CreateAsset(q, $"{QuestsFolder}/{id}.asset");
        return q;
    }

    // ── Objective 빌더 ────────────────────────────────────────────────
    static MoveDistanceObjective CreateMoveDistance(string name, string label, float distance)
    {
        var o = ScriptableObject.CreateInstance<MoveDistanceObjective>();
        o.label = label; o.requiredDistance = distance;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static PressKeyObjective CreatePressKey(string name, string label, KeyCode key, int count)
    {
        var o = ScriptableObject.CreateInstance<PressKeyObjective>();
        o.label = label; o.key = key; o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static ReachTriggerObjective CreateReachTrigger(string name, string label, string triggerId)
    {
        var o = ScriptableObject.CreateInstance<ReachTriggerObjective>();
        o.label = label; o.targetTriggerId = triggerId;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static EnemyKillObjective CreateEnemyKill(string name, string label, string enemyId, int count)
    {
        var o = ScriptableObject.CreateInstance<EnemyKillObjective>();
        o.label = label; o.enemyId = enemyId; o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static ItemAcquireObjective CreateItemAcquire(string name, string label, int itemId, int count)
    {
        var o = ScriptableObject.CreateInstance<ItemAcquireObjective>();
        o.label = label; o.itemId = itemId; o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static FacilityPlaceObjective CreateFacilityPlace(string name, string label, int facilityId, int count)
    {
        var o = ScriptableObject.CreateInstance<FacilityPlaceObjective>();
        o.label = label; o.facilityId = facilityId; o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static FacilityInteractObjective CreateFacilityInteract(string name, string label, int facilityId, int count)
    {
        var o = ScriptableObject.CreateInstance<FacilityInteractObjective>();
        o.label = label; o.facilityId = facilityId; o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static FacilityInputObjective CreateFacilityInput(string name, string label, int facilityId, int itemId, int count)
    {
        var o = ScriptableObject.CreateInstance<FacilityInputObjective>();
        o.label = label; o.facilityId = facilityId; o.inputItemId = itemId; o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static ItemUseObjective CreateItemUse(string name, string label, int itemId, int count)
    {
        var o = ScriptableObject.CreateInstance<ItemUseObjective>();
        o.label = label; o.itemId = itemId; o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    // ── 신규 Objective 빌더 ───────────────────────────────────────────
    static ContinueObjective CreateContinue(string name, string label, string spotlightTargetId = "")
    {
        var o = ScriptableObject.CreateInstance<ContinueObjective>();
        o.label = label; o.spotlightTargetId = spotlightTargetId;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static FacilityUnlockObjective CreateFacilityUnlock(string name, string label, int facilityId, int count = 1)
    {
        var o = ScriptableObject.CreateInstance<FacilityUnlockObjective>();
        o.label = label; o.facilityId = facilityId; o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static CoreUpgradeObjective CreateCoreUpgrade(string name, string label, int targetLevel = 0)
    {
        var o = ScriptableObject.CreateInstance<CoreUpgradeObjective>();
        o.label = label; o.targetLevel = targetLevel;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static RailConnectObjective CreateRailConnect(string name, string label, int count = 1)
    {
        var o = ScriptableObject.CreateInstance<RailConnectObjective>();
        o.label = label; o.requiredCount = count;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static FuelAddObjective CreateFuelAdd(string name, string label, int facilityId, int count = 1)
    {
        var o = ScriptableObject.CreateInstance<FuelAddObjective>();
        o.label = label; o.facilityId = facilityId; o.requiredCount = count;
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
