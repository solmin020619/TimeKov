using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 자폭거미(SpiderBot) 풀세트 자동 조립: 미니 애니 컨트롤러 + 프리팹 + SO + 폭발 VFX/비프음 배선.
/// 난파 우주선 오작동 드론 컨셉의 순수 자폭 러셔. 전용 SelfDestructSpiderAI 로 굴림(공유 BT 미사용).
///
/// 보스 빌더와 같은 방식(모델에서 조립, WireRewards). 단 컨트롤러는 GUID 고정(load-or-clear)이라 재빌드해도 참조 안 깨짐.
/// 클립은 원본 FBX 참조(복사 안 함). Idle/Walk 는 loopTime 1회 세팅(안 켜면 굳음).
/// 메뉴: Tools > Enemy > Build Suicide Spider (prefab + SO)
/// </summary>
public static class SpiderBotBuilder
{
    const string ModelPath  = "Assets/00.창동에셋/몬스터/자폭거미/Prefabs/자폭거미.prefab";
    const string AnimFolder = "Assets/00.창동에셋/몬스터/자폭거미/Animations";
    const string WorkFolder = "Assets/05.Prefabs/Enemy/자폭거미";
    const string CtrlPath   = WorkFolder + "/자폭거미_Enemy.controller";
    const string SoFolder   = "Assets/05.Prefabs/Enemy/SO";
    const string SoPath     = SoFolder + "/EnemyData_SpiderBot.asset";
    const string PrefabPath = "Assets/05.Prefabs/Enemy/Enemy_SpiderBot.prefab";
    const string BaseEnemyPath = "Assets/05.Prefabs/###/BaseEnemy.prefab";

    // 폭발범위 지면 링(텔레그래프)은 일부러 없다. 경고는 몸통 점멸 + 비프음뿐.
    const string ExplodeVfx = "Assets/00.창동에셋/VFX/VFX(전조)/Anime VFX URP/Shared/Particles/VFX_Bomb_Explosion.prefab";
    // 난파 우주선 드론 컨셉이라 우주선 에셋 비프음을 쓴다. 취향 바뀌면 같은 폴더 Beep01~08 로 교체.
    const string ArmBeepSfx = "Assets/18.외부에셋/우주선/Common/SoundEffects/Beep04.wav";

    // 로코 클립(루프 필수)
    static readonly string[] LoopClips = { "SpiderBot@Idle", "SpiderBot@Walk" };

