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
    const string TargetTimeBar      = "time_bar";        // 좌하단 시간/DECAY 막대
    const string TargetStatPanel    = "status_panel";    // C 스탯 창
    const string TargetStatButton   = "stat_button";     // 우측 상단 C 스탯 아이콘(C_Icon)
    const string TargetMachineOutput = "machine_output"; // 머신 출력/모두받기 버튼
    const string TargetTabIcon      = "tab_icon";        // 우측 상단 TAB(인벤) 아이콘
    const string TargetBIcon        = "b_icon";          // 우측 상단 B(건설) 아이콘
    const string TargetEscIcon      = "esc_icon";        // 우측 상단 ESC(설정) 아이콘
    const string TargetQuickSlots    = "quick_slots";    // 건설 퀵슬롯 바
    const string TargetRailSlot      = "rail_slot";      // E 레일 슬롯
    const string TargetBuildDemolish = "build_demolish"; // X 일괄조작 힌트
    const string TargetBuildRotate   = "build_rotate";   // R 설비회전 힌트
    const string TargetSkillQ        = "skill_q";        // 우하단 스킬 슬롯 Q
    const string TargetSkillE        = "skill_e";        // 우하단 스킬 슬롯 E
    const string TargetSkillR        = "skill_r";        // 우하단 스킬 슬롯 R

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

        // Q0. [시작 안내] 가이드 투어 — 한 오버레이에서 클릭으로 연속 진행(끊김 없음). 시간/메뉴아이콘/스탯.
        quests.Add(BuildQuest("quest_tut_00_intro", "시작 안내",
            CreateGuidedTour("obj_intro_tour",
                TourStep($"이 게임은 {Y}체력{E}이 곧 {Y}시간{E}입니다. {Y}결계(기지) 안{E}에서는 시간이 줄지 않지만({Y}DECAY OFF{E}), {Y}결계 밖{E}에선 시간이 계속 {Y}줄어들어{E} 0이 되면 쓰러집니다. 시간은 {Y}회복 앰플{E}로 채우고 {Y}코어 강화{E}로 최대치를 늘려요.", TargetTimeBar),
                TourStep($"{Y}TAB{E} - {Y}인벤토리{E}를 여닫습니다.", TargetTabIcon),
                TourStep($"{Y}B{E} - {Y}건설 모드{E}로 들어갑니다.", TargetBIcon),
                TourStep($"{Y}ESC{E} - {Y}설정{E}을 엽니다.", TargetEscIcon),
                TourStep($"{Y}C{E} 키를 눌러 {Y}스탯 창{E}을 열어보세요.", TargetStatButton, KeyCode.C),
                TourStep($"여기서 {Y}최대 시간{E} · {Y}스태미나{E} · {Y}공격력{E} · {Y}방어력{E}을 확인할 수 있어요. {Y}코어 강화{E}로 {Y}최대 시간{E}을, {Y}앰플 제작{E}으로 나머지 스탯을 올릴 수 있습니다. {Y}C{E} 키로 {Y}언제든 여닫을 수 있어요{E}.", TargetStatPanel),
                TourStep($"우하단 {Y}스킬{E} 3개. {Y}Q{E} 스킬은 {Y}기본 공격 1타{E}를 적에게 {Y}적중{E}시키면 충전돼요.", TargetSkillQ),
                TourStep($"{Y}E{E} 스킬은 {Y}기본 공격 2타{E}를 {Y}적중{E}시키면 충전됩니다.", TargetSkillE),
                TourStep($"{Y}R{E} 스킬은 {Y}기본 공격 3타{E}를 {Y}적중{E}시키면 충전됩니다. 충전되면 강력한 스킬을 사용하세요!", TargetSkillR))));

        // Q1. 기본 조작 [병렬]
        quests.Add(BuildQuest("quest_tut_01_basics", "기본 조작 익히기",
            CreateMoveDistance("obj_move", $"{Y}WASD{E}로 {Y}이동{E}하세요.", 3f),
            CreatePressKey("obj_jump", $"{Y}Space{E}로 {Y}점프{E}하세요.", KeyCode.Space, 1),
            CreatePressKey("obj_dash", $"{Y}우클릭{E}으로 {Y}대시{E}하세요.", KeyCode.Mouse1, 1)));

        // Q1b. 사냥터로 이동 (enemy 트리거 도착 — 전투 전)
        quests.Add(BuildQuest("quest_tut_01b_reach_hunt", "사냥터로 이동",
            CreateReachTrigger("obj_reach_enemy", $"결계 밖 {Y}사냥터{E}로 {Y}이동{E}하세요.", "enemy")));

        // Q2. 전투 [병렬]
        quests.Add(BuildQuest("quest_tut_02_combat", "전투",
            CreatePressKey("obj_attack", $"{Y}좌클릭{E}으로 {Y}공격{E}하세요.", KeyCode.Mouse0, 1),
            CreateEnemyKill("obj_kill", $"외부의 {Y}적{E}을 {Y}처치{E}하세요.", "tutorial_enemy", 1)));

        // Q3. 드랍 획득 + 인벤 [병렬] (나뭇가지=설비 연료, OakTreeEnt 드롭)
        quests.Add(BuildQuest("quest_tut_03_loot", "전리품 획득",
            CreateItemAcquire("obj_loot_venom", $"{Y}거미 독액{E}을 {Y}획득{E}하세요.", ItemSpiderVenom, 1),
            CreateItemAcquire("obj_loot_corrosive", $"{Y}부식액{E}을 {Y}획득{E}하세요.", ItemCorrosive, 1),
            CreateItemAcquire("obj_loot_twig", $"{Y}오크 트리{E}를 잡아 연료 {Y}나뭇가지{E}를 {Y}2개{E} {Y}획득{E}하세요.", ItemTwig, 2),
            CreatePressKey("obj_inventory", $"{Y}Tab{E}으로 {Y}인벤토리{E}를 확인하세요.", KeyCode.Tab, 1)));

        // Q5. 설비 해금 — 위치 이동 + F 해금 [병렬]. (따로 '도착' 퀘 안 둠: 갔다가 한 번 깨지고 다시 F 누르는 흐름 제거)
        quests.Add(BuildQuest("quest_tut_05_unlock_extractor", "설비 해금",
            CreateReachTrigger("obj_reach_extractor", $"{Y}생체 추출기{E}가 있는 곳으로 {Y}이동{E}하세요.", "1"),
            CreateFacilityUnlock("obj_unlock_extractor", $"바닥의 {Y}생체 추출기{E}를 {Y}F{E}로 주워 {Y}해금{E}하세요.", BioExtractorId)));

        // Q5b. 건설 구역으로 이동 (build 트리거 도착) — 건설은 이 구역에서만 가능
        quests.Add(BuildQuest("quest_tut_05b_reach_build", "건설 구역으로 이동",
            CreateReachTrigger("obj_reach_build", $"{Y}건설 구역{E}(결계 안)으로 {Y}이동{E}하세요. 건설은 {Y}이 구역에서만{E} 가능합니다.", "build")));

        // Q6. 건설 모드 진입 (실제 진입 시에만 완료 — 존 밖 B는 진입 안 되므로 안 깨짐)
        quests.Add(BuildQuest("quest_tut_06_enter_build", "건설 모드 진입",
            CreateEnterBuildMode("obj_build_mode", $"{Y}B{E}로 {Y}건설 모드{E}에 진입하세요.")));

        // Q6a. [안내] 건설 조작 — 진입하자마자 퀵슬롯/해제/레일 설명 (가이드 투어)
        // ensureBuildMode=true: 퀘 갭에서 B를 연타로 들어갔다 나가도 투어가 항상 건설 모드에서 뜨도록 강제 진입.
        var buildTour = CreateGuidedTour("obj_build_tour",
            TourStep($"{Y}퀵 슬롯{E}에서 지을 설비를 고릅니다. 방금 {Y}해금한 설비{E}가 여기 있고, 빈 칸은 다른 설비가 맵에 {Y}숨겨져{E} 있어서예요 - {Y}찾아 F로 해금{E}하면 채워집니다.", TargetQuickSlots),
            TourStep($"{Y}X{E} - 설치한 설비를 {Y}해제{E} (클릭 제거 / {Y}Shift 드래그{E}로 여러 개 한 번에).", TargetBuildDemolish),
            TourStep($"{Y}E{E} - {Y}레일{E}을 깔아 설비를 이으면 아이템이 {Y}자동{E}으로 이동합니다.", TargetRailSlot),
            TourStep($"{Y}R{E} - 설치할 설비를 {Y}회전{E}시켜 방향을 바꿉니다. (회전 가능한 설비만)", TargetBuildRotate),
            TourStep($"{Y}우클릭{E} - {Y}건설 모드{E}를 {Y}빠져나갑니다{E}. ({Y}B{E}로 다시 들어올 수 있어요)"));
        buildTour.ensureBuildMode = true;
        quests.Add(BuildQuest("quest_tut_06a_build_tour", "건설 조작 안내", buildTour));

        // Q6b. 생체 추출기 설치
        quests.Add(BuildQuest("quest_tut_06b_place_extractor", "생체 추출기 설치",
            CreateFacilityPlace("obj_place_extractor", $"{Y}생체 추출기{E}를 {Y}설치{E}하세요.", BioExtractorId, 1)));

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

        // Q9b. [안내 + 스포트라이트] 출력/받기 강조
        quests.Add(BuildQuest("quest_tut_09b_output_info", "결과물 받기 안내",
            CreateContinue("obj_output_info",
                $"{Y}가공{E}이 끝나면 결과물이 나옵니다. {Y}모두 받기{E}로 결과물을 {Y}회수{E}하세요.",
                TargetMachineOutput)));

        // Q10. 회복젤 회수 (+ 다음 배양기 가동용 연료 나뭇가지 보상, #28)
        var qCollectGel = BuildQuest("quest_tut_10_collect_gel", "결과물 회수",
            CreateItemAcquire("obj_collect_gel", $"{Y}출력 슬롯{E}에서 {Y}회복 젤{E}을 {Y}회수{E}하세요.", ItemHealGel, 1));
        qCollectGel.rewards = new[] { new QuestSO.QuestReward { itemId = ItemTwig, amount = 5 } };
        EditorUtility.SetDirty(qCollectGel);
        quests.Add(qCollectGel);

        // (연료 확보는 별도 퀘 없이 — 초반 Q3에서 나뭇가지 2개 수집 + Q10 완료 보상으로 지급, #28)

        // Q11. 배양기 해금 + 설치 — 위치 이동 + F 해금 + 설치 [병렬]. (도착 퀘 분리 안 함)
        quests.Add(BuildQuest("quest_tut_11_build_cultivator", "생체 배양기 설치",
            CreateReachTrigger("obj_reach_cultivator", $"{Y}생체 배양기{E}가 있는 곳으로 {Y}이동{E}하세요.", "2"),
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
                $"{Y}레일{E}로 설비를 이으면 아이템이 {Y}자동{E}으로 다음 설비로 이동합니다. {Y}건설 모드(B){E}에서 {Y}E(레일){E}를 고른 뒤, 설비의 {Y}출구(E 표시){E}를 클릭해 다음 설비까지 이어 주세요.")));

        // Q16. 레일 연결 (액션)
        quests.Add(BuildQuest("quest_tut_16_rail_connect", "레일 연결",
            CreateRailConnect("obj_rail_connect", $"두 설비를 {Y}레일{E}로 {Y}연결{E}하세요.", 1)));

        // (저장고 #8 = 있으면 좋고 없으면 말고 → 튜토리얼 필수에서 제외. 마무리(Q21)에서 "숨겨진 설비 찾기"로 안내.)

        // [보류] 코어 강화 튜토(옛 Q19 안내 + Q20 강화)는 방식 전면 개편 예정이라 현재 제외.
        // 새 플로우(추후 구현, 지금은 만들지 말 것): (1) 코어강화 위치로 이동(마커) + 완료보상으로 코어 키트 3개
        // (2) F로 단말 열기 (3) 열리면 UI 강조 + 효과 설명(체력↑/단계별 필요한 코어 다름/코어 제작처/스타포스식 강화 등)
        // (4) 끝나면 체력바 강조로 최대치 변화 인지. ※코어 UI는 교체 예정이라 그에 맞춰 구현.

        // Q21. [안내] 마무리 (숨겨진 설비 안내는 건설투어에서 함)
        quests.Add(BuildQuest("quest_tut_21_finish", "튜토리얼 완료",
            CreateContinue("obj_finish",
                $"{Y}튜토리얼 완료!{E} {Y}코어 키트{E}는 {Y}코어 합성기{E}에서 제작할 수 있어요. 이제 자유롭게 기지를 키워보세요!")));

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
            $"2. FacilityUnlockPickup 배치(+위치 마커): 필수 {BioExtractorId}(추출기)/{BioCultivatorId}(배양기)만 튜토 동선에. {StorageId}(저장고) 등 나머지는 튜토 밖 숨김(마무리에서 안내)\n" +
            $"3. 튜토 스폰 몹 드롭(EnemyDropOnDeath.sourceId): 거미독액{ItemSpiderVenom}/부식액{ItemCorrosive}, OakTreeEnt가 연료 나뭇가지{ItemTwig} → 스폰풀에 OakTreeEnt 꼭 포함\n" +
            $"4. 스포트라이트: 시간막대('{TargetTimeBar}')/스탯창('{TargetStatPanel}')=코드 자동등록. 수동 TutorialHighlightTarget 부착 필요: C아이콘(C_Icon) id='{TargetStatButton}', 재료슬롯 id='{TargetMachineInput}', 연료슬롯 id='{TargetFuelSlot}', 코어강화 id='{TargetCoreUpgrade}'\n" +
            $"5. PlayerMovementWatcher.watchedKeys 에 Space/Mouse0/Mouse1/Tab/B 포함 확인 (C는 GameUIController에서 자동 발화 → 설정 불필요)\n" +
            $"6. QuestTrigger 콜라이더(IsTrigger, 플레이어Tag='Player'): 사냥터='enemy', 건설구역='build'(BuildZone위치), 추출기위치='1', 배양기위치='2'. 각 설비위치 트리거 안에 해당 FacilityUnlockPickup 배치(추출기#1/배양기#2)\n" +
            $"7. 건설투어/스킬 스포트라이트=전부 코드 자동등록(씬 작업 불필요): rail_slot=QuickSlotPanel 'Quick (8)'(라벨 E), build_demolish='Right_X'+자식(아이콘 포함), quick_slots=설비 들어간 슬롯만 QuickSlotIconUI가 자동, skill_q/e/r=PlayerHudUI가 skill1/2/3IconImage로 등록. (slotEffects/railSlotEffect는 씬 미연결이라 사용 안 함)");
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

    static EnterBuildModeObjective CreateEnterBuildMode(string name, string label)
    {
        var o = ScriptableObject.CreateInstance<EnterBuildModeObjective>();
        o.label = label;
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
    static ContinueObjective CreateContinue(string name, string label, string spotlightTargetId = "", KeyCode advanceKey = KeyCode.None)
    {
        var o = ScriptableObject.CreateInstance<ContinueObjective>();
        o.label = label; o.spotlightTargetId = spotlightTargetId; o.advanceKey = advanceKey;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static GuidedTourObjective CreateGuidedTour(string name, params GuidedTourObjective.Step[] steps)
    {
        var o = ScriptableObject.CreateInstance<GuidedTourObjective>();
        o.steps = steps;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static GuidedTourObjective.Step TourStep(string label, string spotlightId = "", KeyCode advanceKey = KeyCode.None)
        => new GuidedTourObjective.Step { stepLabel = label, spotlightTargetId = spotlightId, advanceKey = advanceKey };

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
