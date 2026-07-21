using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 헬 몬스터(용암) 지상 4종 공용 빌더. 컨트롤러 + SO + 프리팹을 한 번에 굽는다.
/// 몹별 차이는 HellConfig 하나로만 준다. 실제 설정은 HellMonsterConfigs.cs 참고.
///
/// 클립 이름이 몹마다 조금씩 다르다(Idle vs Idle1, BattleRoar vs BattleRoar1)
/// -> 전부 config 로 받는다. FBX 는 대문자 .FBX 라 로더가 양쪽 다 시도한다.
/// </summary>
public static class HellMonsterBuilder
{
    const string BaseEnemyPath = "Assets/05.Prefabs/###/BaseEnemy.prefab";
    // 흡수 VFX 참조 원본. BaseEnemy 에는 EnemyAbsorbOnDeath 자체가 없어서 여기서 가져온다.
    const string AbsorbRefPath = "Assets/05.Prefabs/Enemy/Enemy_DarknessSpider.prefab";

    public static void Build(HellConfig c)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(c.modelPath);
        if (model == null) { Debug.LogError($"[{c.enemyName}] 모델 없음: {c.modelPath}"); return; }

        NormalizeLoopClips(c);
        var ctrl = BuildAnimator(c);
        var so = BuildData(c);
        BuildPrefab(c, model, ctrl, so);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(c.PrefabPath);
        if (saved != null) { Selection.activeObject = saved; EditorGUIUtility.PingObject(saved); }

