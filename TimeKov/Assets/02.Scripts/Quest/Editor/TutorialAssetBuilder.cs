using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 튜토리얼 SO 자산 일괄 생성 (영상 팝업 기반 전면 리디자인).
/// 설계 원칙: 영상=설명 / 실제 플레이 행동(action)=깨질 수 있는 별 objective(영상에 흡수 금지).
/// 복잡/공장/레일/코어 안내는 영상 팝업으로 묶고, 자명한 조작은 좌측 퀘스트 텍스트만 사용.
///
/// 메뉴: Tools > Quest > Generate Tutorial Assets
/// 주의: Objective/Quest 폴더는 통째 삭제 후 재생성 -> 인스펙터에서 다듬은 라벨은 사라짐.
///       Category/Tutorial 은 GUID 유지(씬 슬롯 안 끊김).
///
/// 무소프트락 불변식(검증 통과 - 바꾸지 말 것):
///  - 건설구역 도착(ReachTrigger 'build')은 건설진입/투어와 절대 한 퀘로 안 합침(AND게이트는 활성순서 보장 안 함).
///  - 앰플 회수(ItemAcquire)와 사용(ItemUse)은 별 퀘로 분리(1개짜리 소비형 비대칭 갭락 회피).
///  - facilityId 는 전부 구체값(추출기1/배양기2). 0 와일드카드는 상시 FacilityUnlock(상태형)에서만 허용.
///  - CoreUpgrade targetLevel=0(시도형), EnemyKill requiredCount=1(한정자원 회피).
///  - FacilityProcessComplete 미사용(lookback/상태조회 둘 다 없는 유일 진성 갭위험). 회수는 ItemAcquire 로.
///  - 보상은 CreateAsset 전에 set(BuildQuestRewarded). 후 set 은 저장 안 돼 wipe 됨.
/// </summary>
public static class TutorialAssetBuilder
{
    const string RootFolder = "Assets/06.ScriptableObjects/Quest";
    const string ObjectivesFolder = RootFolder + "/Objectives/Tutorial";
    const string QuestsFolder = RootFolder + "/Quests/Tutorial";
    const string CategoriesFolder = RootFolder + "/Categories";
    const string TutorialsFolder = RootFolder + "/Tutorials";

    // -- 최종 FacilityData 시트 기준 --
    const int BioExtractorId = 1;   // 생체 추출기 (3x3)
    const int BioCultivatorId = 2;  // 생체 배양기 (5x5)
    const int StorageId = 8;        // 저장고 (창고 용량 제공) - 튜토 필수 아님(숨김)
    const int TotalFacilityCount = 8;   // 전체 설비 수 (마무리 "설비 해금하기" 목표 = x/8). 설비 수 바뀌면 여기 수정.

    // -- 최종 ItemData 시트 기준 --
    const int ItemSpiderVenom = 1102;  // 거미 독액 (드롭, 원료)
    const int ItemCorrosive   = 3101;  // 부식액 (드롭, 원료)
    const int ItemHealGel     = 1201;  // 회복 젤 (R1201 @추출기1: 1102+3101)
    const int ItemHealAmpoule = 5101;  // 초급 회복 앰플 (R5101 @배양기2: 1201)
    const int CoreKitId       = 6101;  // 코어 키트 I (코어 1단계 강화 재료)
    const int CoreKitAmount   = 3;     // CoreLevelData lv1 requiredAmount
    const int ItemTwig        = 4101;  // 나뭇가지 = 설비 연료 (FuelConfig.fuelItemId). OakTreeEnt 단독 드롭.

