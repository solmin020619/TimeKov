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
    const int StorageId = 8;        // 저장고 (레일 아이템을 창고로 넣음 = 창고 출력 포트 반대) - 튜토 필수 아님(숨김)
    const int TotalFacilityCount = 8;   // 전체 설비 수 (마무리 "설비 해금하기" 목표 = x/8). 설비 수 바뀌면 여기 수정.

    // -- 최종 ItemData 시트 기준 --
    const int ItemSpiderVenom = 1102;  // 거미 독액 (드롭, 원료)
    const int ItemCorrosive   = 3101;  // 부식액 (드롭, 원료)
    const int ItemHealGel     = 1201;  // 앰플 젤 (R1201 @추출기1: 1102+3101). 시트 rename: 회복 젤 -> 앰플 젤
    const int ItemHealAmpoule = 5101;  // 초급 회복 앰플 (R5101 @배양기2: 1201)
    const int CoreKitId       = 6101;  // 코어 키트 I (코어 1단계 강화 재료)
    const int CoreKitAmount   = 3;     // CoreLevelData lv1 requiredAmount
    const int ItemTwig        = 4101;  // 나뭇가지 = 설비 연료 (FuelConfig.fuelItemId). OakTreeEnt 단독 드롭.
    const int StarterKitId    = 8301;  // 자연 충전 키트 (엔드게임 부트스트랩: 첫 전송 5% -> 시간에너지 합성기(3) 해금).
                                       //   TransmissionManager.EnsureKitDefs 규약(83xx=지역 일반 키트, 자연=끝자리1)과 일치.

    // -- 스포트라이트 타깃 id (씬 UI 요소의 TutorialHighlightTarget 와 매칭) --
    const string TargetTimeBar      = "time_bar";        // 좌하단 시간/DECAY 막대
    const string TargetStaminaBar   = "stamina_bar";     // 스태미나 막대 (인트로에서 로직 설명)
    const string TargetStatPanel    = "status_panel";    // C 스탯 창
    const string TargetStatButton   = "stat_button";     // 우측 상단 C 스탯 아이콘(C_Icon)
    const string TargetTabIcon      = "tab_icon";        // 우측 상단 TAB(인벤) 아이콘
    const string TargetMenuIcons    = "menu_icons";      // 우측 상단 메뉴 전체(TAB/B/C/ESC) 합집합
    const string TargetQuickSlots    = "quick_slots";    // 건설 퀵슬롯 바
    const string TargetRailSlot      = "rail_slot";      // E 레일 슬롯
    const string TargetBuildDemolish = "build_demolish"; // X 일괄조작 힌트
    const string TargetBuildRotate   = "build_rotate";   // R 설비회전 힌트
    const string TargetSkillQ        = "skill_q";        // 우하단 스킬 슬롯 Q
    const string TargetSkillsAll     = "skills_qer";     // 우하단 스킬 3칸(Q/E/R) 합집합

    const string Y = "<color=#FFCC00>";  // 강조 색 열기
    const string E = "</color>";          // 닫기

    // -- 데모(자연맵 전용) 토글 --
    // true = 설원/사막/용암 구역 퀘스트(quest_end_05~10)를 아예 만들지 않는다.
    //   자연맵만 여는 데모 빌드에서는 그 구역에 갈 수 없어 objective 가 영구 미완료로 남기 때문.
    //   남는 엔드게임 라인 = 전송기 이동 -> 첫 전송 5% -> 와이번 처치 + 전송률 25% -> 우주선 Lv.5(= 데모 끝).
    // 본편 복귀 = false 로 바꾸고 Tools/Quest/Generate Tutorial Assets 재실행.
    //   ★재실행하면 quest_end_05~10 과 그 objective 들이 git 에서 deleted 로 뜬다(삭제도 같이 커밋할 것).
    //   ★const 가 아니라 static readonly 인 이유: const 면 컴파일러가 아래 if 블록을
    //     도달 불가로 판정해 CS0162 경고를 띄운다. 그 블록은 본편 복귀용이라 지울 수 없다.
    static readonly bool DemoNatureOnly = true;

    // -- 영상 팝업 토글 --
    // false 로 두고 재생성하면 영상 단계가 빠진다(설명만 사라지고 행동 퀘는 그대로라 흐름은 완주 가능). 안전 복원용.
    //   위 DemoNatureOnly 와 같은 이유로 const 를 쓰지 않는다 - false 로 내리는 순간
    //   이 값을 보는 if 블록 8곳이 전부 CS0162 경고를 뿜는다.
    static readonly bool EnableVideoTutorials = true;
    // 영상 클립은 이 폴더에서 "페이지 제목 == 파일명"으로 자동 로드. 종욱이 제목 그대로 mp4 를 여기 넣으면 됨.
    const string VideoFolder = "Assets/17.Video/Tutorial";

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

        // 영상 클립을 떨궈둘 폴더 보장.
        //   ★스프라이트 복사(EnsureVideoUiSprites)는 08-07 에 제거했다 - 아래 '영상 클립 로드' 주석 참고.
        //     매 실행마다 GUID 를 새로 만들어 씬의 영상 테두리 참조를 끊어놓던 코드였다.
        if (EnableVideoTutorials)
        {
            EnsureFolder("Assets", "17.Video");
            EnsureFolder("Assets/17.Video", "Tutorial");
        }

        var quests = new List<QuestSO>();

        // ============================================================
        // 도입 - 시작 안내 (스포트라이트 투어, 9->5스텝 압축)
        //   인트로는 HUD 위치를 짚는 게 본질이라 영상 대신 스포트라이트 유지.
        // ============================================================
        quests.Add(BuildQuest("quest_tut_00_intro", "상태 확인하기",
            CreateGuidedTour("obj_intro_tour",
                TourStep($"이 게임은 {Y}체력{E}이 곧 {Y}시간{E}입니다. 시간이 {Y}0{E}이 되면 사망합니다.", TargetTimeBar),
                TourStep($"{Y}스태미나{E}입니다. {Y}달리기{E}나 {Y}대시{E}를 쓰면 줄고, {Y}멈추거나 걸으면{E} 다시 찹니다. 바닥나면 잠시 달리기나 대시를 쓸 수 없어요.", TargetStaminaBar),
                TourStep($"우상단 메뉴 - {Y}K{E}: 도감 / {Y}TAB{E}: 인벤토리 / {Y}B{E}: 건설 모드 / {Y}ESC{E}: 설정.", TargetMenuIcons),
                TourStep($"{Y}C{E} 키를 눌러 {Y}스탯 창{E}을 열어보세요.", TargetStatButton, KeyCode.C),
                TourStep($"여기서 {Y}최대 시간{E}·{Y}스태미나{E}·{Y}공격력{E}·{Y}방어력{E}을 확인합니다. {Y}코어 강화{E}로 최대 시간을, {Y}앰플 제작{E}으로 나머지 스탯을 올릴 수 있어요. {Y}C{E}로 언제든 여닫을 수 있습니다.", TargetStatPanel),
                TourStep($"우하단 {Y}스킬{E} 3개 - {Y}Q·E·R{E}로 사용하고, 쓰고 나면 {Y}쿨타임{E}이 끝나야 다시 쓸 수 있어요.", TargetSkillsAll))));

        // ============================================================
        // 조작 - 프롤로그(불시착 전)에서 WASD 이동/Space 점프/Shift 달리기를 이미 익히고 넘어온다.
        //   여기서 재교육하지 않는다(중복 제거). 대시(우클릭)는 코어 1강 해금이라 초반 튜토 제외.
        // ============================================================

        quests.Add(BuildQuest("quest_tut_01b_reach_hunt", "결계 밖 탐사 지점으로 이동하기",
            CreateReachTrigger("obj_reach_enemy", $"결계 밖 {Y}사냥터{E}로 {Y}이동{E}하세요.", "enemy")));

        // ============================================================
        // [영상] 전리품 + 상자 - 사냥터 '도착 직후'(곧 전투 직전)에 발화하도록 도착 퀘 바로 뒤 독립 퀘.
        //   ★도착 퀘 '안' 병렬 objective로 넣지 말 것 - present 즉시 활성이라 출발 전(이동 지시)에 터진다.
        //   독립 퀘여야 도착 완료=present=발화. 드롭/창고/상자를 한 묶음으로(전투 직후 하드컷이던 걸 도착 beat로).
        // ============================================================
        if (EnableVideoTutorials)
            quests.Add(BuildLootVideoQuest());

        quests.Add(BuildQuest("quest_tut_02_combat", "위협 개체 처치하기",
            CreatePressKey("obj_attack", $"{Y}좌클릭{E}으로 {Y}공격{E}하세요.", KeyCode.Mouse0, 1),
            CreateEnemyKill("obj_kill", $"{Y}위협 개체{E}를 {Y}처치{E}하세요.", "", 1)));   // enemyId 빈값 = 아무 몹이나 1마리(거미/언데드/오크 다 인정)

        quests.Add(BuildQuest("quest_tut_03_loot", "전투 자원 회수하기",
            CreateItemAcquire("obj_loot_venom", $"{Y}거미{E}를 잡아 {Y}거미 독액{E}을 {Y}2개{E} {Y}획득{E}하세요.", ItemSpiderVenom, 2),
            CreateItemAcquire("obj_loot_corrosive", $"{Y}언데드{E}를 잡아 {Y}부식액{E}을 {Y}2개{E} {Y}획득{E}하세요.", ItemCorrosive, 2),
            CreateItemAcquire("obj_loot_twig", $"{Y}오크 트리{E}를 잡아 {Y}나뭇가지{E}를 {Y}2개{E} {Y}획득{E}하세요.", ItemTwig, 2),
            CreatePressKey("obj_inventory", $"{Y}Tab{E}으로 {Y}인벤토리{E}를 확인하세요.", KeyCode.Tab, 1)));

        // ============================================================
        // 건설 (★건설구역 도착은 별 퀘로 유지 - 합치면 소프트락)
        // ============================================================
        // (생체 추출기1/배양기2 는 시작부터 해금 상태 = FacilityUnlockManager 기본해금.
        //  튜토에서 '추출기 이동/해금' 단계 제거 - 바로 건설로 넘어간다. 퀵슬롯에 이미 들어있음.)

        // ★건설구역 도착 - 별 퀘. EnterBuildMode/투어 앞에서 '존 안' 진입을 선행 보장.
        quests.Add(BuildQuest("quest_tut_05b_reach_build", "기지의 건설 구역으로 이동하기",
            CreateReachTrigger("obj_reach_build", $"결계 안 {Y}건설 구역{E}으로 {Y}이동{E}하세요.", "build")));

        // 건설 모드 진입 - 별 퀘(플레이어가 직접 B 를 눌러 배우게. 투어가 자동진입하면 'B 누르기' 학습이 묻힘)
        quests.Add(BuildQuest("quest_tut_06_enter_build", "건설 모드 진입하기",
            CreateEnterBuildMode("obj_build_mode", $"{Y}B{E}로 {Y}건설 모드{E}에 진입하세요.")));

        // [영상] 설비 설치/해제 (Shift 일괄해제 포함) - B로 건설 진입 직후 바로 영상으로 안내(스포트라이트 투어 대체).
        if (EnableVideoTutorials)
            quests.Add(BuildBuildingVideoQuest());

        quests.Add(BuildQuest("quest_tut_06b_place_extractor", "생체 추출기 설치하기",
            CreateFacilityPlace("obj_place_extractor", $"{Y}생체 추출기{E}를 {Y}설치{E}하세요.", BioExtractorId, 1)));

        // ============================================================
        // 설비 가공 - [영상] 열기/연료/재료/수령 전체를 한 큐로, 실제 행동은 별 objective
        // ============================================================
        if (EnableVideoTutorials)
            quests.Add(BuildFactoryVideoQuest());

        // (설비 '열기' 별도 게이트는 두지 않음 - 연료/재료 슬롯이 설비 UI 안에 있어 여는 게 자연 강제됨)
        // R1201: 거미독액 + 부식액 -> 앰플 젤. 연료+재료를 한 퀘(병렬 3목표)로 묶는다.
        //   이유: 연료/재료를 별개 순차 퀘로 나누면 그 사이에 통째 퀘가 끼어 lookback(3.5s)을 넘기고,
        //   플레이어가 자연스럽게 재료를 먼저 넣으면 재료 투입 목표가 활성될 땐 이미 만료돼 미인정 ->
        //   재료회수 후 재투입해야 깨지던 문제. 셋을 한 퀘로 두면 동시에 활성이라 어느 순서로 넣어도 다 잡힘.
        quests.Add(BuildQuest("quest_tut_08_operate", "첫 재료 가공하기",
            CreateFuelAdd("obj_fuel_add", $"{Y}나뭇가지{E}를 {Y}연료 슬롯{E}으로 {Y}드래그{E}해 투입하세요.", BioExtractorId),
            CreateFacilityInput("obj_in_venom", $"{Y}거미 독액{E}을 {Y}재료 슬롯{E}으로 {Y}드래그{E}해 투입하세요.", BioExtractorId, ItemSpiderVenom, 1),
            CreateFacilityInput("obj_in_corrosive", $"{Y}부식액{E}을 {Y}재료 슬롯{E}으로 {Y}드래그{E}해 투입하세요.", BioExtractorId, ItemCorrosive, 1)));

        // 앰플 젤 회수 (+ 다음 배양기/레일 가동 연료 나뭇가지 보상)
        quests.Add(BuildQuestRewarded("quest_tut_10_collect_gel", "가공 결과물 회수하기",
            new[] { new QuestSO.QuestReward { itemId = ItemTwig, amount = 5 } },
            CreateItemAcquire("obj_collect_gel", $"{Y}출력 슬롯{E}에서 {Y}앰플 젤{E}을 {Y}모두 받기{E}로 {Y}회수{E}하세요.", ItemHealGel, 1)));

        // ============================================================
        // 배양 - 두 번째 설비(반복 학습=정착, 안내 없이 텍스트만)
        // ============================================================
        // (배양기2 도 기본해금 - 이동/해금 objective 제거, 설치만. 이미 건설구역 안이라 도착 불필요)
        quests.Add(BuildQuest("quest_tut_11_build_cultivator", "생체 배양기 설치하기",
            CreateFacilityPlace("obj_place_cultivator", $"{Y}생체 배양기{E}를 {Y}설치{E}하세요.", BioCultivatorId, 1)));

        quests.Add(BuildQuestRewarded("quest_tut_12_cultivate", "회복 앰플 제작하기",
            new[] { new QuestSO.QuestReward { itemId = ItemTwig, amount = 1 } },
            CreateFacilityInteract("obj_interact_cultivator", $"{Y}F{E}로 {Y}생체 배양기{E}를 여세요.", BioCultivatorId, 1),
            CreateFacilityInput("obj_in_gel", $"{Y}앰플 젤{E}을 {Y}재료 슬롯{E}으로 {Y}드래그{E}해 투입하세요.", BioCultivatorId, ItemHealGel, 1)));

        // ★앰플 회수 / 사용 분리 (한 퀘에 ItemAcquire+ItemUse 두면 1개짜리 소비형 비대칭 갭락)
        quests.Add(BuildQuest("quest_tut_13_collect_ampoule", "회복 앰플 회수하기",
            CreateItemAcquire("obj_collect_ampoule", $"{Y}초급 회복 앰플{E}을 {Y}회수{E}하세요.", ItemHealAmpoule, 1)));

        // 사용 학습 + 보상으로 앰플 1개 더 지급 -> 다음 퀵슬롯 등록 퀘에서 가방에 둘 게 있어 바로 등록 시도 가능.
        quests.Add(BuildQuestRewarded("quest_tut_13b_use_ampoule", "회복 앰플 사용하기",
            new[] { new QuestSO.QuestReward { itemId = ItemHealAmpoule, amount = 1 } },
            CreateItemUse("obj_use_ampoule", $"{Y}초급 회복 앰플{E}을 {Y}사용{E}해 시간을 회복하세요.", ItemHealAmpoule, 1)));

        // [영상] 퀵슬롯 등록 안내 -> 곧바로 실제 등록 퀘(레일 영상으로 바로 넘어가 읽다 마는 느낌 방지).
        if (EnableVideoTutorials)
            quests.Add(BuildQuickslotVideoQuest());

        // 등록만 하면 바로 끝나 어색하니 사용까지(V) 한 번 해보게 2단계. 사용 단계는 'V로 시도'라
        // 만피 상태(회복 막힘)에서도 완료된다(소프트락 방지). 둘은 병렬이지만 V 사용은 등록 후에만 발생.
        quests.Add(BuildQuest("quest_tut_14b_quickslot_register", "회복 앰플 퀵슬롯 등록하기",
            CreateQuickSlotRegister("obj_quickslot_register",
                $"인벤토리에서 {Y}회복 앰플{E}을 {Y}우클릭 -> 퀵슬롯 등록{E}으로 {Y}V 슬롯{E}에 등록하세요."),
            CreateQuickSlotUse("obj_quickslot_use",
                $"{Y}V{E}를 눌러 등록한 소모품을 {Y}사용{E}해보세요.")));

        // ============================================================
        // 자동화 - [영상] 레일 개념/까는 법, 실제 연결/이동은 별 행동 퀘
        //   ★RailConnect(보상)와 RailItemMove 는 절대 한 퀘로 안 합침(보상선행 사슬)
        // ============================================================
        if (EnableVideoTutorials)
            quests.Add(BuildRailVideoQuest());

        // 보상(거미독액+부식액) = 다음 자동화 확인에서 추출기에 넣어 라인 흐르게 할 재료
        quests.Add(BuildQuestRewarded("quest_tut_16_rail_connect", "설비 사이 레일 연결하기",
            new[] {
                new QuestSO.QuestReward { itemId = ItemSpiderVenom, amount = 1 },
                new QuestSO.QuestReward { itemId = ItemCorrosive,   amount = 1 },
            },
            CreateRailConnect("obj_rail_connect", $"{Y}생체 추출기{E}를 {Y}생체 배양기{E}에 {Y}레일{E}로 이어보세요.", BioExtractorId, BioCultivatorId, 1)));

        quests.Add(BuildQuest("quest_tut_16b_rail_move", "레일 자동화 작동 확인하기",
            CreateRailItemMove("obj_rail_move",
                $"{Y}생체 추출기{E}에 재료를 넣고, 결과물이 {Y}레일{E}을 타고 {Y}생체 배양기{E}로 {Y}자동으로 넘어가는지{E} 확인하세요.", 1)));

        // ============================================================
        // 코어 강화 - 이동(보상 키트) -> [영상 안내 + 열기 + 강화] -> 결과/마무리
        //   ★이동(보상)과 강화는 별 퀘(보상선행). 키트는 이동 퀘 완료시 지급되어 강화 퀘 땐 손에 있음.
        // ============================================================
        quests.Add(BuildQuestRewarded("quest_tut_17_reach_core", "코어 강화 단말로 이동하기",
            new[] { new QuestSO.QuestReward { itemId = CoreKitId, amount = CoreKitAmount } },
            CreateReachTrigger("obj_reach_core", $"{Y}코어 강화 단말{E}이 있는 곳으로 {Y}이동{E}하세요.", "core")));

        // 코어 강화: 영상2p(설명) + 열기(행동) + 강화 시도(행동) 한 퀘.
        //   영상을 CoreOpen '앞'에 둬 '설명 보고 -> F로 열고 -> 강화' 순(코어 UI 먼저 열렸다 영상이 덮는 겹침 제거).
        //   같은 퀘 내 objective 는 영상이 화면을 덮어 입력차단되므로 CoreOpen 입력은 영상 닫은 뒤 받음.
        var coreObjs = new List<ObjectiveSO>();
        if (EnableVideoTutorials)
            coreObjs.Add(CreateVideoTutorial("obj_core_video", "코어 강화 안내를 확인하세요.",
                VPage("코어 강화란",
                    $"{Y}F{E}로 여는 {Y}코어 강화 단말{E}입니다. {Y}코어 강화{E}로 {Y}최대 시간(체력){E}을 늘리며, 단계가 오를수록 더 많은 {Y}코어 키트{E}가 필요합니다({Y}코어 합성기{E}에서 제작). 강화는 {Y}성공·실패와 무관하게 키트를 소모{E}하니 {Y}성공 확률{E}을 꼭 확인하세요."),
                VPage("코어 강화 방법",
                    $"{Y}강화 시작{E}을 누르면 코어가 {Y}시계{E}로 바뀝니다. 바늘이 {Y}초록 성공존{E}에 올 때 {Y}정지! 버튼(또는 Space){E}으로 멈추면 성공 확률이 오릅니다. {Y}강화 버튼{E}과 {Y}성공 확률 바{E}는 코어 강화 창에 있습니다.")));
        coreObjs.Add(CreateCoreOpen("obj_open_core", $"{Y}F{E}로 {Y}코어 강화 단말{E}을 여세요."));
        coreObjs.Add(CreateCoreUpgrade("obj_core_upgrade", $"{Y}강화 시작{E}을 누르고 {Y}정지! 버튼{E}으로 멈춰 코어를 강화해 보세요.", 0));
        quests.Add(BuildQuest("quest_tut_18_core", "코어 강화하기", coreObjs.ToArray()));

        // (강화 결과/마무리 안내 팝업 제거 - 코어 UI 위에 또 뜨는 게 불필요. 아래 엔드게임 라인이 다음 방향 제시.)
        // (구 quest_tut_22 "숨겨진 설비 찾기" 상시목표 제거 - 경제 재설계로 설비 3~9 는 전송 마일스톤 해금이라
        //  '맵에서 F로 줍기'가 아님. 추출기1/배양기2 만 튜토 F해금. 방향 제시는 아래 엔드게임 라인이 담당.)

        // ============================================================
        // [3층] 병렬 엔드게임 라인 (별 카테고리 = 메인 튜토 옆 두 번째 트래커, 온보딩 완료 후 등장/휴면).
        //   전송/우주선/탈출을 직렬 온보딩에 안 끼우고 장기 목표로 상시 표시.
        //   부트스트랩: 스타터 충전 키트로 첫 전송 5% -> 시간에너지 합성기(3) 해금 -> 이후 키트 자급(순환잠금 회피).
        // ============================================================
        var endgameQuests = new List<QuestSO>();

        // 전송기로 이동 - 완료 시 스타터 충전 키트 지급(이걸로 다음 퀘에서 첫 전송).
        endgameQuests.Add(BuildQuestRewarded("quest_end_01_reach_transmit", "시간에너지 전송기 찾기",
            new[] { new QuestSO.QuestReward { itemId = StarterKitId, amount = 1 } },
            CreateReachTrigger("obj_reach_transmit",
                $"기지의 {Y}시간에너지 전송기{E}로 {Y}이동{E}하세요.", "transmit")));

        // 첫 전송 - 스타터 키트를 넣어 5% 달성. 5% 보상으로 시간에너지 합성기(3) 자동 해금 -> 이후 키트 자급.
        //   영상(설명)을 전송 '앞'에 둬 '전송기 도착 -> 설명 보고 -> 전송' 순(코어 강화와 동일 패턴).
        //   전에는 interact:transmit 발견 큐라 F로 UI 연 뒤 영상이 떠 타이밍이 어긋났다 -> 도착 퀘 다음 영상 objective 로 이동.
        var transmitObjs = new List<ObjectiveSO>();
        if (EnableVideoTutorials)
            transmitObjs.Add(CreateVideoTutorial("obj_transmit_video", "시간에너지 전송 안내를 확인하세요.",
                // 문구 = 기획 확정본(08-07 라디오 통합 진행표 28행). 100% 목표를 약속하지 않아 데모/본편 공용이다.
                VPage("시간에너지 전송",
                    $"{Y}충전 키트{E}를 전송하면 {Y}전송률{E}이 오릅니다. 전송률이 오르면 {Y}보급 신호{E}와 다음 목표가 열립니다.")));
        transmitObjs.Add(CreateTransmissionRate("obj_first_transmit",
            $"{Y}F{E}로 전송기를 열고 {Y}충전 키트{E}를 전송해 전송률 {Y}5%{E}를 달성하세요.", 5));
        endgameQuests.Add(BuildQuest("quest_end_02_first_transmit", "시간에너지 키트 전송하기", transmitObjs.ToArray()));

        // ── 구역 캠페인 (5% -> 100%) : 4구역 x (보스 처치 + 전송률) + 우주선 수리 3회 -> 탈출 ──
        //   전송은 25%씩 4구역(자연/설원/사막/용암). 구역 경계는 보스 재료 특수 키트가 필요 = 보스 처치가 핵심 서브.
        //   보상은 전송 마일스톤(TransmissionManager)이 자동 지급하므로 여기 퀘엔 보상 없음(방향 제시 전용).
        //   ★보스 enemyId: 자연=wyvern_boss / 설원=ice_elemental_boss / 사막=sand_elemental_boss / 용암=fire_boss.
        //     (와이번=자연맵 가정 - 씬 배치 확인 필요. 나머지 3원소는 테마==맵 자명)
        //   ★소프트락 후보(종욱 플레이검증): 보스가 죽으면 Destroy(리스폰 안 하는 직접배치면 일회성). 해당 구역 퀘가
        //     활성화되기 '전에' 보스를 미리 잡으면(RecentCount 3.5초 창 지나) EnemyKill 이 영구 미완료.
        //     보스가 리스폰하거나 조기 도달 불가면 문제 없음. 리스폰 안 하면 % 단일 objective 로 바꾸면 됨(라벨에 보스 명시).

        // 자연 구역 (5 -> 25%). 25% 마일스톤이 우주선 Lv.6 재료(선체 보강재)를 지급 - 데모(Lv.5)에선 안 쓰고 다음 구간용.
        endgameQuests.Add(BuildQuest("quest_end_03_region_nature", "자연권역 전송률 25% 달성하기",
            CreateEnemyKill("obj_boss_nature", $"자연권역의 강한 {Y}생체 반응{E} 개체를 {Y}처치{E}하세요.", "wyvern_boss", 1),
            CreateTransmissionRate("obj_rate_25", $"충전 키트를 전송해 전송률 {Y}25%{E}를 달성하세요.", 25)));

        // 자연맵 복구 에너지 15개로 Lv.5 까지 올라간다(특수부품 불필요). 데모의 최종 목표.
        endgameQuests.Add(BuildQuest("quest_end_04_ship_lv5", "우주선 Lv.5 복구 진행하기",
            CreateShipRepairLevel("obj_ship_lv5",
                $"모은 {Y}복구 에너지{E}로 우주선을 {Y}Lv.5{E}까지 복구하세요.", 5)));

        // 아래 3구역 + 탈출은 자연맵 밖이라 데모 빌드에서는 통째로 만들지 않는다(DemoNatureOnly).
        // 만들어두면 갈 수 없는 보스/전송률이 목표로 떠서 영구 미완료로 남는다.
        if (!DemoNatureOnly)
        {
            // 설원 구역 (25 -> 50%). 30% 코어 합성기.
            endgameQuests.Add(BuildQuest("quest_end_05_region_snow", "설원 구역 돌파",
                CreateEnemyKill("obj_boss_snow", $"설원의 {Y}얼음정령{E}을 {Y}처치{E}하세요.", "ice_elemental_boss", 1),
                CreateTransmissionRate("obj_rate_50", $"전송률 {Y}50%{E}를 달성하세요.", 50)));

            endgameQuests.Add(BuildQuest("quest_end_06_ship_lv8", "2차 우주선 수리",
                CreateShipRepairLevel("obj_ship_lv8",
                    $"{Y}동력 안정기{E}로 우주선을 {Y}Lv.8{E}까지 수리하세요.", 8)));

            // 사막 구역 (50 -> 75%). 70% 창고 상한.
            endgameQuests.Add(BuildQuest("quest_end_07_region_desert", "사막 구역 돌파",
                CreateEnemyKill("obj_boss_desert", $"사막의 {Y}모래정령{E}을 {Y}처치{E}하세요.", "sand_elemental_boss", 1),
                CreateTransmissionRate("obj_rate_75", $"전송률 {Y}75%{E}를 달성하세요.", 75)));

            endgameQuests.Add(BuildQuest("quest_end_08_ship_lv10", "최종 우주선 수리",
                CreateShipRepairLevel("obj_ship_lv10",
                    $"{Y}우주선 엔진{E}으로 우주선을 {Y}Lv.10{E}까지 완전 수리하세요.", 10)));

            // 용암 구역 (75 -> 100%). 80/90% 창고 상한·앰플 꾸러미·코어 키트. 100% 도달 = 엔딩 조건.
            endgameQuests.Add(BuildQuest("quest_end_09_region_lava", "용암 구역 돌파",
                CreateEnemyKill("obj_boss_lava", $"용암의 {Y}화염정령{E}을 {Y}처치{E}하세요.", "fire_boss", 1),
                CreateTransmissionRate("obj_rate_100", $"전송률 {Y}100%{E}를 달성하세요.", 100)));

            // 탈출 - 우주선 완전 수리 + 전송 100% 둘 다 충족 시 탈출. (앞 퀘로 이미 달성돼 있으면 즉시 완료 = 승리 확인)
            endgameQuests.Add(BuildQuest("quest_end_10_escape", "탈출",
                CreateShipRepairLevel("obj_escape_ship", $"우주선을 {Y}Lv.10{E}까지 완전 수리하세요.", 10),
                CreateTransmissionRate("obj_escape_rate", $"시간에너지 {Y}100%{E}를 달성해 {Y}탈출{E}하세요.", 100)));
        }

        // [2층] 상황별 발견 팝업 데이터셋 생성 (이벤트 처음 발생 시 1회 설명 팝업. Resources 런타임 로드).
        BuildDiscoveryCueSet();

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
        cat.activateAfterCategoryId = "";   // 메인은 시작부터 활성
        EditorUtility.SetDirty(cat);

        // 엔드게임 카테고리 (GUID 유지) - 메인 튜토 완료 후 등장(휴면). 병렬 트래커로 상시 표시.
        string endCatPath = $"{CategoriesFolder}/Cat_Tutorial_Endgame.asset";
        var endgameCat = AssetDatabase.LoadAssetAtPath<CategorySO>(endCatPath);
        if (endgameCat == null)
        {
            endgameCat = ScriptableObject.CreateInstance<CategorySO>();
            AssetDatabase.CreateAsset(endgameCat, endCatPath);
        }
        endgameCat.id = "tutorial_endgame";
        endgameCat.title = "엔드게임";
        endgameCat.quests = endgameQuests.ToArray();
        endgameCat.activateAfterCategoryId = "tutorial_main";   // 메인 튜토 완료 후 등장
        EditorUtility.SetDirty(endgameCat);

        // TutorialSO (GUID 유지)
        string tutPath = $"{TutorialsFolder}/Tutorial_Main.asset";
        var tut = AssetDatabase.LoadAssetAtPath<TutorialSO>(tutPath);
        if (tut == null)
        {
            tut = ScriptableObject.CreateInstance<TutorialSO>();
            AssetDatabase.CreateAsset(tut, tutPath);
        }
        tut.savePrefix = "tutorial";
        tut.categories = new[] { cat, endgameCat };
        EditorUtility.SetDirty(tut);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 도감 튜토 탭 재시청 목록 갱신 (퀘 영상 + 발견 큐 페이지 모두 포함) - 한 메뉴로 동기화.
        CodexConfigPopulator.PopulateTutorial();

        Debug.Log(
            $"[TutorialAssetBuilder] 생성 완료 - 메인 {quests.Count}개 + 엔드게임 {endgameQuests.Count}개 (영상 {(EnableVideoTutorials ? "ON" : "OFF")}).\n" +
            $"씬 세팅 체크:\n" +
            $"1. QuestManager.tutorial = Tutorial_Main.asset (카테고리 2개: 메인 + 엔드게임)\n" +
            $"2. QuestTrigger(IsTrigger, Player Tag): 'enemy'(사냥터)/'build'(BuildZone과 겹침)/'1'(추출기위치)/'2'(배양기위치)/'core'(코어단말)/'transmit'(시간에너지 전송기). 오타/중복/콜라이더 범위 확인\n" +
            $"3. 'build' 트리거가 BuildManager.buildZoneCollider 와 정확히 겹쳐야(ReachTrigger는 되는데 EnterBuildMode가 안 되는 모순 방지)\n" +
            $"4. 드롭/스폰: tutorial_enemy 무한리스폰 + 거미독액{ItemSpiderVenom}/부식액{ItemCorrosive} 드롭, OakTreeEnt 가 나뭇가지{ItemTwig} 떨굼(스폰풀에 OakTreeEnt 포함, NavMesh 베이크)\n" +
            $"5. 스포트라이트(코드 자동등록 외 수동 부착): time_bar/status_panel/stat_button(C_Icon)/tab_icon + 건설투어 타깃. 코어는 영상이라 코어 스포트 불필요\n" +
            $"6. PlayerMovementWatcher: 이동/점프/달리기(Shift)/대시/Tab/B 등 필수 키는 코드에서 자동 감지. 프롤로그가 이동/점프/달리기 선행 교육(여기선 재교육 안 함)\n" +
            $"7. 영상 클립: Assets/17.Video/Tutorial/ 에 '페이지 제목'과 같은 파일명 mp4(발견 큐 포함). 없으면 '영상 준비 중' 폴백\n" +
            $"8. 레일 자동화: 추출기 출구->배양기 입구 포트정렬 + 다른 연결가능 포트 차단 사전 플레이테스트\n" +
            $"9. 코어: 단말이 BaseZone 콜라이더 안 + 코어키트 보상 증발 방지(가방/창고 여유). 시트 코어레벨1 키트={CoreKitId}/필요수<={CoreKitAmount}\n" +
            $"10. [엔드게임] 'transmit' 트리거 = TransmissionComputerTerminal 위치. 스타터 키트 itemId={StarterKitId}(자연 일반 키트)가 ItemData 시트에 있고 전송 가능해야 부트스트랩 성립\n" +
            $"11. [발견 팝업] DiscoveryCueManager 는 런타임 자동 생성(씬 배치 불필요). 큐셋=Resources/DiscoveryCues/DiscoveryCueSet");
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
    // 전리품 = 줍기/창고 + 상자(F열기/G즉시) 페이지 흡수. 사냥터 도착 순간 한 묶음으로 1회 발화(단독 상자팝업 제거).
    static QuestSO BuildLootVideoQuest()
        => BuildQuest("quest_tut_02v_loot_video", "전투 자원 회수 방법 확인하기",
            CreateVideoTutorial("obj_loot_video", "전투 자원 회수 방법을 확인하세요.",
                VPage("아이템 줍기",
                    $"적이나 오브젝트에서 나온 {Y}아이템{E}은 가까이 가면 {Y}획득{E}됩니다."),
                VPage("창고",
                    $"{Y}가방{E}이 가득 차면 아이템이 {Y}창고{E}로 자동 보관됩니다. 창고를 열어 필요한 아이템을 {Y}꺼내 쓸 수 있어요{E}."),
                VPage("결계 안과 밖",
                    $"결계 안에서는 {Y}시간 감소가 멈춥니다{E}. 결계 밖에서는 {Y}시간이 줄어듭니다{E}."),
                VPage("상자파밍",
                    $"맵에서 발견한 {Y}상자{E}는 {Y}F{E}로 엽니다. {Y}등급 높은{E} 아이템이 들어 있어요."),
                VPage("즉시완료",
                    $"기다리지 않고 {Y}G{E}를 눌러 상자를 {Y}즉시{E} 열 수도 있습니다.")));

    static QuestSO BuildUnlockVideoQuest()
        => BuildQuest("quest_tut_04v_unlock_video", "새 설비 확인하기",
            CreateVideoTutorial("obj_unlock_video", "새로 열린 설비를 확인하세요.",
                VPage("설비해금",
                    $"맵에 떨어진 {Y}설비{E}는 {Y}F{E}로 {Y}해금{E}합니다. 해금하면 {Y}건설 퀵슬롯{E}에 추가돼요."),
                VPage("즉시해금",
                    $"기다리지 않고 {Y}G{E}를 눌러 설비를 {Y}즉시 해금{E}할 수도 있습니다.")));

    static QuestSO BuildFactoryVideoQuest()
        => BuildQuest("quest_tut_07v_factory_video", "설비 가공 방법 확인하기",
            CreateVideoTutorial("obj_factory_video", "설비 가공 방법을 확인하세요.",
                VPage("설비 열기",
                    $"설치한 설비에 다가가 {Y}F{E}를 누르면 설비 UI가 열립니다. 이 화면에서 {Y}연료{E}와 {Y}재료{E}를 넣어 원하는 물건을 {Y}가공{E}할 수 있습니다."),
                VPage("재료 투입과 가공",
                    $"먼저 {Y}연료{E}가 필요합니다. 가방의 {Y}나뭇가지{E}를 {Y}연료 슬롯{E}에 드래그해 넣으세요(나뭇가지는 {Y}오크 트리{E}에서 나옵니다). 이어서 {Y}가방이나 창고{E}의 재료를 {Y}재료 슬롯{E}으로 드래그하면, 맞는 {Y}레시피{E}가 있을 때 {Y}가공{E}이 시작됩니다."),
                VPage("생산품 수령",
                    $"{Y}가공이 완료{E}되면 {Y}모두 받기{E}로 생산품을 가방이나 창고로 {Y}수령{E}할 수 있습니다."),
                VPage("레시피 변경",
                    $"한 설비가 여러 {Y}레시피{E}를 만들 수 있을 때, 만들고 싶은 {Y}레시피{E}를 {Y}클릭{E}해 {Y}바꿀 수 있습니다{E}. (한 번에 하나의 레시피만 가동)")));

    static QuestSO BuildRailVideoQuest()
        => BuildQuest("quest_tut_15v_rail_video", "레일 자동화 확인하기",
            CreateVideoTutorial("obj_rail_video", "레일 자동화 안내를 확인하세요.",
                VPage("레일 자동화란",
                    $"이제 {Y}자동화{E}를 배워봅시다. 설비를 {Y}레일{E}로 이으면, 아이템이 {Y}자동으로{E} 다음 설비로 이동해 {Y}직접 회수{E}할 필요가 없어집니다. (레일은 {Y}건설 모드(B){E}에서 깝니다)"),
                VPage("레일 까는 법",
                    $"{Y}건설 모드(B){E}에서 {Y}레일(E){E}을 고른 뒤, 설비의 {Y}출구(E 표시){E}를 클릭해 다음 설비까지 이어 주세요.")));

    // 설비 설치 + 해제 (Shift 일괄해제 포함). 건설 조작 투어 뒤에 발화.
    static QuestSO BuildBuildingVideoQuest()
        => BuildQuest("quest_tut_06v_building_video", "설비 설치 방법 확인하기",
            CreateVideoTutorial("obj_building_video", "설비 설치 방법을 확인하세요.",
                VPage("설비 설치",
                    $"{Y}건설 모드(B){E}에서 {Y}퀵슬롯{E}의 설비를 고른 뒤 {Y}건설 구역{E} 바닥을 클릭해 설치합니다. 놓을 수 있으면 {Y}초록{E}, 안 되면 {Y}빨강{E}으로 표시돼요. ({Y}R{E}로 회전)"),
                VPage("설비 해제",
                    $"{Y}X{E}로 {Y}해제 모드{E}에 들어가 설비를 {Y}클릭{E}하면 철거됩니다. {Y}Shift{E}를 누른 채 {Y}드래그{E}하면 {Y}여러 개를 한 번에{E} 해제할 수 있어요.")));

    // 퀵슬롯 등록 - 소모품을 우클릭으로 V 퀵슬롯에 등록해 전투 중 즉시 사용.
    static QuestSO BuildQuickslotVideoQuest()
        => BuildQuest("quest_tut_14v_quickslot_video", "퀵슬롯 사용 방법 확인하기",
            CreateVideoTutorial("obj_quickslot_video", "퀵슬롯 사용 방법을 확인하세요.",
                VPage("퀵슬롯 등록",
                    $"자주 쓰는 아이템은 {Y}퀵슬롯{E}에 등록해 빠르게 사용할 수 있습니다. 인벤토리에서 {Y}우클릭 -> 퀵슬롯 등록{E}, 사용은 {Y}V{E}입니다. ({Y}가방에 있을 때만{E} 사용됩니다)")));

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

    static QuickSlotRegisterObjective CreateQuickSlotRegister(string name, string label)
    {
        var o = ScriptableObject.CreateInstance<QuickSlotRegisterObjective>();
        o.label = label;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static QuickSlotUseObjective CreateQuickSlotUse(string name, string label)
    {
        var o = ScriptableObject.CreateInstance<QuickSlotUseObjective>();
        o.label = label;
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

    // 한 페이지 = 영상 + 제목 + 본문. 제목이 곧 파일명 -> Assets/17.Video/Tutorial/{제목}.mp4 자동연결.
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

    static RailConnectObjective CreateRailConnect(string name, string label, int sourceFacilityId, int targetFacilityId, int count = 1)
    {
        var o = ScriptableObject.CreateInstance<RailConnectObjective>();
        o.label = label; o.sourceFacilityId = sourceFacilityId; o.targetFacilityId = targetFacilityId; o.requiredCount = count;
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

    static TransmissionRateObjective CreateTransmissionRate(string name, string label, int targetRate)
    {
        var o = ScriptableObject.CreateInstance<TransmissionRateObjective>();
        o.label = label; o.targetRate = targetRate;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    static ShipRepairLevelObjective CreateShipRepairLevel(string name, string label, int targetLevel)
    {
        var o = ScriptableObject.CreateInstance<ShipRepairLevelObjective>();
        o.label = label; o.targetLevel = targetLevel;
        AssetDatabase.CreateAsset(o, $"{ObjectivesFolder}/{name}.asset");
        return o;
    }

    // ============================================================
    // [2층] 상황별 발견 팝업 데이터셋 (Resources 런타임 로드)
    //   설비 소개(3~9) / 귀환석 / 전송 소개. 전부 기지 안 이벤트라 safe=true(즉시 팝업).
    //   ★title 은 전역 유일(시청기록 키) + 기존 퀘영상 제목과 겹치지 않게 '소개' 접미로 구분.
    //   영상은 나중에 일괄 촬영(제목==파일명 자동연결). 지금은 텍스트 + '영상 준비 중' 폴백.
    // ============================================================
    static void BuildDiscoveryCueSet()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "DiscoveryCues");
        string path = "Assets/Resources/DiscoveryCues/DiscoveryCueSet.asset";

        var set = AssetDatabase.LoadAssetAtPath<DiscoveryCueSet>(path);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<DiscoveryCueSet>();
            AssetDatabase.CreateAsset(set, path);
        }

        set.cues = new List<DiscoveryCue>
        {
            // 설비 소개 - 전송 마일스톤으로 해금되는 순간(전송기 앞 = 기지 안) 1회.
            Cue("facility:3", true, VPage("시간에너지 합성기 소개",
                $"시간에너지 {Y}충전 키트{E}를 만드는 설비입니다. 여기서 만든 키트를 {Y}전송기{E}에 넣어 전송률을 올립니다.")),
            Cue("facility:6", true, VPage("용해로 소개",
                $"재료를 녹여 {Y}주괴{E} 등 기초 소재를 만드는 설비입니다.")),
            Cue("facility:9", true, VPage("창고 출력 포트 소개",
                $"{Y}창고{E}의 아이템을 {Y}레일{E}로 꺼내 다른 설비에 자동 공급하는 설비입니다.")),
            Cue("facility:8", true, VPage("저장고 소개",
                $"레일로 들어온 아이템을 {Y}창고에 자동 보관{E}하는 설비입니다. {Y}창고 출력 포트{E}(창고에서 꺼내기)의 반대입니다.")),
            Cue("facility:7", true, VPage("코어 합성기 소개",
                $"{Y}코어 강화{E}에 쓰는 {Y}코어 키트{E}를 만드는 설비입니다.")),
            // 생체 분리기 + 에너지 변환기 = 60% 동시 해금(원재료 무한복제 쌍). 한 팝업에 3페이지로 묶는다.
            // (facility:5 는 별도 큐 없음 - 4번 큐가 셋을 다 보여준다.)
            Cue("facility:4", true,
                VPage("생체 분리기 소개",
                    $"생체 재료를 분리해 {Y}상위 원료{E}를 얻는 설비입니다. {Y}에너지 변환기{E}와 함께 씁니다."),
                VPage("에너지 변환기 소개",
                    $"원료를 {Y}에너지 형태{E}로 변환하는 설비입니다. {Y}생체 분리기{E}의 산출물을 받아 가공합니다."),
                VPage("원재료 무한 복제",
                    $"{Y}생체 분리기{E}와 {Y}에너지 변환기{E}의 출력을 {Y}레일{E}로 서로 물려 순환시키면 원재료가 계속 불어납니다. 이 {Y}순환 라인{E}으로 재료를 무한히 확보하세요.")),

            // 귀환석 - 첫 획득(전송 20% 보상) 순간. 기지 안이라 안전.
            Cue("returnstone", true, VPage("귀환석",
                $"{Y}귀환석{E}을 사용하면 기지로 빠르게 복귀할 수 있습니다. {Y}H{E}로 사용하며, 레벨이 오를수록 {Y}쿨타임{E}이 짧아집니다.")),

            // (전송기 소개는 DiscoveryCue 가 아니라 엔드게임 퀘 quest_end_02 의 영상 objective 로 재생 - 전송기 도착 순간.
            //  코어 강화와 동일 패턴. 예전 interact:transmit 큐는 F로 UI 연 뒤 떠서 타이밍이 어긋나 제거.)

            // 첫 워프 지점 활성화 - 워프 시스템 소개. F로 활성화하는 순간 즉시(safe=true). WarpManager.ActivateRegionPoint 안에서 TryFire("warp").
            Cue("warp", true, VPage("워프 지점",
                $"발견한 {Y}워프 지점{E}은 {Y}F로 활성화{E}한 뒤 이후 이동에 사용할 수 있습니다. 기지와 지역을 빠르게 오가는 지름길입니다.")),

            // 우주선 부품 첫 획득 - 수리 시스템 소개. 부품은 필드(결계 밖)에 있으므로 safe:false
            //   = 팝업을 기지 복귀 때로 미룬다(전투 중 방해 방지 + 기지의 우주선 위치와 자연스럽게 연결). ShipPartPickup.TryFire("shiprepair").
            Cue("shiprepair", false, VPage("우주선 수리 소개",
                $"우주선 {Y}복구{E}에 사용되는 특수 에너지입니다. 충분히 모으면 기지의 {Y}복구 장치{E}에서 우주선 복구를 진행할 수 있습니다.")),
        };

        EditorUtility.SetDirty(set);
    }

    static DiscoveryCue Cue(string key, bool safe, params VideoTutorialPage[] pages)
        => new DiscoveryCue { cueKey = key, safe = safe, pages = pages };

    // ============================================================
    // 영상 클립 로드
    // ============================================================
    // ★[08-07] 삭제됨: EnsureVideoUiSprites / CopySpriteToResources
    //   영상 테두리 스프라이트를 Assets/Resources/TutorialVideo/vid_frame.png 로 복사하던 코드였다.
    //   "지웠다가 다시 복사"라서 실행할 때마다 GUID 가 새로 생겼는데,
    //   커밋 d110e9f56 이 그 스프라이트를 런타임 Resources.Load(경로 기반) 에서
    //   씬에 박힌 직접 참조(GUID 기반)로 바꾸면서 궁합이 깨졌다.
    //   -> 이 빌더를 돌릴 때마다 씬의 VideoBorder 스프라이트 참조가 끊기고,
    //      스프라이트 없는 Image 는 자기 영역을 흰색으로 꽉 채워 그려서 영상을 통째로 가렸다.
    //      ("영상이 흰 화면으로만 나옴" 의 진범. 영상/코덱/클립 연결은 전부 정상이었다.)
    //   Resources.Load 가 사라진 이상 복사본은 존재 이유가 없다. 씬이 원본
    //   Assets/15.UI/New/hl_slot_frame@2x.png 를 직접 참조하면 GUID 가 고정되어 다시는 안 끊긴다.
    //   Resources 폴더는 통째로 빌드에 포함되므로 복사본을 없애면 빌드 용량에도 이득이다.

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
