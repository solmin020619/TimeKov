using System.Linq;
using Unity.Behavior;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

// 거미S3(쫄몹: 자연/사막) 생성. 신규 FieldMonster 시스템 사용 = 기존 스크립트 무수정.
//
// 만드는 것:
//   1) 거미S3_Enemy.controller     : Speed / StrafeDir 블렌드 / Attack(느리게) / Hit / Die
//   2) FieldData_SpiderS3.asset    : FieldMonsterData (스탯 + 전조 + 패턴)
//   3) Enemy_SpiderS3.prefab       : BaseEnemy 기반. EnemyBrain·BehaviorGraphAgent 는 '비활성'
//                                    (EnemyHealth 가 brain.Data 로 퀘스트ID/HP바명 읽으므로 남겨둠)
//                                    실제 로직은 FieldMonsterAI 가 담당.
//
// 거미 성격: 발견 → 슬금슬금 옆으로 돌다 → 달려들어 턱에서 번쩍(전조) → 물기 → 물고 빠짐 → 재공격
//
// 멱등: 재실행 시 guid 유지하며 덮어씀. 손으로 튜닝한 값은 리셋됨.
// 메뉴: Tools > Enemy > Build 거미S3
public static class SpiderS3Builder
{
    // ── 원본(창동에셋) ──
    // 팀원 얼음정령 방식과 동일하게 '참조'해서 쓴다(복사 안 함).
    // 단, 거미 원본은 세팅이 비어 있어(머티리얼 미지정 + 걷기/Idle 클립 루프 꺼짐) 그냥 참조하면
    // 흰색·멈춤이 난다 -> NormalizeSource() 로 딱 한 번 정상 세팅을 채운 뒤(멱등) 전부 참조한다.
    const string SrcRoot     = "Assets/00.창동에셋/몬스터/거미S3";
    const string SrcAnim     = SrcRoot + "/Animations";
    const string ModelPrefab = SrcRoot + "/Prefabs/거미S3_Skin1.prefab";
    const string SkinMat     = SrcRoot + "/Materials/M_Spider_S3_Skin1.mat";
    const string SkinTex     = SrcRoot + "/Textures/Skin1/T_Spider_S3_Skin1_AlbedoTransparency.tga";

    // ── 생성물(새 파일)만 프리팹 폴더에. 원본 복사본이 아니라 신규 에셋(컨트롤러)뿐 ──
    const string WorkFolder = "Assets/05.Prefabs/Enemy/거미S3";
    const string CtrlPath   = WorkFolder + "/거미S3_Enemy.controller";

    // 걷기/Idle 처럼 반복돼야 하는 클립(원본 임포트에 루프를 1회 켜준다). 나머지는 단발이라 그대로.
    static readonly string[] LoopClips =
    {
        "Anim@Spider_S1_idle1",
        "Anim@Spider_S1_walk6",
        "Anim@Spider_S1_walk3",
        "Anim@Spider_S1_straif_L",
        "Anim@Spider_S1_straif_R",
    };

    const string BaseEnemy  = "Assets/05.Prefabs/###/BaseEnemy.prefab";
    const string SoFolder   = "Assets/06.ScriptableObjects/Enemy";
    const string SoPath     = SoFolder + "/FieldData_SpiderS3.asset";
    const string PrefabPath = "Assets/05.Prefabs/Enemy/Enemy_SpiderS3.prefab";

    // 기존 몬스터가 쓰는 공용 스폰 VFX / 거미 전용 사운드 (있는 것만 연결, 나머지는 슬롯만)
    const string SpawnVfx  = "Assets/18.외부에셋/Eric VFX Studio/몬스터스폰VFX/Prefabs/FX_MagicCircle_Icearrow01.prefab";
    const string SndDetect = "Assets/10. Sound/1. 사운드 모음/몬스터/거미/M_SpiderFind.mp3";
    const string SndAttack = "Assets/10. Sound/1. 사운드 모음/몬스터/거미/M_SpiderAttack.mp3";
    const string SndDie    = "Assets/10. Sound/1. 사운드 모음/몬스터/거미/M_SpiderDie.mp3";