    // -- 스포트라이트 타깃 id (씬 UI 요소의 TutorialHighlightTarget 와 매칭) --
    const string TargetTimeBar      = "time_bar";        // 좌하단 시간/DECAY 막대
    const string TargetStatPanel    = "status_panel";    // C 스탯 창
    const string TargetStatButton   = "stat_button";     // 우측 상단 C 스탯 아이콘(C_Icon)
    const string TargetTabIcon      = "tab_icon";        // 우측 상단 TAB(인벤) 아이콘
    const string TargetQuickSlots    = "quick_slots";    // 건설 퀵슬롯 바
    const string TargetRailSlot      = "rail_slot";      // E 레일 슬롯
    const string TargetBuildDemolish = "build_demolish"; // X 일괄조작 힌트
    const string TargetBuildRotate   = "build_rotate";   // R 설비회전 힌트
    const string TargetSkillQ        = "skill_q";        // 우하단 스킬 슬롯 Q

    const string Y = "<color=#FFCC00>";  // 강조 색 열기
    const string E = "</color>";          // 닫기

    // -- 영상 팝업 토글 --
    // false 로 두고 재생성하면 영상 단계가 빠진다(설명만 사라지고 행동 퀘는 그대로라 흐름은 완주 가능). 안전 복원용.
    const bool EnableVideoTutorials = true;
    // 영상 클립은 이 폴더에서 "페이지 제목 == 파일명"으로 자동 로드. 종욱이 제목 그대로 mp4 를 여기 넣으면 됨.
    const string VideoFolder = "Assets/12.Video/Tutorial";