    [MenuItem("Tools/Enemy/Build Suicide Spider (prefab + SO)")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog(
            "자폭거미 생성",
            "자폭 러셔 풀세트를 만든다:\n" +
            "  - 미니 애니 컨트롤러(Idle/Walk/Die)\n" +
            $"  - 프리팹({PrefabPath})\n" +
            $"  - 데이터 SO({SoPath}, 없으면 기본값/있으면 보존)\n" +
            "  - 폭발 VFX 배선(지면 범위링 없음)\n\n순수 자폭(근접 물기 없음). 계속?",
            "생성", "취소")) return;

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) { Debug.LogError($"[SpiderBot] 모델 프리팹 없음: {ModelPath}"); return; }

        NormalizeLoopClips();            // Idle/Walk 루프 ON(멱등)
        var ctrl = BuildAnimator();      // GUID 고정
        var so   = BuildData();
        BuildPrefab(model, ctrl, so);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (saved != null) { Selection.activeObject = saved; EditorGUIUtility.PingObject(saved); }

        Debug.Log(
            $"[SpiderBot] 생성 완료.\n" +
            $"  프리팹: {PrefabPath} (HP {so.maxHP}, 폭발 {so.attackDamage}x배율)\n" +
            $"  SO: {SoPath}\n  컨트롤러: {CtrlPath}\n\n" +
            "정체성: 순수 자폭 러셔(추격->점화->폭발). arming 중 처치하면 폭발 안 함=카운터플레이.\n\n" +
            "다음(종욱):\n" +
            "1. 스폰존(EnemySpawnPoint)의 spawnEntries 에 Enemy_SpiderBot 추가(maxCount 1~2, 전 바이옴 저밀도).\n" +
            "2. ★스폰할 바이옴에 NavMesh 베이크됐는지 확인(안 되면 스폰 실패).\n" +
            "3. 드롭시트에 sourceId=suicide_spider 행 추가.\n" +
            "4. Play -> 추격/점화 발광/폭발 확인. arming 시간(회피 가능한지)/폭발 데미지 튜닝은 SO+컨트롤러 인스펙터.\n" +
            "5. 작열이 약하면 프리팹 인스펙터 armGlowPeak 를 올려라(블룸 세기에 따라 체감이 다르다).");
    }

    // -- 애니메이터 (GUID 고정: 재빌드해도 참조 안 깨짐) --
    static AnimatorController BuildAnimator()
    {
        EnsureFolder(WorkFolder);
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        else ClearController(ctrl);

        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);   // EnemyFeedback 호환(선언만)

        var sm = ctrl.layers[0].stateMachine;

        var idle = sm.AddState("Idle");
        idle.motion = Clip("SpiderBot@Idle");
        sm.defaultState = idle;

        var loco = sm.AddState("Locomotion");
        loco.motion = Clip("SpiderBot@Walk");

        var toLoco = idle.AddTransition(loco);
        toLoco.hasExitTime = false; toLoco.duration = 0.1f;
        toLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        var toIdle = loco.AddTransition(idle);
        toIdle.hasExitTime = false; toIdle.duration = 0.1f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // 사망 상태(터미널). 자폭이라 전용 사망 클립 없음 -> Idle 로 대체(폭발 순간 렌더 꺼져 안 보임).
        // EnemyFeedback.PlayDeath 가 "Die" 를 CrossFade 하므로 상태만 있으면 경고 없음.
        sm.AddState("Die").motion = Clip("SpiderBot@Idle");

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static void ClearController(AnimatorController ctrl)
    {
        foreach (var p in ctrl.parameters.ToList()) ctrl.RemoveParameter(p);
        var sm = ctrl.layers[0].stateMachine;
        foreach (var t in sm.anyStateTransitions.ToList()) sm.RemoveAnyStateTransition(t);
        foreach (var st in sm.states.ToList()) sm.RemoveState(st.state);
        sm.defaultState = null;
    }

    // -- 데이터 SO --
    static MeleeEnemyData BuildData()
    {
        var so = AssetDatabase.LoadAssetAtPath<MeleeEnemyData>(SoPath);
        if (so == null)
        {
            EnsureFolder(SoFolder);
            so = ScriptableObject.CreateInstance<MeleeEnemyData>();
            so.enemyName = "자폭거미"; so.enemyId = "suicide_spider";
            so.maxHP = 30f;   // 유리대포: arming 중 처치 가능해야 카운터플레이 성립
            so.moveSpeed = 5.5f; so.acceleration = 20f; so.angularSpeed = 720f; so.stoppingDistance = 0f;
            so.visionRange = 16f; so.visionAngle = 360f;
            so.attackDamage = 25f; so.attackRange = 2.5f; so.attackApproachRatio = 0.9f; so.attackCooldown = 1f;
            so.hitDelay = 0.5f; so.animLength = 1f;
            so.attackTrigger = "Attack"; so.hitTrigger = "Hit"; so.detectTrigger = "Detect"; so.dieTrigger = "Die";
            so.targetLostMemory = 2f;
            so.deathAnimDuration = 0.15f;   // 전용 사망 애니 없음 -> 폭발 후 즉시 정리
            so.detectStunDuration = 0f;
            AssetDatabase.CreateAsset(so, SoPath);
        }
        EditorUtility.SetDirty(so);
        return so;
    }

    // -- 프리팹 (모델에서 조립, 보스 방식) --
    static void BuildPrefab(GameObject model, AnimatorController ctrl, MeleeEnemyData so)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Enemy_SpiderBot";

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0) enemyLayer = 6;
        go.layer = enemyLayer;

        var animator = go.GetComponentInChildren<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = false;

        // 콜라이더/네비 핏(모델 크기)
        float bodyH = 1f, bodyR = 0.5f; Vector3 localCenter = new Vector3(0f, 0.5f, 0f);
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            bodyH = Mathf.Max(0.4f, b.size.y);
            bodyR = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.4f, 0.25f, 1.5f);
            localCenter = go.transform.InverseTransformPoint(b.center);
        }

        var col = GetOrAdd<CapsuleCollider>(go);
        col.center = localCenter; col.radius = bodyR; col.height = bodyH; col.isTrigger = false;

        var agent = GetOrAdd<NavMeshAgent>(go);
        agent.radius = bodyR; agent.height = bodyH;
        agent.speed = so.moveSpeed; agent.acceleration = so.acceleration; agent.angularSpeed = so.angularSpeed;
        agent.stoppingDistance = 0f;

        var rb = GetOrAdd<Rigidbody>(go);
        rb.isKinematic = true; rb.useGravity = false;

        var audio = GetOrAdd<AudioSource>(go);
        audio.playOnAwake = false; audio.spatialBlend = 1f;
        audio.minDistance = 4f;      // 점화 사거리 안에선 비프가 또렷하게
        audio.maxDistance = 25f;

        var health = GetOrAdd<EnemyHealth>(go);
        health.maxHP = so.maxHP; health.currentHP = so.maxHP;

        GetOrAdd<EnemyFeedback>(go);

        var ai = GetOrAdd<SelfDestructSpiderAI>(go);
        var sobj = new SerializedObject(ai);
        SetRef(sobj, "data", so);
        SetRef(sobj, "explodeVfx", Load(ExplodeVfx));

        var beep = AssetDatabase.LoadAssetAtPath<AudioClip>(ArmBeepSfx);
        if (beep == null) Debug.LogWarning($"[SpiderBot] 비프음 없음: {ArmBeepSfx} (점화 경고가 무음이 된다)");
        SetRef(sobj, "armSound", beep);

        sobj.ApplyModifiedProperties();

        WireRewards(go);
        // 머리 위 체력바 + 이름표. 이 빌더는 모델에서 조립하므로 BaseEnemy 처럼 상속받지 못한다.
        EnemyBuildUtil.AttachWorldHpBar(go, "SpiderBot");

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out bool ok);
        Object.DestroyImmediate(go);

        if (!ok) Debug.LogWarning("[SpiderBot] 프리팹 저장 실패");
    }

    static void WireRewards(GameObject go)
    {
        GameObject boxPrefab = null, absorbVfx = null;
        var baseEnemy = AssetDatabase.LoadAssetAtPath<GameObject>(BaseEnemyPath);
        if (baseEnemy != null)
        {
            var bd = baseEnemy.GetComponent<EnemyDropOnDeath>();
            if (bd != null) boxPrefab = bd.BoxPrefab;
            var ba = baseEnemy.GetComponent<EnemyAbsorbOnDeath>();
            if (ba != null)
            {
                var p = new SerializedObject(ba).FindProperty("absorbVfxPrefab");
                if (p != null) absorbVfx = p.objectReferenceValue as GameObject;
            }
        }
        else Debug.LogWarning($"[SpiderBot] BaseEnemy 못 찾음(드롭/흡수 미연결): {BaseEnemyPath}");

        var drop = GetOrAdd<EnemyDropOnDeath>(go);
        var ds = new SerializedObject(drop);
        var sid = ds.FindProperty("sourceId"); if (sid != null) sid.stringValue = "suicide_spider";
        SetRef(ds, "boxPrefab", boxPrefab);
        ds.ApplyModifiedProperties();

        var absorb = GetOrAdd<EnemyAbsorbOnDeath>(go);
        var abs = new SerializedObject(absorb);
        SetRef(abs, "absorbVfxPrefab", absorbVfx);
        abs.ApplyModifiedProperties();
    }

    // -- helpers --
    static void NormalizeLoopClips()
    {
        foreach (var name in LoopClips)
        {
            string path = $"{AnimFolder}/{name}.FBX";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) importer = AssetImporter.GetAtPath($"{AnimFolder}/{name}.fbx") as ModelImporter;
            if (importer == null) { Debug.LogWarning($"[SpiderBot] FBX 임포터 없음: {path}"); continue; }

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) continue;

            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
                if (!clips[i].loopTime) { clips[i].loopTime = true; changed = true; }

            if (changed) { importer.clipAnimations = clips; importer.SaveAndReimport(); Debug.Log($"[SpiderBot] 루프 ON: {name}"); }
        }
    }

    static AnimationClip Clip(string fbxName)
    {
        var c = AssetDatabase.LoadAllAssetRepresentationsAtPath($"{AnimFolder}/{fbxName}.FBX")
                             .OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview"));
        if (c == null)
            c = AssetDatabase.LoadAllAssetRepresentationsAtPath($"{AnimFolder}/{fbxName}.fbx")
                             .OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview"));
        if (c == null) Debug.LogWarning($"[SpiderBot] 클립 없음: {AnimFolder}/{fbxName}.FBX");
        return c;
    }

    static GameObject Load(string path)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (p == null) Debug.LogWarning($"[SpiderBot] VFX 없음: {path}");
        return p;
    }

    static void SetRef(SerializedObject sobj, string prop, Object value)
    {
        var p = sobj.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[SpiderBot] 직렬화 필드 없음: {prop}");
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void EnsureFolder(string fullPath)
    {
        if (AssetDatabase.IsValidFolder(fullPath)) return;
        var parts = fullPath.Split('/');
        string curr = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{curr}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(curr, parts[i]);
            curr = next;
        }
    }
}