    // 전조 VFX — 구매 시트의 VFX(전조) 팩. 공격 순간 턱에서 번쩍이는 차지 오브.
    const string TelegraphVfx = "Assets/00.창동에셋/VFX/VFX(전조)/ChargeProjectiles_Chargefx/Prefabs_Chargefx/Charge/Charge_01.prefab";

    // ── 스탯 ──
    const string EnemyNameKo = "거미";
    const string EnemyId     = "spider_s3";
    const string SourceId    = "MeleeBot_SpiderS3";
    const float  MaxHP        = 55f;
    const float  MoveSpeed    = 5f;
    const float  AttackDamage = 12f;
    const float  AttackRange  = 2.5f;
    const float  VisionRange  = 18f;
    const float  VisionAngle  = 300f;   // 거미 = 시야 넓게

    // ── 모션 클립 지정 ──
    const string RoarClip   = "Anim@Spider_S1_attack1";  // 최초 조우 포효(attack1 을 느리게)
    const string AttackClip = "Anim@Spider_S1_attack2";  // 실제 공격(attack2 를 0.75배속)

    // ── 전조/패턴 (여기만 고쳐서 성격 조절) ──
    const float AttackSpeedMul = 0.75f;  // 공격(attack2) 감속 -> 전조가 읽힘 (요청: 0.75배속)
    const float RoarSpeedMul   = 0.6f;   // 포효(attack1) 감속 -> 위압감 (느리게)
    const float HitDelayRatio  = 0.5f;   // 모션 대비 타격 시점(= 피할 수 있는 시간)
    const CombatStepKind OpeningStep    = CombatStepKind.None;          // 옆걸음 삭제 -> 발견 후 바로 접근
    const float          OpeningStepSec = 0f;
    const float          PostAttackPause = 0.5f;   // 공격 후 후퇴 전 경직(플레이어 반격 틈)
    const CombatStepKind AfterStep      = CombatStepKind.Retreat;       // 공격 후: 보면서 멀어짐(치고 빠지기)
    const float          AfterStepMin   = 0.8f;   // 공격 후 후퇴 시간 랜덤 최소
    const float          AfterStepMax   = 3.2f;   // 공격 후 후퇴 시간 랜덤 최대
    const float          RetreatDiag    = 45f;    // 후퇴 대각선 최대 각도(정후방~대각선 뒤). 순수 옆걸음만 뺐고 대각선은 유지
    const float          StepSpeedMul   = 0.5f;    // 후퇴(뒷걸음) 속도. 낮춰서 뒷걸음 애니와 매칭(요청)
    // 옆걸음: 이동 속도를 애니 기준속도(WalkAnimRefSpeed)에 맞추면 발이 안 미끄러진다.
    //   0.5 × moveSpeed(5) = 2.5 m/s = WalkAnimRefSpeed -> 애니 1배속과 정확히 일치(미끄러짐 0).
    //   더 느리게 하면 발이 미끄러져 어색해진다(느린이동↔자연스러운애니는 물리적으로 상충).
    const float          StrafeSpeedMul = 0.5f;
    const float          StrafeAnimMul  = 1f;      // 옆걸음 애니 배속(이동과 분리 가능)
    const float          LocoDirDamp    = 0.12f;   // (미사용) 방향 블렌드 damp
    const float          AttackCooldown = 0.2f;   // 실질 간격은 후퇴 시간(AfterStepMin~Max 랜덤)이 만든다

    // 걷기 애니가 1배속으로 자연스러운 이동 속도(m/s). 실제속도/이 값 = 재생 배속.
    // 클립이 제자리(in-place)라 자동 계산이 불가능 -> 눈으로 보고 맞추는 값.
    //   발이 헛돌면(애니가 너무 빠름) ↑,  발이 질질 끌리면(애니가 너무 느림) ↓
    const float WalkAnimRefSpeed = 2.5f;