    [MenuItem("Tools/Quest/Generate Tutorial Assets")]
    public static void Generate()
    {
        bool ok = EditorUtility.DisplayDialog(
            "튜토리얼 자산 생성 (전면 재작성)",
            "영상 팝업 기반 리디자인으로 SO 일괄 생성.\n" +
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

        // 영상 클립을 떨궈둘 폴더 보장 + 런타임 스프라이트 복사
        if (EnableVideoTutorials)
        {
            EnsureFolder("Assets", "12.Video");
            EnsureFolder("Assets/12.Video", "Tutorial");
            EnsureVideoUiSprites();   // 인벤 강조 프레임을 Resources로 복사(런타임 로드용, 9-slice 보존)
        }

        var quests = new List<QuestSO>();

        // ============================================================
        // 도입 - 시작 안내 (스포트라이트 투어, 9->5스텝 압축)
        //   인트로는 HUD 위치를 짚는 게 본질이라 영상 대신 스포트라이트 유지.
        // ============================================================
        quests.Add(BuildQuest("quest_tut_00_intro", "시작 안내",
            CreateGuidedTour("obj_intro_tour",
                TourStep($"이 게임은 {Y}체력{E}이 곧 {Y}시간{E}입니다. {Y}결계(기지) 안{E}에선 시간이 줄지 않지만({Y}DECAY OFF{E}), {Y}결계 밖{E}에선 시간이 계속 {Y}줄어{E} 0이 되면 쓰러집니다. {Y}회복 앰플{E}로 채우고 {Y}코어 강화{E}로 최대치를 늘려요.", TargetTimeBar),
                TourStep($"우상단 메뉴 - {Y}TAB{E}: 인벤토리 / {Y}B{E}: 건설 모드 / {Y}ESC{E}: 설정.", TargetTabIcon),
                TourStep($"{Y}C{E} 키를 눌러 {Y}스탯 창{E}을 열어보세요.", TargetStatButton, KeyCode.C),
                TourStep($"여기서 {Y}최대 시간{E}·{Y}스태미나{E}·{Y}공격력{E}·{Y}방어력{E}을 확인합니다. {Y}코어 강화{E}로 최대 시간을, {Y}앰플 제작{E}으로 나머지 스탯을 올릴 수 있어요. {Y}C{E}로 언제든 여닫을 수 있습니다.", TargetStatPanel),
                TourStep($"우하단 {Y}스킬{E} 3개 - {Y}Q·E·R{E}로 사용하고, 쓰고 나면 {Y}쿨타임{E}이 끝나야 다시 쓸 수 있어요. {Y}R{E}은 쿨타임이 길지만 강력합니다.", TargetSkillQ))));

        // ============================================================
        // 조작 - 자명한 입력은 좌측 텍스트만
        // ============================================================
        quests.Add(BuildQuest("quest_tut_01_basics", "기본 조작 익히기",
            CreateMoveDistance("obj_move", $"{Y}WASD{E}로 {Y}이동{E}하세요.", 3f),
            CreatePressKey("obj_jump", $"{Y}Space{E}로 {Y}점프{E}하세요.", KeyCode.Space, 1),
            CreatePressKey("obj_dash", $"{Y}우클릭{E}으로 {Y}대시{E}하세요.", KeyCode.Mouse1, 1)));

        quests.Add(BuildQuest("quest_tut_01b_reach_hunt", "사냥터로 이동",
            CreateReachTrigger("obj_reach_enemy", $"결계 밖 {Y}사냥터{E}로 {Y}이동{E}하세요. (결계 밖에선 {Y}시간{E}이 줄기 시작합니다)", "enemy")));

        quests.Add(BuildQuest("quest_tut_02_combat", "전투",
            CreatePressKey("obj_attack", $"{Y}좌클릭{E}으로 {Y}공격{E}하세요.", KeyCode.Mouse0, 1),
            CreateEnemyKill("obj_kill", $"외부의 {Y}적{E}을 {Y}처치{E}하세요.", "tutorial_enemy", 1)));

        // ============================================================
        // 전리품 - [영상] 드롭/상자/가방/창고 개념을 한 큐로, 실제 줍기는 행동 퀘
        // ============================================================
        if (EnableVideoTutorials)
            quests.Add(BuildLootVideoQuest());

        quests.Add(BuildQuest("quest_tut_03_loot", "전리품 획득",
            CreateItemAcquire("obj_loot_venom", $"{Y}거미 독액{E}을 {Y}획득{E}하세요.", ItemSpiderVenom, 1),
            CreateItemAcquire("obj_loot_corrosive", $"{Y}부식액{E}을 {Y}획득{E}하세요.", ItemCorrosive, 1),
            CreateItemAcquire("obj_loot_twig", $"{Y}오크 트리{E}를 잡아 연료 {Y}나뭇가지{E}를 {Y}2개{E} {Y}획득{E}하세요.", ItemTwig, 2),
            CreatePressKey("obj_inventory", $"{Y}Tab{E}으로 {Y}인벤토리{E}를 확인하세요.", KeyCode.Tab, 1)));

        // ============================================================
        // 설비 해금 + 건설 (★건설구역 도착은 별 퀘로 유지 - 합치면 소프트락)
        // ============================================================
        quests.Add(BuildQuest("quest_tut_05_unlock_extractor", "설비 해금",
            CreateReachTrigger("obj_reach_extractor", $"{Y}생체 추출기{E}가 있는 곳으로 {Y}이동{E}하세요.", "1", BioExtractorId),
            CreateFacilityUnlock("obj_unlock_extractor", $"바닥의 {Y}생체 추출기{E}를 {Y}F{E}로 주워 {Y}해금{E}하세요.", BioExtractorId)));

        // ★건설구역 도착 - 별 퀘. EnterBuildMode/투어 앞에서 '존 안' 진입을 선행 보장.
        quests.Add(BuildQuest("quest_tut_05b_reach_build", "건설 구역으로 이동",
            CreateReachTrigger("obj_reach_build", $"{Y}건설 구역{E}(결계 안)으로 {Y}이동{E}하세요. 건설은 {Y}이 구역에서만{E} 가능합니다.", "build")));

        // 건설 모드 진입 - 별 퀘(플레이어가 직접 B 를 눌러 배우게. 투어가 자동진입하면 'B 누르기' 학습이 묻힘)
        quests.Add(BuildQuest("quest_tut_06_enter_build", "건설 모드 진입",
            CreateEnterBuildMode("obj_build_mode", $"{Y}B{E}로 {Y}건설 모드{E}에 진입하세요.")));

        // 건설 조작 안내 (스포트라이트 투어). ensureBuildMode: 갭에 B 토글로 나가도 항상 건설 모드에서 뜨게.
        var buildTour = CreateGuidedTour("obj_build_tour",
            TourStep($"{Y}퀵 슬롯{E}에서 지을 설비를 고릅니다. 방금 {Y}해금한 설비{E}가 여기 있고, 빈 칸은 다른 설비가 맵에 {Y}숨겨져{E} 있어서예요 - {Y}찾아 F로 해금{E}하면 채워집니다.", TargetQuickSlots),
            TourStep($"{Y}X{E} - 설치한 설비를 {Y}해제{E} (클릭 제거 / {Y}Shift 드래그{E}로 여러 개 한 번에).", TargetBuildDemolish),
            TourStep($"{Y}E{E} - {Y}레일{E}을 깔아 설비를 이으면 아이템이 {Y}자동{E}으로 이동합니다.", TargetRailSlot),
            TourStep($"{Y}R{E} - 설치할 설비를 {Y}회전{E}시켜 방향을 바꿉니다. (회전 가능한 설비만)", TargetBuildRotate),
            TourStep($"{Y}우클릭{E} - {Y}건설 모드{E}를 {Y}빠져나갑니다{E}. ({Y}B{E}로 다시 들어올 수 있어요)"));
        buildTour.ensureBuildMode = true;
        quests.Add(BuildQuest("quest_tut_06a_build_tour", "건설 조작 안내", buildTour));

        quests.Add(BuildQuest("quest_tut_06b_place_extractor", "생체 추출기 설치",
            CreateFacilityPlace("obj_place_extractor", $"{Y}생체 추출기{E}를 {Y}설치{E}하세요.", BioExtractorId, 1)));

        // ============================================================
        // 설비 가공 - [영상] 열기/연료/재료/수령 전체를 한 큐로, 실제 행동은 별 objective
        // ============================================================
        if (EnableVideoTutorials)
            quests.Add(BuildFactoryVideoQuest());

        // (설비 '열기' 별도 게이트는 두지 않음 - 연료/재료 슬롯이 설비 UI 안에 있어 여는 게 자연 강제됨)
        quests.Add(BuildQuest("quest_tut_08_fuel_add", "연료 투입",
            CreateFuelAdd("obj_fuel_add", $"{Y}나뭇가지{E}를 {Y}연료 슬롯{E}으로 {Y}드래그{E}해 투입하세요.", BioExtractorId)));

        // R1201: 거미독액 + 부식액 -> 회복젤
        quests.Add(BuildQuest("quest_tut_09_input_materials", "재료 투입",
            CreateFacilityInput("obj_in_venom", $"{Y}거미 독액{E}을 {Y}재료 슬롯{E}으로 {Y}드래그{E}해 투입하세요.", BioExtractorId, ItemSpiderVenom, 1),
            CreateFacilityInput("obj_in_corrosive", $"{Y}부식액{E}을 {Y}재료 슬롯{E}으로 {Y}드래그{E}해 투입하세요.", BioExtractorId, ItemCorrosive, 1)));

        // 회복젤 회수 (+ 다음 배양기/레일 가동 연료 나뭇가지 보상)
        quests.Add(BuildQuestRewarded("quest_tut_10_collect_gel", "결과물 회수",
            new[] { new QuestSO.QuestReward { itemId = ItemTwig, amount = 5 } },
            CreateItemAcquire("obj_collect_gel", $"{Y}출력 슬롯{E}에서 {Y}회복 젤{E}을 {Y}모두 받기{E}로 {Y}회수{E}하세요.", ItemHealGel, 1)));

        // ============================================================
        // 배양 - 두 번째 설비(반복 학습=정착, 안내 없이 텍스트만)
        // ============================================================
        quests.Add(BuildQuest("quest_tut_11_build_cultivator", "생체 배양기 설치",
            CreateReachTrigger("obj_reach_cultivator", $"{Y}생체 배양기{E}가 있는 곳으로 {Y}이동{E}하세요.", "2", BioCultivatorId),
            CreateFacilityUnlock("obj_unlock_cultivator", $"{Y}생체 배양기{E}를 {Y}F{E}로 주워 {Y}해금{E}하세요.", BioCultivatorId),
            CreateFacilityPlace("obj_place_cultivator", $"{Y}생체 배양기{E}를 {Y}설치{E}하세요.", BioCultivatorId, 1)));

        quests.Add(BuildQuestRewarded("quest_tut_12_cultivate", "회복 젤 가공",
            new[] { new QuestSO.QuestReward { itemId = ItemTwig, amount = 1 } },
            CreateFacilityInteract("obj_interact_cultivator", $"{Y}F{E}로 {Y}생체 배양기{E}를 여세요.", BioCultivatorId, 1),
            CreateFacilityInput("obj_in_gel", $"{Y}회복 젤{E}을 {Y}재료 슬롯{E}으로 {Y}드래그{E}해 투입하세요.", BioCultivatorId, ItemHealGel, 1)));

        // ★앰플 회수 / 사용 분리 (한 퀘에 ItemAcquire+ItemUse 두면 1개짜리 소비형 비대칭 갭락)
        quests.Add(BuildQuest("quest_tut_13_collect_ampoule", "회복 앰플 완성",
            CreateItemAcquire("obj_collect_ampoule", $"{Y}초급 회복 앰플{E}을 {Y}회수{E}하세요.", ItemHealAmpoule, 1)));

        quests.Add(BuildQuest("quest_tut_13b_use_ampoule", "회복 앰플 사용",
            CreateItemUse("obj_use_ampoule", $"{Y}초급 회복 앰플{E}을 {Y}사용{E}해 시간을 회복하세요.", ItemHealAmpoule, 1)));

        // ============================================================
        // 자동화 - [영상] 레일 개념/까는 법, 실제 연결/이동은 별 행동 퀘
        //   ★RailConnect(보상)와 RailItemMove 는 절대 한 퀘로 안 합침(보상선행 사슬)
        // ============================================================
        if (EnableVideoTutorials)
            quests.Add(BuildRailVideoQuest());

        // 보상(거미독액+부식액) = 다음 자동화 확인에서 추출기에 넣어 라인 흐르게 할 재료
        quests.Add(BuildQuestRewarded("quest_tut_16_rail_connect", "레일 연결",
            new[] {
                new QuestSO.QuestReward { itemId = ItemSpiderVenom, amount = 1 },
                new QuestSO.QuestReward { itemId = ItemCorrosive,   amount = 1 },
            },
            CreateRailConnect("obj_rail_connect", $"{Y}생체 추출기{E}와 {Y}생체 배양기{E}를 {Y}레일{E}로 {Y}연결{E}하세요.", 1)));

        quests.Add(BuildQuest("quest_tut_16b_rail_move", "레일 자동화 확인",
            CreateRailItemMove("obj_rail_move",
                $"{Y}생체 추출기{E}에 재료를 넣어 가공하고, 결과물이 {Y}레일{E}을 타고 {Y}생체 배양기{E}로 {Y}자동 이동{E}하는지 확인하세요. (추출기 연료가 없으면 {Y}나뭇가지{E}를 다시 넣으세요)", 1)));

        // ============================================================
        // 코어 강화 - 이동(보상 키트) -> [영상 안내 + 열기 + 강화] -> 결과/마무리
        //   ★이동(보상)과 강화는 별 퀘(보상선행). 키트는 이동 퀘 완료시 지급되어 강화 퀘 땐 손에 있음.
        // ============================================================
        quests.Add(BuildQuestRewarded("quest_tut_17_reach_core", "코어 강화 단말로 이동",
            new[] { new QuestSO.QuestReward { itemId = CoreKitId, amount = CoreKitAmount } },
            CreateReachTrigger("obj_reach_core", $"{Y}코어 강화 단말{E}이 있는 곳으로 {Y}이동{E}하세요.", "core")));

        // 코어 강화: 열기(행동) + 영상2p(설명) + 강화 시도(행동) 한 퀘. 미니게임은 움직임이라 영상이 직관적.
        var coreObjs = new List<ObjectiveSO>();
        coreObjs.Add(CreateCoreOpen("obj_open_core", $"{Y}F{E}로 {Y}코어 강화 단말{E}을 여세요."));
        if (EnableVideoTutorials)
            coreObjs.Add(CreateVideoTutorial("obj_core_video", "코어 강화 안내를 확인하세요.",
                VPage("코어 강화란",
                    $"{Y}코어 강화{E}로 {Y}최대 시간(체력){E}을 늘릴 수 있습니다. 단계가 오를수록 더 많은 {Y}코어 키트{E}가 필요하고({Y}코어 합성기{E}에서 제작), 강화는 {Y}성공·실패와 무관하게 키트를 소모{E}하니 {Y}성공 확률{E}을 꼭 확인하세요."),
                VPage("코어 강화 방법",
                    $"{Y}강화 시작{E}을 누르면 코어가 {Y}시계{E}로 바뀝니다. 바늘이 {Y}초록 성공존{E}에 올 때 {Y}정지! 버튼(또는 Space){E}으로 멈추면 성공 확률이 오릅니다. {Y}강화 버튼{E}과 {Y}성공 확률 바{E}는 코어 강화 창에 있습니다.")));
        coreObjs.Add(CreateCoreUpgrade("obj_core_upgrade", $"{Y}강화 시작{E}을 누르고 {Y}정지! 버튼{E}으로 멈춰 코어를 강화해 보세요.", 0));
        quests.Add(BuildQuest("quest_tut_18_core", "코어 강화", coreObjs.ToArray()));

        // 결과 확인 + 마무리 (시간바 스포트라이트 하나로 합침)
        quests.Add(BuildQuest("quest_tut_20_finish", "강화 결과",
            CreateContinue("obj_finish",
                $"{Y}최대 시간{E}이 늘었습니다! 코어를 계속 {Y}강화{E}하려면 {Y}코어 키트{E}가 필요한데, {Y}코어 합성기{E}를 찾아 해금하면 직접 만들 수 있어요. 이제 {Y}자유롭게{E} 기지를 키워보세요!",
                TargetTimeBar)));

        // 상시 목표 - 튜토 끝나도 막연하지 않게 남기는 목표 하나 (facilityId=0=아무거나, 상태조회형이라 갭안전)
        quests.Add(BuildQuest("quest_tut_22_unlock_all", "설비 해금하기",
            CreateFacilityUnlock("obj_unlock_all",
                $"맵에 {Y}숨겨진 설비{E}를 찾아 {Y}F{E}로 {Y}해금{E}해보세요.", 0, TotalFacilityCount)));

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
            $"[TutorialAssetBuilder] 생성 완료 - Quest {quests.Count}개 (영상 {(EnableVideoTutorials ? "ON" : "OFF")}).\n" +
            $"씬 세팅 체크:\n" +
            $"1. QuestManager.tutorial = Tutorial_Main.asset\n" +
            $"2. QuestTrigger(IsTrigger, Player Tag): 'enemy'(사냥터)/'build'(BuildZone과 겹침)/'1'(추출기위치)/'2'(배양기위치)/'core'(코어단말). 오타/중복/콜라이더 범위 확인\n" +
            $"3. 'build' 트리거가 BuildManager.buildZoneCollider 와 정확히 겹쳐야(ReachTrigger는 되는데 EnterBuildMode가 안 되는 모순 방지)\n" +
            $"4. 드롭/스폰: tutorial_enemy 무한리스폰 + 거미독액{ItemSpiderVenom}/부식액{ItemCorrosive} 드롭, OakTreeEnt 가 나뭇가지{ItemTwig} 떨굼(스폰풀에 OakTreeEnt 포함, NavMesh 베이크)\n" +
            $"5. 스포트라이트(코드 자동등록 외 수동 부착): time_bar/status_panel/stat_button(C_Icon)/tab_icon + 건설투어 타깃. 코어는 영상이라 코어 스포트 불필요\n" +
            $"6. PlayerMovementWatcher.watchedKeys 에 Space/Mouse0/Mouse1/Tab/B 포함 (C는 GameUIController 자동발화)\n" +
            $"7. 영상 클립: Assets/12.Video/Tutorial/ 에 '페이지 제목'과 같은 파일명 mp4. 없으면 '영상 준비 중' 폴백\n" +
            $"8. 레일 자동화: 추출기 출구->배양기 입구 포트정렬 + 다른 연결가능 포트 차단 사전 플레이테스트\n" +
            $"9. 코어: 단말이 BaseZone 콜라이더 안 + 코어키트 보상 증발 방지(가방/창고 여유). 시트 코어레벨1 키트={CoreKitId}/필요수<={CoreKitAmount}");
    }