        Debug.Log(
            $"[{c.enemyName}] 생성 완료.\n" +
            $"  프리팹: {c.PrefabPath} (HP {so.maxHP}, 공격 {so.attackDamage})\n" +
            $"  SO: {c.SoPath} / 컨트롤러: {c.CtrlPath}\n" +
            $"  패턴 {so.attacks.Length}개 / 전조 {so.telegraphTime}초\n\n" +
            "다음(종욱):\n" +
            "1. 스폰존 spawnEntries 에 추가(용암맵). NavMesh 베이크 확인.\n" +
            "2. 드롭시트에 sourceId=" + c.sourceId + " 행 추가.\n" +
            "3. 도감 CodexPreviewConfig 에 등록.\n" +
            "4. Play -> 전조가 충분히 보이는지, 피할 수 있는지 확인. 안 되면 SO 의 telegraphTime 을 올려라.\n\n" +
            $"입 위치: 프리팹 선택하면 씬 뷰에 주황 구가 뜬다. 인스펙터의 Muzzle Offset(현재 {c.muzzleOffset})\n" +
            "  을 움직여 맞춘 뒤, 그 값을 HellMonsterConfigs 의 muzzleOffset 에 적어야 재빌드해도 남는다.");
    }

    // -- 애니메이터 (GUID 고정: 재빌드해도 프리팹 참조가 안 깨진다) --
    static AnimatorController BuildAnimator(HellConfig c)
    {
        EnsureFolder(c.WorkFolder);
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(c.CtrlPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(c.CtrlPath);
        else ClearController(ctrl);

        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);   // EnemyFeedback 호환(선언만)

        var sm = ctrl.layers[0].stateMachine;

        var idle = sm.AddState("Idle");
        idle.motion = Clip(c, c.clipIdle);
        sm.defaultState = idle;

        // 이동: 스트레이프 클립이 없는 회사 에셋이라 전후 1D 블렌드로 간다.
        ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendParameter = "Speed";
        tree.blendType = BlendTreeType.Simple1D;
        // ★AddChild 로 준 threshold 는 useAutomaticThresholds 가 켜져 있으면 무시되고 균등 재배치된다.
        tree.useAutomaticThresholds = false;
        AddMotion(tree, Clip(c, c.clipIdle), 0f);
        AddMotion(tree, Clip(c, c.clipWalk), c.walkSpeedRef);
        AddMotion(tree, Clip(c, c.clipRun), c.runSpeedRef);

        AddState(sm, "BattleRoar", Clip(c, c.clipRoar));
        AddState(sm, "Die", Clip(c, c.clipDeath));

        // 피격 반응 상태(맞으면 흠칫)
        if (c.useHitReaction && c.hitStates != null)
            for (int i = 0; i < c.hitStates.Length; i++)
                AddState(sm, c.hitStates[i], Clip(c, i < c.hitClips.Length ? c.hitClips[i] : c.hitClips[0]));

        foreach (var a in c.attacks)
            AddState(sm, a.state, Clip(c, a.clipName));

        if (c.useLeap)
        {
            AddState(sm, "JumpStart", Clip(c, c.clipJumpStart));
            AddState(sm, "JumpFly", Clip(c, c.clipJumpFly));
            AddState(sm, "JumpEnd", Clip(c, c.clipJumpEnd));
        }
        if (c.useBurrow)
        {
            AddState(sm, "Submerge", Clip(c, c.clipSubmerge));
            AddState(sm, "Emerge", Clip(c, c.clipEmerge));
        }

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static void AddMotion(BlendTree tree, Motion m, float threshold)
    {
        if (m == null) return;
        tree.AddChild(m, threshold);
    }

    static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion m)
    {
        if (sm.states.Any(s => s.state.name == name)) return null;
        var st = sm.AddState(name);
        st.motion = m;
        st.writeDefaultValues = true;
        return st;
    }

    static void ClearController(AnimatorController ctrl)
    {
        foreach (var p in ctrl.parameters.ToList()) ctrl.RemoveParameter(p);
        var sm = ctrl.layers[0].stateMachine;
        foreach (var t in sm.anyStateTransitions.ToList()) sm.RemoveAnyStateTransition(t);
        foreach (var st in sm.states.ToList()) sm.RemoveState(st.state);
        sm.defaultState = null;

        // RemoveState 는 상태만 지우고 BlendTree 서브에셋은 파일에 남긴다.
        // 안 지우면 재빌드할 때마다 컨트롤러 파일에 고아 트리가 쌓인다.
        string path = AssetDatabase.GetAssetPath(ctrl);
        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
            if (sub is BlendTree) Object.DestroyImmediate(sub, true);
    }

    // -- 데이터 SO --
    static HellMonsterData BuildData(HellConfig c)
    {
        EnsureFolder(c.SoFolder);
        var so = AssetDatabase.LoadAssetAtPath<HellMonsterData>(c.SoPath);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<HellMonsterData>();
            AssetDatabase.CreateAsset(so, c.SoPath);
        }

        so.enemyName = c.enemyName; so.enemyId = c.enemyId;
        so.maxHP = c.maxHP;
        so.moveSpeed = c.moveSpeed; so.acceleration = 18f; so.angularSpeed = 480f; so.stoppingDistance = 0f;
        so.visionRange = c.visionRange; so.visionAngle = c.visionAngle;
        so.attackDamage = c.attackDamage; so.attackRange = c.attackRange;
        so.attackApproachRatio = 0.9f; so.attackCooldown = c.attackCooldown;
        so.targetLostMemory = 3f;
        so.deathAnimDuration = c.deathAnimDuration;
        so.detectStunDuration = 0f;
        so.attackTrigger = "Attack"; so.hitTrigger = "Hit"; so.detectTrigger = "Detect"; so.dieTrigger = "Die";

        // 전조(공통)
        so.telegraphVfx = Load(c.telegraphVfxPath);
        so.groundTelegraphVfx = Load(c.groundTelegraphVfxPath);
        so.groundTelegraphUnitRadius = c.groundTelegraphUnitRadius;
        so.fillCircleVfx = EnsureRing(c);
        so.fillCircleUnitRadius = c.fillCircleUnitRadius;
        so.fillCircleColor = c.fillCircleColor;
        so.fillOutlineDim = c.fillOutlineDim;
        so.actionGapMin = c.actionGapMin;
        so.actionGapMax = c.actionGapMax;
        so.fillCircleFromScale = c.fillCircleFromScale;
        so.fillCircleLinger = c.fillCircleLinger;
        so.useHitReaction = c.useHitReaction;
        so.hitStates = c.hitStates;
        so.hitReactionTime = c.hitReactionTime;
        so.hitReactionCooldown = c.hitReactionCooldown;
        so.knockbackDistance = c.knockbackDistance;
        so.knockbackTime = c.knockbackTime;
        so.muzzleVfx = Load(c.muzzleVfxPath);
        so.projectilePrefab = c.HasRanged ? EnsureProjectile(c) : null;
        so.telegraphTime = c.telegraphTime;
        so.telegraphColor = c.telegraphColor;
        so.telegraphScale = c.telegraphScale;
        so.telegraphHeight = c.telegraphHeight;

        // 패턴
        var list = new List<HellAttack>();
        foreach (var a in c.attacks)
        {
            list.Add(new HellAttack
            {
                label = a.label, state = a.state, weight = a.weight,
                minRange = a.minRange, maxRange = a.maxRange,
                hitTime = a.hitTime, totalTime = a.totalTime, cooldown = a.cooldown,
                damageMul = a.damageMul, radius = a.radius, halfAngle = a.halfAngle, reach = a.reach,
                impactVfx = string.IsNullOrEmpty(a.impactVfxPath) ? null : Load(a.impactVfxPath),
                kind = a.kind, telegraph = a.telegraph,
                telegraphTime = a.telegraphTime, telegraphScaleMul = a.telegraphScaleMul,
                shots = a.shots, shotGap = a.shotGap, spreadAngle = a.spreadAngle, homing = a.homing,
                lockFacing = a.lockFacing,
            });
        }
        so.attacks = list.ToArray();
        so.repeatPenalty = 0.35f;

        so.useLeap = c.useLeap;
        so.leapWeight = c.leapWeight; so.leapMinRange = c.leapMinRange; so.leapMaxRange = c.leapMaxRange;
        so.leapCooldown = c.leapCooldown; so.leapFlyTime = c.leapFlyTime;
        so.leapDamageMul = c.leapDamageMul; so.leapRadius = c.leapRadius; so.leapArcHeight = c.leapArcHeight; so.leapTelegraphTime = c.leapTelegraphTime; so.leapTelegraphScale = c.leapTelegraphScale;
        so.leapImpactVfx = string.IsNullOrEmpty(c.leapImpactVfxPath) ? null : Load(c.leapImpactVfxPath);

        so.useBurrow = c.useBurrow;
        so.burrowWeight = c.burrowWeight; so.burrowCooldown = c.burrowCooldown;
        so.burrowUnderTime = c.burrowUnderTime; so.burrowEmergeDistance = c.burrowEmergeDistance;
        so.burrowDamageMul = c.burrowDamageMul; so.burrowRadius = c.burrowRadius;
        so.burrowImpactVfx = string.IsNullOrEmpty(c.burrowImpactVfxPath) ? null : Load(c.burrowImpactVfxPath);

        so.roarOnDetect = true; so.roarTime = c.roarTime;
        so.idleState = "Idle"; so.moveState = "Locomotion"; so.roarState = "BattleRoar"; so.dieState = "Die";

        EditorUtility.SetDirty(so);
        return so;
    }

    // 투사체 프리팹 조립: 날아가는 VFX + WyvernFireball(재사용) + 착탄 VFX.
    // Charge/Muzzle/Effect/Hit 이 번호별로 짝이 맞는 팩이라 같은 번호를 쓰면 톤이 통일된다.
    static GameObject EnsureProjectile(HellConfig c)
    {
        EnsureFolder(c.WorkFolder);
        string path = $"{c.WorkFolder}/{c.PrefabName}_Projectile.prefab";

        var visual = Load(c.projectileVfxPath);
        if (visual == null)
        {
            Debug.LogWarning($"[{c.enemyName}] 투사체 VFX 없음: {c.projectileVfxPath}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        var root = (GameObject)PrefabUtility.InstantiatePrefab(visual);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = $"{c.PrefabName}_Projectile";

        var fb = root.AddComponent<WyvernFireball>();
        var so = new SerializedObject(fb);
        SetFloat(so, "speed", c.projectileSpeed);
        SetFloat(so, "life", 5f);
        SetFloat(so, "hitRadius", c.projectileHitRadius);
        SetFloat(so, "explodeRadius", c.projectileExplodeRadius);
        SetRef(so, "explodeVfx", Load(c.projectileHitVfxPath));
        so.ApplyModifiedProperties();

        var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
        Object.DestroyImmediate(root);
        if (!ok) Debug.LogWarning($"[{c.enemyName}] 투사체 프리팹 저장 실패");
        return saved;
    }

    // 착지 예고용 링 프리팹 조립.
    // ★SM_VFX_Ring 메시 프리팹은 머티리얼이 비어 있어서 그냥 쓰면 분홍으로 나온다.
    //   같은 팩의 저작된 링 머티리얼을 물려서 바닥에 눕힌 링을 만든다.
    static GameObject EnsureRing(HellConfig c)
    {
        EnsureFolder(c.WorkFolder);
        string path = $"{c.WorkFolder}/{c.PrefabName}_Ring.prefab";

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(c.fillCircleVfxPath);
        if (src == null)
        {
            Debug.LogWarning($"[{c.enemyName}] 링 메시 없음: {c.fillCircleVfxPath}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(c.fillCircleMaterialPath);
        if (mat == null) Debug.LogWarning($"[{c.enemyName}] 링 머티리얼 없음(분홍으로 보인다): {c.fillCircleMaterialPath}");

        var root = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = $"{c.PrefabName}_Ring";

        // ★메시가 이미 바닥에 누워 있는지(XZ 평면) 세로로 서 있는지(XY 평면) 보고 결정한다.
        //   무조건 90도 돌리면, 이미 누워 있는 메시는 세워져서 위에서 볼 때 선으로만 보인다.
        //   실제로 그래서 원이 안 보였다.
        var mf = root.GetComponentInChildren<MeshFilter>(true);
        if (mf != null && mf.sharedMesh != null)
        {
            Vector3 e = mf.sharedMesh.bounds.extents;
            bool flatAlready = e.y <= e.z * 0.5f;   // 높이가 거의 없으면 이미 바닥 평면
            root.transform.localRotation = flatAlready ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);
            Debug.Log($"[{c.enemyName}] 링 메시 bounds {e} -> {(flatAlready ? "이미 눕혀짐(회전 안 함)" : "세워져 있어 90도 눕힘")}");
        }
        else root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        if (mat != null)
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var arr = new Material[r.sharedMaterials.Length == 0 ? 1 : r.sharedMaterials.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                r.sharedMaterials = arr;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

        var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
        Object.DestroyImmediate(root);
        if (!ok) Debug.LogWarning($"[{c.enemyName}] 링 프리팹 저장 실패");
        return saved;
    }

    // -- 프리팹 --
    static void BuildPrefab(HellConfig c, GameObject model, AnimatorController ctrl, HellMonsterData so)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = c.PrefabName;
        go.transform.localScale = Vector3.one * c.scale;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0) enemyLayer = 6;
        SetLayerRecursive(go, enemyLayer);

        var animator = go.GetComponentInChildren<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = false;

        // 모델에 딸려온 잡 콜라이더 제거(화염보스에서 땅속 박스로 당한 적 있다)
        foreach (var stray in go.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(stray, true);

        var col = go.AddComponent<CapsuleCollider>();
        col.height = c.bodyHeight;
        col.radius = c.bodyRadius;
        col.center = new Vector3(0f, c.bodyHeight * 0.5f, 0f);
        col.direction = 1;

        var agent = GetOrAdd<NavMeshAgent>(go);
        agent.height = c.bodyHeight;
        agent.radius = Mathf.Max(0.3f, c.bodyRadius);
        agent.speed = c.moveSpeed;
        agent.stoppingDistance = 0f;
        agent.baseOffset = 0f;

        var rb = GetOrAdd<Rigidbody>(go);
        rb.isKinematic = true; rb.useGravity = false;

        var audio = GetOrAdd<AudioSource>(go);
        audio.playOnAwake = false; audio.spatialBlend = 1f;
        audio.minDistance = 6f; audio.maxDistance = 40f;

        var health = GetOrAdd<EnemyHealth>(go);
        health.maxHP = so.maxHP; health.currentHP = so.maxHP;

        var feedback = GetOrAdd<EnemyFeedback>(go);
        feedback.SetData(so);

        ConfigureVision(go, c);

        var ai = GetOrAdd<HellMonsterAI>(go);
        var sobj = new SerializedObject(ai);
        SetRef(sobj, "data", so);
        SetRef(sobj, "telegraphAnchor", FindMuzzleBone(go, c));
        var mo = sobj.FindProperty("muzzleOffset");
        if (mo != null) mo.vector3Value = c.muzzleOffset;
        sobj.ApplyModifiedProperties();

        WireRewards(go, c.sourceId);

        PrefabUtility.SaveAsPrefabAsset(go, c.PrefabPath, out bool ok);
        Object.DestroyImmediate(go);
        if (!ok) Debug.LogWarning($"[{c.enemyName}] 프리팹 저장 실패");
    }

    // 입/총구로 쓸 본을 계층에서 찾는다. FBX 가 바이너리라 본 이름을 미리 알 수 없어서
    // 이름 패턴으로 훑고, 못 찾으면 가장 높이 있는 본으로 대체한다(대개 머리).
    // config 의 muzzleBoneHint 로 정확한 이름을 직접 줄 수도 있다.
    static Transform FindMuzzleBone(GameObject go, HellConfig c)
    {
        var all = go.GetComponentsInChildren<Transform>(true);

        if (!string.IsNullOrEmpty(c.muzzleBoneHint))
            foreach (var t in all)
                if (t.name.Equals(c.muzzleBoneHint, System.StringComparison.OrdinalIgnoreCase))
                    return t;

        // 후보를 로그로 보여준다. 자동 선택이 마음에 안 들면 muzzleBoneHint 에 이름을 적으면 된다.
        var cands = new List<string>();
        foreach (var t in all)
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("jaw") || n.Contains("mouth") || n.Contains("muzzle")
             || n.Contains("snout") || n.Contains("head") || n.Contains("neck"))
                cands.Add(t.name);
        }
        if (cands.Count > 0)
            Debug.Log($"[{c.enemyName}] 입 앵커 후보: {string.Join(", ", cands)}\n" +
                      "  (다른 걸 쓰려면 config 의 muzzleBoneHint 에 이름을 적어라)");

        string[] keys = { "jaw", "mouth", "muzzle", "snout", "head" };
        foreach (var key in keys)
            foreach (var t in all)
                if (t.name.ToLowerInvariant().Contains(key))
                {
                    Debug.Log($"[{c.enemyName}] 입 앵커로 '{t.name}' 사용");
                    return t;
                }

        // 폴백: 루트에서 가장 높은 본(머리일 확률이 높다)
        Transform best = null; float bestY = float.MinValue;
        foreach (var t in all)
        {
            if (t == go.transform) continue;
            float y = t.position.y - go.transform.position.y;
            if (y > bestY) { bestY = y; best = t; }
        }
        if (best != null) Debug.LogWarning($"[{c.enemyName}] 머리 본 이름을 못 찾아 최상단 '{best.name}' 로 대체(위치가 어긋나면 muzzleBoneHint 지정)");
        return best;
    }

    // ★VisionSensor 는 targetMask 가 [SerializeField] 인데 기본값이 없다.
    //   그냥 AddComponent 하면 마스크가 Nothing 이라 OverlapSphere 가 아무것도 못 잡고,
    //   결과적으로 몹이 플레이어를 영원히 못 봐서 가만히 서 있는다(에러도 안 난다).
    //   기존 적들은 BaseEnemy 에 설정된 값을 물려받는데, 이 빌더는 모델에서 조립하므로 직접 복사해 온다.
    static void ConfigureVision(GameObject go, HellConfig c)
    {
        var vision = GetOrAdd<VisionSensor>(go);

        int targetBits = 1 << Mathf.Max(0, LayerMask.NameToLayer("Player"));
        int obstacleBits = 1;   // Default

        var baseEnemy = AssetDatabase.LoadAssetAtPath<GameObject>(BaseEnemyPath);
        var bv = baseEnemy != null ? baseEnemy.GetComponent<VisionSensor>() : null;
        if (bv != null)
        {
            var bs = new SerializedObject(bv);
            var tm = bs.FindProperty("targetMask");
            var om = bs.FindProperty("obstacleMask");
            if (tm != null) targetBits = tm.intValue;
            if (om != null) obstacleBits = om.intValue;
        }
        else Debug.LogWarning($"[{c.enemyName}] BaseEnemy 의 VisionSensor 를 못 찾아 기본 마스크로 대체한다");

        var vs = new SerializedObject(vision);
        SetInt(vs, "targetMask", targetBits);
        SetInt(vs, "obstacleMask", obstacleBits);
        SetFloat(vs, "visionRange", c.visionRange);
        SetFloat(vs, "visionAngle", c.visionAngle);
        SetFloat(vs, "eyeHeight", c.eyeHeight);
        vs.ApplyModifiedProperties();

        if (targetBits == 0)
            Debug.LogError($"[{c.enemyName}] VisionSensor targetMask 가 비었다. 이대로면 몹이 플레이어를 못 본다.");
    }

    // 드롭 + 시간흡수. ★흡수는 재원 빌더가 빠뜨린 부분이라 여기선 처음부터 넣는다.
    static void WireRewards(GameObject go, string sourceId)
    {
        GameObject boxPrefab = null, absorbVfx = null;

        var baseEnemy = AssetDatabase.LoadAssetAtPath<GameObject>(BaseEnemyPath);
        if (baseEnemy != null)
        {
            var bd = baseEnemy.GetComponent<EnemyDropOnDeath>();
            if (bd != null) boxPrefab = bd.BoxPrefab;
        }
        else Debug.LogWarning($"[Hell] BaseEnemy 못 찾음(드롭 미연결): {BaseEnemyPath}");

        // BaseEnemy 에는 EnemyAbsorbOnDeath 가 아예 없다 -> 실참조를 가진 프리팹에서 가져온다
        var absorbRef = AssetDatabase.LoadAssetAtPath<GameObject>(AbsorbRefPath);
        if (absorbRef != null)
        {
            var ra = absorbRef.GetComponent<EnemyAbsorbOnDeath>();
            if (ra != null)
            {
                var p = new SerializedObject(ra).FindProperty("absorbVfxPrefab");
                if (p != null) absorbVfx = p.objectReferenceValue as GameObject;
            }
        }
        if (absorbVfx == null) Debug.LogWarning($"[Hell] 흡수 VFX 참조 실패: {AbsorbRefPath}");

        var drop = GetOrAdd<EnemyDropOnDeath>(go);
        var ds = new SerializedObject(drop);
        var sid = ds.FindProperty("sourceId"); if (sid != null) sid.stringValue = sourceId;
        SetRef(ds, "boxPrefab", boxPrefab);
        ds.ApplyModifiedProperties();

        var absorb = GetOrAdd<EnemyAbsorbOnDeath>(go);
        var abs = new SerializedObject(absorb);
        SetRef(abs, "absorbVfxPrefab", absorbVfx);
        abs.ApplyModifiedProperties();
    }

    // -- helpers --
    static void NormalizeLoopClips(HellConfig c)
    {
        foreach (var name in new[] { c.clipIdle, c.clipWalk, c.clipRun })
        {
            if (string.IsNullOrEmpty(name)) continue;
            var importer = FindImporter(c, name);
            if (importer == null) { Debug.LogWarning($"[{c.enemyName}] FBX 임포터 없음: {name}"); continue; }

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) continue;

            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
                if (!clips[i].loopTime) { clips[i].loopTime = true; changed = true; }

            if (changed) { importer.clipAnimations = clips; importer.SaveAndReimport(); }
        }
    }

    static ModelImporter FindImporter(HellConfig c, string clipName)
    {
        var im = AssetImporter.GetAtPath($"{c.AnimFolder}/{clipName}.FBX") as ModelImporter;
        return im != null ? im : AssetImporter.GetAtPath($"{c.AnimFolder}/{clipName}.fbx") as ModelImporter;
    }

    static AnimationClip Clip(HellConfig c, string fbxName)
    {
        if (string.IsNullOrEmpty(fbxName)) return null;
        var clip = AssetDatabase.LoadAllAssetRepresentationsAtPath($"{c.AnimFolder}/{fbxName}.FBX")
                                .OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview"));
        if (clip == null)
            clip = AssetDatabase.LoadAllAssetRepresentationsAtPath($"{c.AnimFolder}/{fbxName}.fbx")
                                .OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview"));
        if (clip == null) Debug.LogWarning($"[{c.enemyName}] 클립 없음: {c.AnimFolder}/{fbxName}.FBX");
        return clip;
    }

    static GameObject Load(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (p == null) Debug.LogWarning($"[Hell] 에셋 없음: {path}");
        return p;
    }

    static void SetRef(SerializedObject sobj, string prop, Object value)
    {
        var p = sobj.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[Hell] 직렬화 필드 없음: {prop}");
    }

    static void SetInt(SerializedObject sobj, string prop, int value)
    {
        var p = sobj.FindProperty(prop);
        if (p != null) p.intValue = value;
        else Debug.LogWarning($"[Hell] 직렬화 필드 없음: {prop}");
    }

    static void SetFloat(SerializedObject sobj, string prop, float value)
    {
        var p = sobj.FindProperty(prop);
        if (p != null) p.floatValue = value;
        else Debug.LogWarning($"[Hell] 직렬화 필드 없음: {prop}");
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
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