    [MenuItem("Tools/Enemy/Build 거미S3")]
    public static void Build()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefab);
        if (model == null) { Debug.LogError($"[거미S3] 모델 프리팹 없음: {ModelPrefab}"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BaseEnemy) == null)
        { Debug.LogError($"[거미S3] BaseEnemy 없음: {BaseEnemy}"); return; }

        // 원본을 딱 한 번 정상 세팅(멱등). 이후 모든 참조가 흰색/멈춤 없이 동작.
        NormalizeSource();

        var ctrl = BuildAnimator();
        var so   = BuildData();
        BuildPrefab(model, ctrl, so);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[거미S3] 생성 완료 (FieldMonster 시스템 — 팀원 얼음정령과 동일한 참조 방식)\n" +
            $"  원본        : {SrcRoot}  (클립/머티리얼/모델 전부 '참조'. 최초 1회만 루프·스킨 세팅 보정)\n" +
            $"  생성물      : {WorkFolder}  (신규 컨트롤러 1개뿐, 복사본 없음)\n" +
            $"  Data        : {SoPath}  (HP {MaxHP} / 공격 {AttackDamage} / 사거리 {AttackRange})\n" +
            $"  Prefab      : {PrefabPath}\n" +
            $"  포효     : 최초 조우 시 attack1 을 {RoarSpeedMul:0.##}배속으로 (Detect 상태)\n" +
            $"  공격     : attack2 를 {AttackSpeedMul:0.##}배속 + 턱(Jaw)에서 전조 번쩍 → 모션 {HitDelayRatio:P0} 지점 타격\n" +
            $"  패턴     : 발견→포효→{OpeningStep}({OpeningStepSec}s)→접근→공격→{AfterStep}({AfterStepMin}~{AfterStepMax}s 랜덤)→재공격\n\n" +
            "## 할 것\n" +
            "1) 스폰존(EnemySpawnPoint)의 enemyPrefabs 에 Enemy_SpiderS3 배치 (자연/사막)\n" +
            $"2) 드롭 시트(DropTable)에 sourceId={SourceId} 행 추가\n" +
            "3) 전조음 등 사운드는 슬롯만 비어있음 — 클립 찾으면 SO에 꽂으면 바로 남\n" +
            "4) 패턴이 어색하면 SpiderS3Builder 상단 상수 고치고 재실행");
    }

    // ── 1) Animator ────────────────────────────────────────────────
    static AnimatorController BuildAnimator()
    {
        EnsureFolder(WorkFolder);

        // ★컨트롤러는 '재생성' 대신 '재사용'해서 GUID 를 고정한다.
        //   매번 DeleteAsset + Create 하면 GUID 가 바뀌어, 씬에 놓인/스폰된/언팩된 인스턴스가
        //   들고 있던 컨트롤러 참조가 끊긴다 -> 런타임에 컨트롤러=null ->
        //   "Animator is not playing an AnimatorController" 로 애니가 안 나온다.
        //   기존 자산을 비우고 다시 채우면 GUID 가 유지돼 재빌드해도 참조가 안 깨진다.
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
        if (ctrl == null)
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        else
            ClearController(ctrl);

        ctrl.AddParameter("Speed",     AnimatorControllerParameterType.Float);
        ctrl.AddParameter("StrafeDir", AnimatorControllerParameterType.Float);   // -1 좌 / +1 우
        ctrl.AddParameter("MoveDir",   AnimatorControllerParameterType.Float);   // +1 정면 / -1 뒤
        ctrl.AddParameter("SpeedMul",  AnimatorControllerParameterType.Float);   // 걷기 재생 배속(발 미끄러짐 방지)
        ctrl.AddParameter("Attack",    AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Hit",       AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Detect",    AnimatorControllerParameterType.Trigger);   // 최초 조우 포효
        var sm = ctrl.layers[0].stateMachine;

        // 클립은 전부 원본 FBX 내장 클립을 '참조'(복사 안 함). 루프는 NormalizeSource 가 원본에 1회 세팅.
        var idle = sm.AddState("Idle"); idle.motion = Clip("Anim@Spider_S1_idle1");

        // Locomotion = 2D 방향 블렌드. 뒤/옆으로 빠질 때 그 방향 전용 모션이 나와야 문워크가 안 생긴다.
        //   정면 walk6 / 뒤 walk3 / 좌 straif_L / 우 straif_R  (클립 방향은 재원님 확인)
        var loco = ctrl.CreateBlendTreeInController("Locomotion", out var bt, 0);
        bt.blendType = BlendTreeType.SimpleDirectional2D;
        bt.blendParameter  = "StrafeDir";   // X
        bt.blendParameterY = "MoveDir";     // Y
        bt.AddChild(Clip("Anim@Spider_S1_walk6"),    new Vector2( 0f,  1f));   // 정면
        bt.AddChild(Clip("Anim@Spider_S1_walk3"),    new Vector2( 0f, -1f));   // 뒤
        bt.AddChild(Clip("Anim@Spider_S1_straif_L"), new Vector2(-1f,  0f));   // 좌
        bt.AddChild(Clip("Anim@Spider_S1_straif_R"), new Vector2( 1f,  0f));   // 우

        // 걷기 재생 배속을 SpeedMul 로 제어 -> 실제 이동 속도에 발을 맞춘다(제자리 클립이라 수동).
        loco.speedParameterActive = true;
        loco.speedParameter = "SpeedMul";

        sm.defaultState = idle;

        var toLoco = idle.AddTransition(loco);
        toLoco.hasExitTime = false; toLoco.duration = 0.1f;
        toLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        var toIdle = loco.AddTransition(idle);
        toIdle.hasExitTime = false; toIdle.duration = 0.1f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // 공격(attack2) — speed<1 로 늦춰 전조가 읽히게
        var atk = sm.AddState("Attack");
        atk.motion = Clip(AttackClip);
        atk.speed = AttackSpeedMul;
        var toAtk = sm.AddAnyStateTransition(atk);
        toAtk.hasExitTime = false; toAtk.duration = 0.05f; toAtk.canTransitionToSelf = false;
        toAtk.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        var atkEx = atk.AddTransition(idle);
        atkEx.hasExitTime = true; atkEx.exitTime = 0.9f; atkEx.duration = 0.1f;

        // 최초 조우 포효(attack1 을 느리게). EnemyFeedback.PlayDetect 가 "Detect" 트리거를 검.
        var roar = sm.AddState("Detect");
        roar.motion = Clip(RoarClip);
        roar.speed = RoarSpeedMul;
        var toRoar = sm.AddAnyStateTransition(roar);
        toRoar.hasExitTime = false; toRoar.duration = 0.05f; toRoar.canTransitionToSelf = false;
        toRoar.AddCondition(AnimatorConditionMode.If, 0f, "Detect");
        var roarEx = roar.AddTransition(idle);
        roarEx.hasExitTime = true; roarEx.exitTime = 0.95f; roarEx.duration = 0.15f;

        // 피격 — 짧은 경직 (EnemyFeedback 이 Hit 트리거를 검)
        var hit = sm.AddState("Hit"); hit.motion = Clip("Anim@Spider_S1_get_hit1");
        var toHit = sm.AddAnyStateTransition(hit);
        toHit.hasExitTime = false; toHit.duration = 0.05f; toHit.canTransitionToSelf = false;
        toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
        var hitEx = hit.AddTransition(idle);
        hitEx.hasExitTime = true; hitEx.exitTime = 0.8f; hitEx.duration = 0.1f;

        // 사망 = 터미널. EnemyFeedback.PlayDeath 가 "Die" 상태를 이름으로 CrossFade.
        sm.AddState("Die").motion = Clip("Anim@Spider_S1_death1");

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    // 컨트롤러 자산(=GUID)은 유지한 채 내용만 비운다. 재빌드 시 참조가 안 깨지게 하는 핵심.
    static void ClearController(AnimatorController ctrl)
    {
        foreach (var p in ctrl.parameters.ToList())
            ctrl.RemoveParameter(p);

        var sm = ctrl.layers[0].stateMachine;
        foreach (var t in sm.anyStateTransitions.ToList())
            sm.RemoveAnyStateTransition(t);
        foreach (var st in sm.states.ToList())
            sm.RemoveState(st.state);
        foreach (var ss in sm.stateMachines.ToList())
            sm.RemoveStateMachine(ss.stateMachine);
        sm.defaultState = null;

        // RemoveState 로 참조가 끊긴 BlendTree 서브에셋(orphan)을 파일에서 정리(누적 방지)
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(ctrl)))
            if (obj is BlendTree bt)
                Object.DestroyImmediate(bt, true);
    }

    // ── 2) FieldMonsterData ────────────────────────────────────────
    static FieldMonsterData BuildData()
    {
        var so = AssetDatabase.LoadAssetAtPath<FieldMonsterData>(SoPath);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<FieldMonsterData>();
            AssetDatabase.CreateAsset(so, SoPath);
        }

        so.enemyName = EnemyNameKo;
        so.enemyId   = EnemyId;          // ★ EnemyHealth 가 이걸로 퀘스트 킬 집계
        so.maxHP     = MaxHP;
        so.moveSpeed = MoveSpeed;
        so.acceleration = 12f;
        so.angularSpeed = 480f;
        so.visionRange = VisionRange;
        so.visionAngle = VisionAngle;
        so.attackDamage = AttackDamage;
        so.attackRange  = AttackRange;
        so.attackApproachRatio = 0.85f;
        so.attackCooldown = AttackCooldown;
        so.targetLostMemory = 1.5f;
        // 최초 조우 포효 시간 = 포효 클립 길이 / 감속배율. 이 시간 동안 멈춰서 포효를 보여준다.
        // (EnemyFeedback.PlayDetect 가 "Detect" 트리거를 걸어 포효 상태를 재생, DetectPause 가 이만큼 대기)
        so.detectStunDuration = ClipLength(RoarClip, 1.2f) / Mathf.Max(0.01f, RoarSpeedMul);

        // 타이밍은 실제 공격 클립(attack2)에서. Attack 상태를 늦췄으니 체감 길이도 늘어남 -> 보정.
        float atkLen = ClipLength(AttackClip, 1.2f) / Mathf.Max(0.01f, AttackSpeedMul);
        so.animLength = atkLen;
        so.hitDelay   = atkLen * HitDelayRatio;
        so.deathAnimDuration = ClipLength("Anim@Spider_S1_death1", 1.5f);

        // 전조
        so.telegraphVFX = AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphVfx);
        so.telegraphSound = null;                       // 사운드는 나중에
        so.telegraphLifeTime = so.hitDelay + 0.15f;
        so.attackSpeedMul = AttackSpeedMul;

        // 패턴 (거미 고유)
        so.openingStep = OpeningStep;
        so.openingStepDuration = OpeningStepSec;
        so.postAttackPause = PostAttackPause;
        so.afterAttackStep = AfterStep;
        so.afterAttackStepDurationRange = new Vector2(AfterStepMin, AfterStepMax);
        so.retreatDiagonalMaxAngle = RetreatDiag;
        so.stepSpeedMul = StepSpeedMul;
        so.strafeSpeedMul = StrafeSpeedMul;
        so.strafeAnimSpeedMul = StrafeAnimMul;
        so.walkAnimRefSpeed = WalkAnimRefSpeed;
        so.walkAnimSpeedClamp = new Vector2(0.4f, 2.5f);
        so.locoDirDamp = LocoDirDamp;
        so.wander = true;
        so.wanderRadius = 6f;

        // 있는 사운드만 연결 (나머지는 슬롯만)
        so.spawnVFX    = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnVfx);
        so.detectSound = AssetDatabase.LoadAssetAtPath<AudioClip>(SndDetect);
        so.attackSound = AssetDatabase.LoadAssetAtPath<AudioClip>(SndAttack);
        so.deathSound  = AssetDatabase.LoadAssetAtPath<AudioClip>(SndDie);

        EditorUtility.SetDirty(so);
        return so;
    }

    // ── 3) Prefab ──────────────────────────────────────────────────
    static void BuildPrefab(GameObject model, AnimatorController ctrl, FieldMonsterData so)
    {
        // 매번 BaseEnemy 원본에서 새로 떠서 조립 -> 재실행해도 중복 안 붙음
        var root = PrefabUtility.LoadPrefabContents(BaseEnemy);
        root.name = "Enemy_SpiderS3";

        var vis = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        vis.transform.localPosition = Vector3.zero;
        vis.transform.localRotation = Quaternion.identity;

        // ★모델을 완전히 Unpack — 중첩 프리팹 상태로 두면 Animator 컨트롤러가 '오버라이드'로 저장돼
        //   런타임에 간헐적으로 안 먹어(=null) "Animator is not playing an AnimatorController" 경고가 뜬다.
        //   Unpack 하면 컨트롤러가 컴포넌트 직접 값이 되어 확실히 적용된다(팀원 얼음정령과 동일 방식).
        //   ※머티리얼/클립은 여전히 GUID 참조라 복사 아님.
        PrefabUtility.UnpackPrefabInstance(vis, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        var anim = vis.GetComponentInChildren<Animator>(true);
        if (anim == null) anim = vis.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;

        // 스킨은 NormalizeSource 가 원본 프리팹에 이미 물려놨으므로 여기선 손대지 않는다(참조).

        // 기존 브레인/그래프는 '끄고' 데이터만 남긴다.
        // EnemyHealth 가 GetComponent<EnemyBrain>().Data 에서 enemyId/enemyName/deathAnimDuration 을
        // 읽기 때문에 컴포넌트 자체는 있어야 한다(비활성이어도 GetComponent 로 잡힘).
        var brain = root.GetComponent<EnemyBrain>();
        if (brain != null) { SetPrivate(brain, "data", so); brain.enabled = false; }
        var bt = root.GetComponent<BehaviorGraphAgent>();
        if (bt != null) bt.enabled = false;

        // 실제 로직 = 신규 AI
        var ai = root.GetComponent<FieldMonsterAI>();
        if (ai == null) ai = root.AddComponent<FieldMonsterAI>();
        SetPrivate(ai, "data", so);
        SetPrivate(ai, "visionSensor", root.GetComponentInChildren<VisionSensor>(true));
        SetPrivate(ai, "animator", anim);
        SetPrivate(ai, "animatorController", ctrl);   // 런타임 컨트롤러 복구용 폴백
        SetPrivate(ai, "audioSource", root.GetComponent<AudioSource>());

        // 전조는 눈/턱에서 번쩍여야 티가 난다 (거미는 head/eye 본이 없고 Jaw 가 있음)
        var eye = FindTelegraphAnchor(vis);
        if (eye != null) SetPrivate(ai, "telegraphAnchor", eye);
        else Debug.LogWarning("[거미S3] 턱/머리 본을 못 찾음 -> FieldMonsterAI.telegraphAnchor 직접 지정 필요");

        SetPrivateString(root.GetComponent<EnemyDropOnDeath>(), "sourceId", SourceId);

        FitBodyToModel(root, vis);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ── 원본 정상화 (팀원 얼음정령처럼 '참조'만으로 되게, 원본을 딱 한 번 세팅) ──────
    // 전부 멱등: 이미 올바르면 아무것도 쓰지 않는다 -> 재실행해도 원본에 새 diff 안 생김.
    // 사용자 승인 하에 진행. 거미 원본이 애초에 세팅이 비어 있던(깨진) 것을 채우는 1회성 보정.
    static void NormalizeSource()
    {
        NormalizeMaterialTexture();
        NormalizeModelPrefabSkin();
        NormalizeLocoClips();
    }

    /// <summary>원본 .mat 의 비어 있던 알베도(_MainTex/_BaseMap)를 1회 채운다.</summary>
    static void NormalizeMaterialTexture()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(SkinMat);
        var tex = AssetDatabase.LoadAssetAtPath<Texture>(SkinTex);
        if (mat == null) { Debug.LogWarning($"[거미S3] 스킨 머티리얼 없음: {SkinMat}"); return; }
        if (tex == null) { Debug.LogWarning($"[거미S3] 알베도 텍스처 없음: {SkinTex}"); return; }

        bool changed = false;
        if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") == null) { mat.SetTexture("_MainTex", tex); changed = true; }
        if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") == null) { mat.SetTexture("_BaseMap", tex); changed = true; }
        if (changed)
        {
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log("[거미S3] 원본 머티리얼 알베도 보정(최초 1회)");
        }
    }

    /// <summary>원본 모델 프리팹의 렌더러가 스킨 머티리얼을 물게 1회 세팅(비어 있어 흰색이던 것).</summary>
    static void NormalizeModelPrefabSkin()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(SkinMat);
        if (mat == null) return;

        var root = PrefabUtility.LoadPrefabContents(ModelPrefab);
        if (root == null) { Debug.LogWarning($"[거미S3] 모델 프리팹 로드 실패: {ModelPrefab}"); return; }

        bool changed = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool local = false;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != mat) { mats[i] = mat; local = true; }
            if (local) { r.sharedMaterials = mats; changed = true; }
        }
        if (changed)
        {
            PrefabUtility.SaveAsPrefabAsset(root, ModelPrefab);
            Debug.Log("[거미S3] 원본 모델 프리팹 스킨 연결(최초 1회)");
        }
        PrefabUtility.UnloadPrefabContents(root);
    }

    /// <summary>
    /// 로코모션 클립(걷기/Idle/옆걸음)을 원본 FBX 임포트에서 정상화.
    ///   - 루프 ON  — 참조해도 안 멈추게
    ///   - Root Transform Bake Into Pose 는 OFF 로 유지
    ///     (★Generic 리그에서 이걸 켜면 리타게팅이 틀어져 T-포즈로 애니가 아예 안 나온다.
    ///      들썩임/뜸은 애니 임포트가 아니라 FieldMonsterAI 의 방향 블렌드 damp(locoDirDamp)로 잡는다.)
    /// 멱등: 이미 이 상태면 아무것도 안 함.
    /// </summary>
    static void NormalizeLocoClips()
    {
        foreach (var name in LoopClips)
        {
            string path = $"{SrcAnim}/{name}.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) { Debug.LogWarning($"[거미S3] FBX 임포터 없음: {path}"); continue; }

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) continue;

            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                // API 속성명 ≠ 메타 필드명:
                //   lockRootHeightY=loopBlendPositionY / lockRootPositionXZ=loopBlendPositionXZ / lockRootRotation=loopBlendOrientation
                var c = clips[i];
                if (!c.loopTime)          { c.loopTime = true;           changed = true; }  // 루프 ON
                if (c.lockRootHeightY)    { c.lockRootHeightY = false;   changed = true; }  // Bake Into Pose OFF (T-포즈 방지)
                if (c.lockRootPositionXZ) { c.lockRootPositionXZ = false; changed = true; }
                if (c.lockRootRotation)   { c.lockRootRotation = false;  changed = true; }
            }

            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                Debug.Log($"[거미S3] 원본 로코 클립 정상화(루프 ON / Bake OFF): {name}");
            }
        }
    }

    // 콜라이더/네비를 실제 모델 크기에 맞춤. 안 맞으면 공중에 뜨거나 사거리가 엉킨다.
    static void FitBodyToModel(GameObject root, GameObject vis)
    {
        var rs = vis.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return;

        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

        float h = Mathf.Max(0.2f, b.size.y);
        float r = Mathf.Max(0.15f, Mathf.Max(b.size.x, b.size.z) * 0.4f);

        var cap = root.GetComponent<CapsuleCollider>();
        if (cap != null) { cap.height = h; cap.radius = r; cap.center = new Vector3(0f, h * 0.5f, 0f); }

        var nav = root.GetComponent<NavMeshAgent>();
        if (nav != null) { nav.height = h; nav.radius = r; }
    }

    static Transform FindTelegraphAnchor(GameObject vis)
    {
        string[] prefer = { "eye", "jaw", "head", "neck" };
        var all = vis.GetComponentsInChildren<Transform>(true);
        foreach (var key in prefer)
        {
            var hit = all.FirstOrDefault(t => t.name.ToLower().Contains(key));
            if (hit != null) return hit;
        }
        return null;
    }

    // ── helpers ────────────────────────────────────────────────────
    // 원본 FBX 안(서브에셋)의 클립. ★읽기만 한다.
    static AnimationClip SrcClip(string fbxName)
    {
        string p = $"{SrcAnim}/{fbxName}.fbx";
        var c = AssetDatabase.LoadAllAssetRepresentationsAtPath(p)
                             .OfType<AnimationClip>()
                             .FirstOrDefault(x => !x.name.StartsWith("__preview"));
        if (c == null) Debug.LogWarning($"[거미S3] 원본 클립 없음: {p}");
        return c;
    }

    // 컨트롤러에 꽂을 클립 = 원본 FBX 내장 클립을 그대로 '참조'(복사 안 함).
    // 루프는 NormalizeSource().NormalizeClipLoops 가 원본 임포트에 미리 켜둔다.
    static AnimationClip Clip(string fbxName) => SrcClip(fbxName);

    static float ClipLength(string fbxName, float fallback)
    {
        var c = SrcClip(fbxName);
        return (c != null && c.length > 0.01f) ? c.length : fallback;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
        string leaf = System.IO.Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static void SetPrivate(Object target, string field, Object value)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedProperties(); }
    }

    static void SetPrivateString(Object target, string field, string value)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p != null) { p.stringValue = value; so.ApplyModifiedProperties(); }
    }
}