    // ============================================================
    // QuestSO 빌더 (보상은 CreateAsset 전에 set 해야 저장됨)
    // ============================================================
    static QuestSO BuildQuest(string id, string title, params ObjectiveSO[] objectives)
        => BuildQuestRewarded(id, title, null, objectives);

    static QuestSO BuildQuestRewarded(string id, string title, QuestSO.QuestReward[] rewards, params ObjectiveSO[] objectives)
    {
        var q = ScriptableObject.CreateInstance<QuestSO>();
        q.id = id;
        q.title = title;
        q.objectives = objectives;
        q.rewards = rewards ?? new QuestSO.QuestReward[0];   // ★후 set 은 wipe 되므로 CreateAsset 전에 박는다
        AssetDatabase.CreateAsset(q, $"{QuestsFolder}/{id}.asset");
        return q;
    }

    // ============================================================
    // 영상 팝업 퀘 빌더 (페이지 제목 == 영상 파일명)
    // ============================================================
    static QuestSO BuildLootVideoQuest()
        => BuildQuest("quest_tut_02v_loot_video", "전리품과 가방",
            CreateVideoTutorial("obj_loot_video", "전리품과 가방 안내를 확인하세요.",
                VPage("아이템 줍기",
                    $"적이나 오브젝트를 처치하면 {Y}아이템{E}이 떨어집니다. 가까이 가면 {Y}자동으로 줍습니다{E}. 등급이 높은 건 {Y}상자{E}로 나오기도 하며, {Y}F{E}로 열 수 있습니다."),
                VPage("가방",
                    $"{Y}TAB{E}으로 {Y}가방(인벤토리){E}을 엽니다. 칸의 {Y}테두리 색{E}이 아이템 {Y}등급{E}을 나타냅니다."),
                VPage("창고와 연료",
                    $"가방이 가득 차면 {Y}창고{E}로 자동 보관되고, 창고에서 다시 꺼내 쓸 수 있습니다. 설비 연료인 {Y}나뭇가지{E}는 {Y}오크 트리{E}에서 얻습니다.")));

