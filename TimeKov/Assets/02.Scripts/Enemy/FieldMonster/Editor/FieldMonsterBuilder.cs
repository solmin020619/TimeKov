using System.Linq;
using Unity.Behavior;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 신규 필드 몬스터 공용 빌더. 몬스터별 빌더는 <see cref="FieldMonsterBuildConfig"/> 만 채워
/// <c>FieldMonsterBuilder.Build(config)</c> 를 호출하면 된다.
///
/// 방식(팀원 얼음정령과 동일): 원본 에셋을 '참조'만 하고 복사하지 않는다.
/// 단, 원본이 세팅이 비어 있으면(머티리얼 미지정/클립 루프 꺼짐) NormalizeSource() 로 딱 한 번 보정(멱등).
///
/// 생성물(신규 에셋)만 만든다: 컨트롤러 1개 + Data SO + Enemy 프리팹.
/// </summary>
public static class FieldMonsterBuilder
{
    public static void Build(FieldMonsterBuildConfig c)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(c.ModelPrefab);
        if (model == null) { Debug.LogError($"[{c.enemyName}] 모델 프리팹 없음: {c.ModelPrefab}"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(c.baseEnemy) == null)
        { Debug.LogError($"[{c.enemyName}] BaseEnemy 없음: {c.baseEnemy}"); return; }

        NormalizeSource(c);                 // 원본 1회 정상화(멱등)
        var ctrl = BuildAnimator(c);
        var so   = BuildData(c);
        BuildPrefab(c, model, ctrl, so);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[{c.enemyName}] 생성 완료 (FieldMonster 공용 빌더 — 참조 방식)\n" +
            $"  원본   : {c.srcRoot}  (클립/머티리얼/모델 참조. 최초 1회만 정상화)\n" +
            $"  생성물 : {c.CtrlPath} / {c.soPath} / {c.prefabPath}\n" +
            $"  포효   : {c.clipRoar} ×{c.roarSpeedMul:0.##}  /  공격 : {c.clipAttack} ×{c.attackSpeedMul:0.##}\n" +
            $"  패턴   : 발견→포효→접근→전조공격(시선고정)→회복 {c.postAttackPause}s→후퇴 {c.afterStepMin}~{c.afterStepMax}s(랜덤)→재공격");
    }

    // ── 원본 정상화 (멱등) ─────────────────────────────────────────
    static void NormalizeSource(FieldMonsterBuildConfig c)
    {
        // 1) 머티리얼 알베도 채우기
        var mat = AssetDatabase.LoadAssetAtPath<Material>(c.SkinMat);
        var tex = AssetDatabase.LoadAssetAtPath<Texture>(c.skinTexPath);
        if (mat != null && tex != null)
        {
            bool ch = false;
            if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") == null) { mat.SetTexture("_MainTex", tex); ch = true; }
            if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") == null) { mat.SetTexture("_BaseMap", tex); ch = true; }
            if (ch) { EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets(); Debug.Log($"[{c.enemyName}] 원본 머티리얼 알베도 보정(1회)"); }
        }
        else Debug.LogWarning($"[{c.enemyName}] 머티리얼/텍스처 없음: {c.SkinMat} / {c.skinTexPath}");

        // 2) 모델 프리팹 렌더러에 스킨 연결
        if (mat != null)
        {
            var root = PrefabUtility.LoadPrefabContents(c.ModelPrefab);
            if (root != null)
            {
                bool ch = false;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool local = false;
                    for (int i = 0; i < mats.Length; i++) if (mats[i] != mat) { mats[i] = mat; local = true; }
                    if (local) { r.sharedMaterials = mats; ch = true; }
                }
                if (ch) { PrefabUtility.SaveAsPrefabAsset(root, c.ModelPrefab); Debug.Log($"[{c.enemyName}] 원본 모델 프리팹 스킨 연결(1회)"); }
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 3) 로코 클립 루프 ON (Bake Into Pose 는 OFF 유지 — Generic 리그에서 켜면 T-포즈)
        if (!string.IsNullOrEmpty(c.singleAnimFbx)) { NormalizeLoopsSingleFbx(c); return; }
        foreach (var name in c.LoopClips())
        {
            string path = $"{c.SrcAnim}/{name}.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) { Debug.LogWarning($"[{c.enemyName}] FBX 임포터 없음: {path}"); continue; }

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) continue;

            bool ch = false;
            for (int i = 0; i < clips.Length; i++)
            {
                var a = clips[i];
                if (!a.loopTime)          { a.loopTime = true;           ch = true; }
                if (a.lockRootHeightY)    { a.lockRootHeightY = false;   ch = true; }
                if (a.lockRootPositionXZ) { a.lockRootPositionXZ = false; ch = true; }
                if (a.lockRootRotation)   { a.lockRootRotation = false;  ch = true; }
            }
            if (ch) { importer.clipAnimations = clips; importer.SaveAndReimport(); Debug.Log($"[{c.enemyName}] 원본 로코 클립 루프 ON: {name}"); }
        }
    }

    // 단일 FBX 정상화: 로코 서브클립(idle/walk/back/좌/우)만 루프 ON +
    //   모든 클립의 데모 AnimationEvent 제거(PlayAudio/LeftFoot/RightFoot/Clap 등 — 수신 컴포넌트 없어 콘솔 스팸).
    static void NormalizeLoopsSingleFbx(FieldMonsterBuildConfig c)
    {
        var importer = AssetImporter.GetAtPath(c.SingleFbxPath) as ModelImporter;
        if (importer == null) { Debug.LogWarning($"[{c.enemyName}] FBX 임포터 없음: {c.SingleFbxPath}"); return; }

        var loopSet = new System.Collections.Generic.HashSet<string>(c.LoopClips());
        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;

        bool ch = false;
        for (int i = 0; i < clips.Length; i++)
        {
            var a = clips[i];
            if (loopSet.Contains(a.name))   // 로코 클립만 루프 ON
            {
                if (!a.loopTime)          { a.loopTime = true;           ch = true; }
                if (a.lockRootHeightY)    { a.lockRootHeightY = false;   ch = true; }
                if (a.lockRootPositionXZ) { a.lockRootPositionXZ = false; ch = true; }
                if (a.lockRootRotation)   { a.lockRootRotation = false;  ch = true; }
            }
            // 데모 이벤트 제거(모든 클립) — 우리 AI는 이 이벤트를 안 쓴다. 사운드는 별도 슬롯으로.
            if (a.events != null && a.events.Length > 0) { a.events = new AnimationEvent[0]; ch = true; }
        }
        if (ch) { importer.clipAnimations = clips; importer.SaveAndReimport(); Debug.Log($"[{c.enemyName}] 원본 정상화(단일 FBX): 로코 루프 ON + 데모 이벤트 제거"); }
    }

    // ── Animator (컨트롤러 재사용 = GUID 고정) ──────────────────────
    static AnimatorController BuildAnimator(FieldMonsterBuildConfig c)
    {
        EnsureFolder(c.ctrlFolder);

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(c.CtrlPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(c.CtrlPath);
        else ClearController(ctrl);

        ctrl.AddParameter("Speed",     AnimatorControllerParameterType.Float);
        ctrl.AddParameter("StrafeDir", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("MoveDir",   AnimatorControllerParameterType.Float);
        ctrl.AddParameter("SpeedMul",  AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Attack",    AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Hit",       AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Stagger",   AnimatorControllerParameterType.Bool);   // 피격 경직 게이트(AI가 확률로 켬)
        ctrl.AddParameter("Detect",    AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;

        var idle = sm.AddState("Idle"); idle.motion = Clip(c, c.clipIdle);

        // Locomotion = 2D 방향 블렌드(정면/뒤/좌/우). Retreat 가 매 프레임 좌표를 갱신해 씀.
        var loco = ctrl.CreateBlendTreeInController("Locomotion", out var bt, 0);
        bt.blendType = BlendTreeType.SimpleDirectional2D;
        bt.blendParameter = "StrafeDir"; bt.blendParameterY = "MoveDir";
        bt.AddChild(Clip(c, c.clipFront), new Vector2( 0f,  1f));
        bt.AddChild(Clip(c, c.clipBack),  new Vector2( 0f, -1f));
        bt.AddChild(Clip(c, c.clipLeft),  new Vector2(-1f,  0f));
        bt.AddChild(Clip(c, c.clipRight), new Vector2( 1f,  0f));
        loco.speedParameterActive = true; loco.speedParameter = "SpeedMul";

        sm.defaultState = idle;

        // 휴면 포즈(선택) — 지정 시 '기본 상태'를 이걸로. 발견(Detect) 전까지 제자리 유지(이동 X).
        //   Detect 트리거는 AnyState→Detect 가 처리 → 깨어나는 모션(clipRoar, 예: RubbleToIdle) 재생 후 standing Idle 로.
        //   깨어난 뒤엔 idle↔loco 만 쓰고 휴면으로 되돌아오지 않는다(리스폰/리셋 시에만 다시 휴면으로 시작).
        if (!string.IsNullOrEmpty(c.dormantClip))
        {
            var dormant = sm.AddState("Dormant"); dormant.motion = Clip(c, c.dormantClip);
            sm.defaultState = dormant;

            // 붕괴(휴면 복귀, 선택) — Sleep 트리거 → Crumble(예: IdleToRubble) 재생 → 다시 Dormant.
            //   각성 상태로 오래 타깃을 못 보면 AI 가 Sleep 을 걸어 바위 더미로 되돌아간다.
            if (!string.IsNullOrEmpty(c.crumbleClip))
            {
                ctrl.AddParameter("Sleep", AnimatorControllerParameterType.Trigger);
                var crumble = sm.AddState("Crumble"); crumble.motion = Clip(c, c.crumbleClip); crumble.speed = c.roarSpeedMul;
                var toCrumble = sm.AddAnyStateTransition(crumble); toCrumble.hasExitTime = false; toCrumble.duration = 0.1f; toCrumble.canTransitionToSelf = false;
                toCrumble.AddCondition(AnimatorConditionMode.If, 0f, "Sleep");
                var crumbleEx = crumble.AddTransition(dormant); crumbleEx.hasExitTime = true; crumbleEx.exitTime = 0.95f; crumbleEx.duration = 0.15f;
            }
        }

        var toLoco = idle.AddTransition(loco); toLoco.hasExitTime = false; toLoco.duration = 0.1f;
        toLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        var toIdle = loco.AddTransition(idle); toIdle.hasExitTime = false; toIdle.duration = 0.1f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        var atk = sm.AddState("Attack"); atk.motion = Clip(c, c.clipAttack); atk.speed = c.attackSpeedMul;
        var toAtk = sm.AddAnyStateTransition(atk); toAtk.hasExitTime = false; toAtk.duration = 0.05f; toAtk.canTransitionToSelf = false;
        toAtk.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        var atkEx = atk.AddTransition(idle); atkEx.hasExitTime = true; atkEx.exitTime = 0.9f; atkEx.duration = 0.1f;

        // 공격 2종(선택) — AttackAlt bool 로 A/B(왼손/오른손) 라우팅. AI 가 매 공격 랜덤으로 세팅.
        if (!string.IsNullOrEmpty(c.clipAttackAlt))
        {
            ctrl.AddParameter("AttackAlt", AnimatorControllerParameterType.Bool);
            toAtk.AddCondition(AnimatorConditionMode.IfNot, 0f, "AttackAlt");   // Attack & !AttackAlt → A
            var atkB = sm.AddState("AttackB"); atkB.motion = Clip(c, c.clipAttackAlt); atkB.speed = c.attackSpeedMul;
            var toAtkB = sm.AddAnyStateTransition(atkB); toAtkB.hasExitTime = false; toAtkB.duration = 0.05f; toAtkB.canTransitionToSelf = false;
            toAtkB.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            toAtkB.AddCondition(AnimatorConditionMode.If, 0f, "AttackAlt");     // Attack & AttackAlt → B
            var atkBEx = atkB.AddTransition(idle); atkBEx.hasExitTime = true; atkBEx.exitTime = 0.9f; atkBEx.duration = 0.1f;
        }

        var roar = sm.AddState("Detect"); roar.motion = Clip(c, c.clipRoar); roar.speed = c.roarSpeedMul;
        var toRoar = sm.AddAnyStateTransition(roar); toRoar.hasExitTime = false; toRoar.duration = 0.05f; toRoar.canTransitionToSelf = false;
        toRoar.AddCondition(AnimatorConditionMode.If, 0f, "Detect");
        if (!string.IsNullOrEmpty(c.clipRoar2))
        {
            // 2단계 포효(예: IdleToRubble→RubbleToIdle = 무너졌다 재조립). Detect→Detect2→Idle 로 이어 붙여
            //   시작/끝 포즈(서 있음)가 매끄럽게 연결되게 한다. detectStunDuration 은 두 클립 합(아래 BuildData).
            var roar2 = sm.AddState("Detect2"); roar2.motion = Clip(c, c.clipRoar2); roar2.speed = c.roarSpeedMul;
            var toRoar2 = roar.AddTransition(roar2); toRoar2.hasExitTime = true; toRoar2.exitTime = 0.9f; toRoar2.duration = 0.1f;
            var roar2Ex = roar2.AddTransition(idle); roar2Ex.hasExitTime = true; roar2Ex.exitTime = 0.9f; roar2Ex.duration = 0.15f;
        }
        else
        {
            var roarEx = roar.AddTransition(idle); roarEx.hasExitTime = true; roarEx.exitTime = 0.95f; roarEx.duration = 0.15f;
        }

        var hit = sm.AddState("Hit"); hit.motion = Clip(c, c.clipHit);
        var toHit = sm.AddAnyStateTransition(hit); toHit.hasExitTime = false; toHit.duration = 0.05f; toHit.canTransitionToSelf = false;
        toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
        toHit.AddCondition(AnimatorConditionMode.If, 0f, "Stagger");   // ★Hit && Stagger → 확률로만 경직(AI가 Stagger 세팅)
        var hitEx = hit.AddTransition(idle); hitEx.hasExitTime = true; hitEx.exitTime = 0.8f; hitEx.duration = 0.1f;

        sm.AddState("Die").motion = Clip(c, c.clipDeath);

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static void ClearController(AnimatorController ctrl)
    {
        foreach (var p in ctrl.parameters.ToList()) ctrl.RemoveParameter(p);
        var sm = ctrl.layers[0].stateMachine;
        foreach (var t in sm.anyStateTransitions.ToList()) sm.RemoveAnyStateTransition(t);
        foreach (var s in sm.states.ToList()) sm.RemoveState(s.state);
        foreach (var ss in sm.stateMachines.ToList()) sm.RemoveStateMachine(ss.stateMachine);
        sm.defaultState = null;
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(ctrl)))
            if (obj is BlendTree bt) Object.DestroyImmediate(bt, true);
    }

    // ── Data SO ────────────────────────────────────────────────────
    static FieldMonsterData BuildData(FieldMonsterBuildConfig c)
    {
        var so = AssetDatabase.LoadAssetAtPath<FieldMonsterData>(c.soPath);
        if (so == null)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(c.soPath).Replace("\\", "/"));
            so = ScriptableObject.CreateInstance<FieldMonsterData>();
            AssetDatabase.CreateAsset(so, c.soPath);
        }

        so.enemyName = c.enemyName; so.enemyId = c.enemyId;
        so.maxHP = c.maxHP; so.moveSpeed = c.moveSpeed; so.acceleration = c.acceleration; so.angularSpeed = c.angularSpeed;
        so.visionRange = c.visionRange; so.visionAngle = c.visionAngle;
        so.attackDamage = c.attackDamage; so.attackRange = c.attackRange; so.attackApproachRatio = c.attackApproachRatio;
        so.attackCooldown = c.attackCooldown; so.targetLostMemory = 1.5f;

        float roarLen = ClipLength(c, c.clipRoar, 1.2f);
        if (!string.IsNullOrEmpty(c.clipRoar2)) roarLen += ClipLength(c, c.clipRoar2, 1.0f);   // 2단계면 합산
        so.detectStunDuration = roarLen / Mathf.Max(0.01f, c.roarSpeedMul);
        float atkLen = ClipLength(c, c.clipAttack, 1.2f) / Mathf.Max(0.01f, c.attackSpeedMul);
        so.animLength = atkLen; so.hitDelay = atkLen * c.hitDelayRatio;
        so.deathAnimDuration = ClipLength(c, c.clipDeath, 1.5f);

        so.telegraphVFX = AssetDatabase.LoadAssetAtPath<GameObject>(c.telegraphVfx);
        so.telegraphSound = null; so.telegraphLifeTime = so.hitDelay + 0.15f; so.attackSpeedMul = c.attackSpeedMul;
        so.staggerChance = c.staggerChance;
        so.startDormant = !string.IsNullOrEmpty(c.dormantClip);
        so.sleepAfterIdle = c.sleepAfterIdle;
        so.sleepAnimDuration = string.IsNullOrEmpty(c.crumbleClip)
            ? 0f : ClipLength(c, c.crumbleClip, 1.2f) / Mathf.Max(0.01f, c.roarSpeedMul);
        so.meleeImpactVFX = LoadIf<GameObject>(c.meleeImpactVfx);
        so.meleeImpactOffset = c.meleeImpactOffset;
        so.meleeImpactScale = c.meleeImpactScale;
        so.meleeImpactLifeTime = c.meleeImpactLifeTime;
        so.telegraphLocalOffset = c.telegraphOffset;
        so.telegraphScale = c.telegraphScale;
        so.telegraphGrow = c.telegraphGrow; so.telegraphGrowFrom = c.telegraphGrowFrom;
        so.telegraphSyncToHit = c.telegraphSyncToHit;
        so.telegraphStripObjects = c.telegraphStrip;

        // 원거리(발사체)
        so.ranged = c.ranged;
        so.projectileVFX = LoadIf<GameObject>(c.projectileVfx);
        so.muzzleVFX     = LoadIf<GameObject>(c.muzzleVfx);
        so.impactVFX     = LoadIf<GameObject>(c.impactVfx);
        so.projectileSpeed = c.projectileSpeed; so.projectileHitRadius = c.projectileHitRadius;
        so.projectileScale = c.projectileScale;
        so.projectileCount = c.projectileCount; so.projectileInterval = c.projectileInterval;
        so.projectileLifeTime = c.projectileLifeTime; so.projectileHomingDeg = c.projectileHomingDeg;
        so.projectileHomingDuration = c.projectileHomingDuration;
        so.projectileSpawnHeight = c.projectileSpawnHeight; so.projectileAimHeight = c.projectileAimHeight;
        so.projectileBlockMask = MaskFromLayers(c.projectileBlockLayers);

        so.openingStep = c.openingStep; so.openingStepDuration = c.openingStepSec;
        so.postAttackPause = c.postAttackPause;
        so.afterAttackStep = c.afterStep;
        so.afterAttackStepDurationRange = new Vector2(c.afterStepMin, c.afterStepMax);
        so.retreatDiagonalMaxAngle = c.retreatDiag;
        so.stepSpeedMul = c.stepSpeedMul; so.strafeSpeedMul = c.strafeSpeedMul; so.strafeAnimSpeedMul = c.strafeAnimMul;
        so.walkAnimRefSpeed = c.walkAnimRefSpeed; so.walkAnimSpeedClamp = new Vector2(0.4f, 2.5f); so.locoDirDamp = c.locoDirDamp;
        so.wander = c.wander; so.wanderRadius = c.wanderRadius;

        so.spawnVFX    = LoadIf<GameObject>(c.spawnVfx);
        so.detectSound = LoadIf<AudioClip>(c.sndDetect);
        so.attackSound = LoadIf<AudioClip>(c.sndAttack);
        so.deathSound  = LoadIf<AudioClip>(c.sndDie);

        EditorUtility.SetDirty(so);
        return so;
    }

    // ── Prefab ─────────────────────────────────────────────────────
    static void BuildPrefab(FieldMonsterBuildConfig c, GameObject model, AnimatorController ctrl, FieldMonsterData so)
    {
        var root = PrefabUtility.LoadPrefabContents(c.baseEnemy);
        root.name = System.IO.Path.GetFileNameWithoutExtension(c.prefabPath);

        var vis = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        vis.transform.localPosition = Vector3.zero;
        vis.transform.localRotation = Quaternion.identity;
        // 완전 Unpack -> 컨트롤러가 직접 값이 되어 런타임에 확실히 적용(오버라이드 불안정 회피).
        PrefabUtility.UnpackPrefabInstance(vis, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        if (c.scale != 1f) vis.transform.localScale = Vector3.one * c.scale;

        var anim = vis.GetComponentInChildren<Animator>(true);
        if (anim == null) anim = vis.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;

        var brain = root.GetComponent<EnemyBrain>();
        if (brain != null) { SetPrivate(brain, "data", so); brain.enabled = false; }
        var bt = root.GetComponent<BehaviorGraphAgent>();
        if (bt != null) bt.enabled = false;

        var ai = root.GetComponent<FieldMonsterAI>();
        if (ai == null) ai = root.AddComponent<FieldMonsterAI>();
        SetPrivate(ai, "data", so);
        SetPrivate(ai, "visionSensor", root.GetComponentInChildren<VisionSensor>(true));
        SetPrivate(ai, "animator", anim);
        SetPrivate(ai, "animatorController", ctrl);   // 런타임 컨트롤러 복구 폴백
        SetPrivate(ai, "audioSource", root.GetComponent<AudioSource>());

        var eye = FindTelegraphAnchor(vis, c.telegraphBone);
        if (eye != null) SetPrivate(ai, "telegraphAnchor", eye);
        else Debug.LogWarning($"[{c.enemyName}] 얼굴 본 못 찾음 -> telegraphAnchor 직접 지정 필요");

        SetPrivateString(root.GetComponent<EnemyDropOnDeath>(), "sourceId", c.sourceId);

        FitBodyToModel(root, vis, c.scale);

        EnsureFolder(System.IO.Path.GetDirectoryName(c.prefabPath).Replace("\\", "/"));
        PrefabUtility.SaveAsPrefabAsset(root, c.prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void FitBodyToModel(GameObject root, GameObject vis, float scale)
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
        if (nav != null) { nav.height = h; nav.radius = r; }   // bounds 는 이미 scale 반영된 월드값
    }

    static Transform FindTelegraphAnchor(GameObject vis, string forceBone)
    {
        var all = vis.GetComponentsInChildren<Transform>(true);

        // 명시 지정(정확 일치 우선, 없으면 부분일치)이 있으면 그걸 최우선.
        if (!string.IsNullOrEmpty(forceBone))
        {
            var exact = all.FirstOrDefault(t => t.name.Equals(forceBone, System.StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
            var part = all.FirstOrDefault(t => t.name.ToLower().Contains(forceBone.ToLower()));
            if (part != null) return part;
        }

        // 자동 — 얼굴 '중앙' 본을 먼저(head/eye). jaw(협각)는 좌/우로 갈려 옆에서 나오므로 뒤로.
        string[] prefer = { "head", "eye", "mouth", "face", "neck", "jaw" };
        foreach (var key in prefer)
        {
            var hit = all.FirstOrDefault(t => t.name.ToLower().Contains(key));
            if (hit != null) return hit;
        }
        return null;
    }

    // ── helpers ────────────────────────────────────────────────────
    static AnimationClip SrcClip(FieldMonsterBuildConfig c, string clipName)
    {
        // 단일 FBX 방식: 모든 클립이 한 FBX 안의 서브클립 → 이름으로 골라낸다.
        if (!string.IsNullOrEmpty(c.singleAnimFbx))
        {
            var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(c.SingleFbxPath)
                                    .OfType<AnimationClip>().ToList();
            var m = reps.FirstOrDefault(x => x.name == clipName)
                 ?? reps.FirstOrDefault(x => x.name.Equals(clipName, System.StringComparison.OrdinalIgnoreCase));
            if (m == null) Debug.LogWarning($"[{c.enemyName}] 서브클립 없음: '{clipName}' in {c.SingleFbxPath}");
            return m;
        }
        // 개별 FBX 방식(기존): 클립마다 <name>.fbx.
        string p = $"{c.SrcAnim}/{clipName}.fbx";
        var clip = AssetDatabase.LoadAllAssetRepresentationsAtPath(p).OfType<AnimationClip>()
                                .FirstOrDefault(x => !x.name.StartsWith("__preview"));
        if (clip == null) Debug.LogWarning($"[{c.enemyName}] 원본 클립 없음: {p}");
        return clip;
    }
    static AnimationClip Clip(FieldMonsterBuildConfig c, string fbxName) => SrcClip(c, fbxName);
    static float ClipLength(FieldMonsterBuildConfig c, string fbxName, float fallback)
    {
        var clip = SrcClip(c, fbxName);
        return (clip != null && clip.length > 0.01f) ? clip.length : fallback;
    }

    static T LoadIf<T>(string path) where T : Object
        => string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);

    /// <summary>레이어 이름 목록 -> LayerMask 비트. 프로젝트에 없는 이름은 건너뜀.</summary>
    static int MaskFromLayers(string[] names)
    {
        if (names == null) return 0;
        int mask = 0;
        foreach (var n in names)
        {
            int l = LayerMask.NameToLayer(n);
            if (l >= 0) mask |= (1 << l);
            else Debug.LogWarning($"[FieldMonsterBuilder] 레이어 없음(무시): {n}");
        }
        return mask;
    }

    static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
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

/// <summary>
/// 몬스터 1종을 만들기 위한 설정 묶음. 몬스터별 빌더는 이걸 채워서
/// <c>FieldMonsterBuilder.Build(config)</c> 만 호출한다.
/// 기본값은 "거미S3형(기민한 치고빠지기)" 기준 — 몬스터마다 다른 부분만 덮어쓰면 된다.
/// </summary>
public class FieldMonsterBuildConfig
{
    // ── 경로(원본) ──
    public string srcRoot;           // "Assets/00.창동에셋/몬스터/거미S3"
    public string modelPrefabName;   // "거미S3_Skin1.prefab"
    public string skinMatName;       // "M_Spider_S3_Skin1.mat" (Materials/ 하위. skinMatPathOverride 있으면 무시)
    public string skinMatPathOverride; // 지정 시 Materials/ 대신 이 전체 경로 머티리얼 사용(예: Skins/RockMonster Default.mat)
    public string skinTexPath;       // 알베도 텍스처 전체 경로(이미 채워진 머티리얼이면 무시됨)
    public string singleAnimFbx;     // 지정 시 모든 클립이 이 FBX 하나의 서브클립(이름으로 찾음). 비우면 클립마다 개별 FBX.
    public string SrcAnim     => srcRoot + "/Animations";
    public string ModelPrefab => srcRoot + "/Prefabs/" + modelPrefabName;
    public string SkinMat     => !string.IsNullOrEmpty(skinMatPathOverride) ? skinMatPathOverride : srcRoot + "/Materials/" + skinMatName;
    public string SingleFbxPath => SrcAnim + "/" + singleAnimFbx + ".fbx";

    // ── 경로(생성물) ──
    public string ctrlFolder = "Assets/04.Animations/AnimationController/Enemy";  // 컨트롤러 위치(프로젝트 관례)
    public string ctrlName;          // "거미S3_Enemy.controller"
    public string soPath;            // "Assets/06.ScriptableObjects/Enemy/FieldData_SpiderS3.asset"
    public string prefabPath;        // "Assets/05.Prefabs/Enemy/Enemy_SpiderS3.prefab"
    public string CtrlPath => ctrlFolder + "/" + ctrlName;
    public string baseEnemy = "Assets/05.Prefabs/###/BaseEnemy.prefab";

    // ── 클립(파일명, srcRoot/Animations/<name>.fbx) — @/_ 접두사 섞여도 되니 전체 이름으로 ──
    public string clipIdle, clipFront, clipBack, clipLeft, clipRight;
    public string clipAttack, clipRoar, clipHit, clipDeath;
    public string clipAttackAlt;   // 선택: 공격 2번째 클립(지정 시 매 공격 A/B 랜덤). 예: 왼손/오른손 강타.
    public string clipRoar2;   // 선택: 포효 2단계 클립(지정 시 Detect→Detect2→Idle). 예: 무너짐→재조립.
    public string dormantClip; // 선택: 휴면 포즈(지정 시 이게 '기본 상태'. 발견(Detect) 전까지 제자리 유지). 예: RubblePose.
    public string crumbleClip; // 선택: 휴면 복귀(붕괴) 클립(지정 시 Sleep 트리거→Crumble→Dormant). 예: IdleToRubble.
    /// <summary>루프가 필요한 클립(원본 임포트 루프 ON 대상). 로코 + (있으면)휴면 포즈.</summary>
    public string[] LoopClips() => string.IsNullOrEmpty(dormantClip)
        ? new[] { clipIdle, clipFront, clipBack, clipLeft, clipRight }
        : new[] { clipIdle, clipFront, clipBack, clipLeft, clipRight, dormantClip };

    // ── 정체 ──
    public string enemyName, enemyId, sourceId;

    // ── 스탯 ──
    public float maxHP = 55f, moveSpeed = 5f, acceleration = 12f, angularSpeed = 480f;
    public float attackDamage = 12f, attackRange = 2.5f, attackApproachRatio = 0.85f;
    public float visionRange = 18f, visionAngle = 300f;
    public float scale = 1f;

    // ── 전조/패턴(기본 = 거미S3형) ──
    public float attackSpeedMul = 0.75f, roarSpeedMul = 0.6f, hitDelayRatio = 0.5f, attackCooldown = 0.2f;
    public float staggerChance = 1f;   // 피격 경직 확률(0~1). 1=매번(기존). 무거운 몹일수록 낮게.
    // 근접 타격 VFX(슬램/클랩). 비우면 없음.
    public string meleeImpactVfx;
    public UnityEngine.Vector3 meleeImpactOffset = new UnityEngine.Vector3(0f, 0f, 1.5f);
    public float meleeImpactScale = 1f, meleeImpactLifeTime = 3f;
    public CombatStepKind openingStep = CombatStepKind.None; public float openingStepSec = 0f;
    public float postAttackPause = 0.5f;
    public CombatStepKind afterStep = CombatStepKind.Retreat;
    public float afterStepMin = 0.8f, afterStepMax = 3.2f, retreatDiag = 45f;
    public float stepSpeedMul = 0.5f, strafeSpeedMul = 0.5f, strafeAnimMul = 1f, walkAnimRefSpeed = 2.5f, locoDirDamp = 0.12f;
    public bool wander = true; public float wanderRadius = 6f;
    public float sleepAfterIdle = 0f;   // 휴면형: 각성 후 이 시간(초) 이상 타깃 없으면 붕괴→휴면. 0=복귀 안 함.

    // ── 원거리 공격(발사체) — ranged=true 일 때만 사용 ──
    public bool ranged = false;
    public string projectileVfx, muzzleVfx, impactVfx;   // 프리팹 경로(비우면 슬롯만)
    public float projectileSpeed = 14f, projectileHitRadius = 0.6f, projectileLifeTime = 4f, projectileScale = 1f;
    public int projectileCount = 1; public float projectileInterval = 0.15f;   // >1 이면 연속 발사(팡팡)
    public float projectileHomingDeg = 0f, projectileHomingDuration = 0f;
    public float projectileSpawnHeight = 1.2f, projectileAimHeight = 1f;
    // 발사체를 막는 장애물 레이어(벽/나무/바위/건물). 이 중 프로젝트에 없는 건 자동 무시.
    public string[] projectileBlockLayers = { "Default", "Ground", "Building", "BuildBlock", "Tree", "Stone", "door" };

    // ── 전조/발사 위치 보정 ──
    public string telegraphBone;                       // 앵커 본 강제 지정(예: "Head"). 비우면 자동(head/eye 우선)
    public UnityEngine.Vector3 telegraphOffset = UnityEngine.Vector3.zero;  // 몸 기준 오프셋(x=오른쪽,y=위,z=앞)
    public float telegraphScale = 1f;                  // 전조 VFX 크기 배율(1=원본)
    public bool telegraphGrow = false;                 // 차징(작게→발사 순간 최대) 연출
    public float telegraphGrowFrom = 0.15f;            // 차징 시작 크기 비율
    public bool telegraphSyncToHit = false;            // 전조 재생을 hitDelay에 맞춰 딱 완성
    public string[] telegraphStrip;                    // 전조에서 제거할 자식 이름들(예: 중복 원)

    // ── 에셋(선택. 없으면 슬롯만) ──
    public string telegraphVfx = "Assets/00.창동에셋/VFX/VFX(전조)/ChargeProjectiles_Chargefx/Prefabs_Chargefx/Charge/Charge_01.prefab";
    public string spawnVfx, sndDetect, sndAttack, sndDie;
}