    static QuestSO BuildFactoryVideoQuest()
        => BuildQuest("quest_tut_07v_factory_video", "설비 가공",
            CreateVideoTutorial("obj_factory_video", "설비 가공 안내를 확인하세요.",
                VPage("설비 열기와 연료",
                    $"설비는 {Y}F{E}로 열 수 있고, 가동하려면 {Y}연료{E}가 필요합니다. 가방의 {Y}나뭇가지{E}를 {Y}연료 슬롯{E}으로 드래그해 넣으세요. (나뭇가지는 {Y}오크 트리{E}에서 나옵니다)"),
                VPage("재료 투입과 가공",
                    $"{Y}가방이나 창고{E}의 재료를 {Y}재료 슬롯{E}으로 드래그하면, 맞는 {Y}조합 공식{E}이 있을 때 {Y}가공{E}이 시작됩니다. 설비당 한 번에 {Y}하나의 공식{E}만 가능합니다."),
                VPage("생산품 수령",
                    $"{Y}가공이 완료{E}되면 {Y}모두 받기{E}로 생산품을 가방이나 창고로 {Y}수령{E}할 수 있습니다.")));

    static QuestSO BuildRailVideoQuest()
        => BuildQuest("quest_tut_15v_rail_video", "레일 자동화",
            CreateVideoTutorial("obj_rail_video", "레일 자동화 안내를 확인하세요.",
                VPage("레일 자동화란",
                    $"설비를 {Y}레일{E}로 이으면, 아이템이 {Y}자동으로{E} 다음 설비로 이동합니다. 더 이상 {Y}직접 회수{E}하지 않아도 됩니다."),
                VPage("레일 까는 법",
                    $"{Y}건설 모드(B){E}에서 {Y}E(레일){E}을 고른 뒤, 설비의 {Y}출구(E 표시){E}를 클릭해 다음 설비까지 이어 주세요. {Y}공격·방어·스태미나{E} 앰플도 같은 방식으로 다른 설비에서 만들 수 있습니다.")));

    // ============================================================
    // Objective 빌더
    // ============================================================
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

    static ReachTriggerObjective CreateReachTrigger(string name, string label, string triggerId, int satisfiedIfFacilityUnlocked = 0)
    {
        var o = ScriptableObject.CreateInstance<ReachTriggerObjective>();
        o.label = label; o.targetTriggerId = triggerId; o.satisfiedIfFacilityUnlocked = satisfiedIfFacilityUnlocked;
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

    static TutorialVideoObjective CreateVideoTutorial(string name, string label, params VideoTutorialPage[] pages)
    {
        var o = ScriptableObject.CreateInstance<TutorialVideoObjective>();
        o.label = label;   // 팝업이 화면을 덮어 트래커엔 거의 안 보이지만 일관성 위해 지정
        o.pages = pages;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    // 한 페이지 = 영상 + 제목 + 본문. 제목이 곧 파일명 -> Assets/12.Video/Tutorial/{제목}.mp4 자동연결.
    static VideoTutorialPage VPage(string title, string body)
        => new VideoTutorialPage { clip = LoadVideoClip(title), title = title, body = body };

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

    static CoreOpenObjective CreateCoreOpen(string name, string label)
    {
        var o = ScriptableObject.CreateInstance<CoreOpenObjective>();
        o.label = label;
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

    static RailItemMoveObjective CreateRailItemMove(string name, string label, int count = 1)
    {
        var o = ScriptableObject.CreateInstance<RailItemMoveObjective>();
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

    // ============================================================
    // 영상 UI 스프라이트 / 클립 로드
    // ============================================================
    static void EnsureVideoUiSprites()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "TutorialVideo");
        // 영상 테두리 = 인벤 "슬롯" 강조 프레임(네모 닫힌 형). 아래 뚫린 region 프레임 아님.
        CopySpriteToResources("Assets/11.UI/New/hl_slot_frame@2x.png",
                              "Assets/Resources/TutorialVideo/vid_frame.png");
    }

    static void CopySpriteToResources(string src, string dst)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(src) == null) return;     // 원본 없음 -> 런타임 폴백
        if (AssetDatabase.LoadAssetAtPath<Sprite>(dst) != null)
            AssetDatabase.DeleteAsset(dst);                                    // 소스 바뀌면 갈아끼우게 기존 것 삭제
        AssetDatabase.CopyAsset(src, dst);                                     // 9-slice/sprite 설정 보존
    }

    static VideoClip LoadVideoClip(string fileName)
    {
        // 흔한 확장자 순서로 탐색 (mp4 권장). 파일명 = 페이지 제목.
        foreach (var ext in new[] { "mp4", "webm", "mov" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>($"{VideoFolder}/{fileName}.{ext}");
            if (clip != null) return clip;
        }
        return null;   // 아직 클립 없음 - 팝업은 텍스트 + '영상 준비 중' 으로 정상 동작
    }

    static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(parent, name);
    }
}
