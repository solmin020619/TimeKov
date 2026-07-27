using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 얼음정령(설산 보스) 풀세트 자동 조립: 전용 애니 컨트롤러 + 보스 프리팹 + 데이터 SO + VFX 배선 + 하늘색 텔레그래프.
/// 기존 적 BT(EnemyBrain)는 안 붙임 - 보스는 전용 IceElementalBossController 로 굴림.
///
/// ★와이번과 다른 점: 원본 모델이 2.14m 라 플레이어(2m)와 동급이다 -> BossScale 로 키운다.
///   NavMeshAgent 는 스케일을 무시하므로(radius/height 가 절대값) 콜라이더와 계산을 따로 해야 한다.
///
/// 메뉴: Tools > Enemy > Build Ice Elemental Boss (prefab + SO)
/// </summary>
public static class IceElementalBossBuilder
{
    const string ModelPath  = "Assets/00.창동에셋/몬스터/얼음정령/Prefabs/얼음정령.prefab";
    const string SoFolder   = "Assets/05.Prefabs/Enemy/SO";
    const string SoPath     = "Assets/05.Prefabs/Enemy/SO/EnemyData_IceElementalBoss.asset";
    const string PrefabPath = "Assets/05.Prefabs/Enemy/Enemy_IceElemental_Boss.prefab";
    const string BaseEnemyPath = "Assets/05.Prefabs/###/BaseEnemy.prefab";   // 드롭박스/흡수VFX 참조 재사용원

    const string FrostFolder = "Assets/00.창동에셋/VFX/VFX(눈)/Elemental VFX Mega Bundle/Frost";
    const string TelegraphSrc = "Assets/12.VFX/WyvernFire/Wyvern_Telegraph.prefab";
    const string TelegraphIce = "Assets/12.VFX/WyvernFire/Ice_Telegraph.prefab";   // 와이번 것 복제 + 하늘색

    // 원본 2.14m -> 보스감. 정령이라 무기가 없어 리치도 짧은데 키우면 팔 스팬이 같이 늘어 해결된다.
    const float BossScale = 2.2f;

    [MenuItem("Tools/Enemy/Build Ice Elemental Boss (prefab + SO)")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog(
            "얼음정령 보스 생성",
            "설산 보스 풀세트를 만든다:\n" +
            "  - 전용 애니 컨트롤러(얼음정령_Boss.controller)\n" +
            $"  - 보스 프리팹({PrefabPath}, scale {BossScale})\n" +
            $"  - 데이터 SO({SoPath}, 없으면 기본값/있으면 보존)\n" +
            "  - Frost VFX 배선(빔/낙하3종/노바/궁극/날개)\n" +
            "  - 하늘색 텔레그래프(와이번 것 복제)\n\n" +
            "기존 적 BT는 안 붙임 - 전용 컨트롤러.\n\n계속?",
            "생성", "취소")) return;

        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelPrefab == null) { Debug.LogError($"[IceBoss] 모델 프리팹 없음: {ModelPath}"); return; }

        // 0) 전용 애니 컨트롤러 보장
        IceElementalAnimatorBuilder.EnsureBuilt();
        var ctrlAsset = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(IceElementalAnimatorBuilder.CtrlPath);
        if (ctrlAsset == null) Debug.LogWarning("[IceBoss] 애니 컨트롤러 로드 실패(애니 안 나올 수 있음)");

        // 1) 데이터 SO (밸런스는 인스펙터에서 튜닝)
        var so = AssetDatabase.LoadAssetAtPath<MeleeEnemyData>(SoPath);
        if (so == null)
        {
            EnsureFolder(SoFolder);
            so = ScriptableObject.CreateInstance<MeleeEnemyData>();
            so.enemyName = "얼음정령"; so.enemyId = "ice_elemental_boss";
            so.maxHP = 1050f;   // 설산 보스. 자연 와이번(입문보스)보다 확실히 위, 화염정령(최종)보다 아래.
            so.moveSpeed = 3.2f; so.acceleration = 12f; so.angularSpeed = 240f; so.stoppingDistance = 0f;
            so.visionRange = 30f; so.visionAngle = 360f;
            so.attackDamage = 39f; so.attackRange = 3.5f; so.attackApproachRatio = 0.9f; so.attackCooldown = 2.5f;
            so.hitDelay = 0.4f; so.animLength = 1.2f;   // Attack1 = 1.133s
            so.attackTrigger = "Attack"; so.hitTrigger = "Hit"; so.detectTrigger = "Detect"; so.dieTrigger = "Die";
            so.targetLostMemory = 3f;
            so.deathAnimDuration = 3.6f;   // Death2 = 3.5s (스스로 지면 아래로 침강)
            so.detectStunDuration = 0f;
            AssetDatabase.CreateAsset(so, SoPath);
            AssetDatabase.SaveAssets();
        }

        // 2) 하늘색 텔레그래프 보장(와이번 것 복제 후 색만 변경)
        var telegraph = EnsureIceTelegraph();

        // 3) 보스 프리팹 조립
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Enemy_IceElemental_Boss";

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0) enemyLayer = 6;
        go.layer = enemyLayer;

        var animator = go.GetComponentInChildren<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        if (ctrlAsset != null) animator.runtimeAnimatorController = ctrlAsset;
        animator.applyRootMotion = false;

        // ★콜라이더/네비 핏: 스케일 적용 "전"(scale 1)에 bounds 를 재서 로컬 값을 얻는다.
        //   CapsuleCollider 는 transform 스케일이 곱해지므로 로컬 값을 넣어야 하고,
        //   NavMeshAgent 는 스케일을 무시하므로 실제 월드 크기(로컬 x BossScale)를 넣어야 한다.
        float bodyH = 2.2f, bodyR = 0.6f; Vector3 localCenter = new Vector3(0f, 1.1f, 0f);
        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null)
        {
            var b = smr.bounds;   // 이 시점 scale=1 이라 월드 == 로컬
            bodyH = Mathf.Max(1f, b.size.y);
            bodyR = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.35f, 0.3f, 1.2f);
            localCenter = go.transform.InverseTransformPoint(b.center);
        }

        var col = GetOrAdd<CapsuleCollider>(go);
        col.center = localCenter; col.radius = bodyR; col.height = bodyH; col.isTrigger = false;

        var agent = GetOrAdd<NavMeshAgent>(go);
        agent.radius = Mathf.Clamp(bodyR * BossScale, 0.5f, 2f);
        agent.height = bodyH * BossScale;
        agent.baseOffset = 0f;   // 원래 떠 있는 모델(바인드 포즈 바닥이 Y=0.42m)이라 0 유지
        agent.speed = so.moveSpeed; agent.acceleration = so.acceleration;
        agent.angularSpeed = so.angularSpeed;
        agent.stoppingDistance = Mathf.Max(0f, so.attackRange * so.attackApproachRatio);

        var rb = GetOrAdd<Rigidbody>(go);
        rb.isKinematic = true; rb.useGravity = false;

        var audio = GetOrAdd<AudioSource>(go);
        audio.playOnAwake = false; audio.spatialBlend = 1f;

        var health = GetOrAdd<EnemyHealth>(go);
        health.maxHP = so.maxHP; health.currentHP = so.maxHP;

        GetOrAdd<EnemyFeedback>(go);

        // 4) 컨트롤러 + VFX 배선
        var ctrl = GetOrAdd<IceElementalBossController>(go);
        var sobj = new SerializedObject(ctrl);
        SetRef(sobj, "data", so);
        SetRef(sobj, "beamVfx",   Frost("FrostRay"));
        SetRef(sobj, "rainVfx",   Frost("SkySingle_Frost"));   // 고드름 한 발(코드로 사방 다발)
        SetRef(sobj, "novaVfx",   Frost("AOE_Explosion_Frost"));   // 바닥 원이 퍼지며 밀어내는 폭발(예쁜 원)
        SetRef(sobj, "ultVfx",    Frost("NuclearBomb_Frost"));
        SetRef(sobj, "dashVfx",   Frost("Wing_Frost"));
        SetRef(sobj, "telegraphVfx", telegraph);
        // 전조(응축) - windup 동안 손/몸에 재생
        SetRef(sobj, "chargeVfxHand", Frost("MeshFX_Frost"));      // 손 소형 응축(근접/빔)
        SetRef(sobj, "chargeVfxBody", Frost("FrostAura"));         // 몸 냉기(낙하/노바/돌진/궁극)
        // 임팩트(타격) - 맞는 순간
        SetRef(sobj, "impactVfxMelee",  Frost("Update/IceCubesExplosion"));  // 근접 피격(진짜 3D 얼음 큐브 파편)
        SetRef(sobj, "impactVfxRanged", Frost("Impact_Frost"));           // 빔틱(초경량 3PS)
        SetRef(sobj, "impactVfxHeavy",  Frost("AOE_Explosion_Frost"));    // 궁극 대형 착탄
        // 전조가 붙을 손 본(언팩된 계층에서 이름으로 탐색. 못 찾으면 beamOffset 위치로 폴백)
        var handBone = FindBone(go, "IceElemental_RightHand", "RightHand", "Hand_R");
        if (handBone != null) SetRef(sobj, "chargeAnchor", handBone);
        else Debug.LogWarning("[IceBoss] 손 본 못 찾음 -> 전조가 beamOffset 위치에 붙는다(무해)");
        sobj.ApplyModifiedProperties();

        WireRewards(go);

        // 5) 마지막에 스케일 적용(위 콜라이더 계산이 scale 1 기준이므로 순서 중요)
        go.transform.localScale = Vector3.one * BossScale;

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out bool ok);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (saved != null) { Selection.activeObject = saved; EditorGUIUtility.PingObject(saved); }

        Debug.Log(
            $"[IceBoss] 생성 {(ok ? "완료" : "실패")}.\n" +
            $"  보스: {PrefabPath} (HP {so.maxHP}, 근접 {so.attackDamage}/{so.attackRange}m, scale {BossScale} = 약 {2.14f * BossScale:F1}m)\n" +
            $"  SO: {SoPath}\n  컨트롤러: {IceElementalAnimatorBuilder.CtrlPath}\n\n" +
            "패턴: P1 근접+빔 / P2 +낙하+노바+가드반격 / P3 +활강돌진+궁극\n\n" +
            "다음(종욱):\n" +
            "1. 보스 프리팹을 결계 밖 사냥터(NavMesh 베이크된 곳) 배치.\n" +
            "2. Play -> 멀면 빔, 붙으면 근접. HP 66%/33%서 포효 -> 패턴 추가.\n" +
            "3. 빔 발사 위치 안 맞으면 컨트롤러 beamOffset 조정(손 근처).\n" +
            "4. 애니 T포즈면 Tools>Enemy>Build Ice Elemental Animator 재실행.\n" +
            "5. 밸런스는 EnemyData_IceElementalBoss.asset / 패턴 수치는 컨트롤러 인스펙터.");
    }

    // 와이번 텔레그래프를 복제해 하늘색으로. 텔레그래프 문법을 보스 간 통일하는 게 학습상 이득이라
    // 형태는 그대로 두고 색만 바꾼다(얼음=하늘색 / 모래=황토 / 화염=주황).
    private static GameObject EnsureIceTelegraph()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphIce);
        if (existing != null) return existing;

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphSrc);
        if (src == null) { Debug.LogWarning($"[IceBoss] 와이번 텔레그래프 없음(텔레그래프 미연결): {TelegraphSrc}"); return null; }

        if (!AssetDatabase.CopyAsset(TelegraphSrc, TelegraphIce))
        { Debug.LogWarning("[IceBoss] 텔레그래프 복제 실패"); return null; }
        AssetDatabase.Refresh();

        var root = PrefabUtility.LoadPrefabContents(TelegraphIce);
        if (root != null)
        {
            // 파티클 startColor 를 하늘색으로(셰이더가 COLOR 스트림으로 틴트한다)
            var iceColor = new Color(0.45f, 0.85f, 1f, 1f);
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = iceColor;
            }
            PrefabUtility.SaveAsPrefabAsset(root, TelegraphIce);
            PrefabUtility.UnloadPrefabContents(root);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphIce);
    }

    // 보스 사망 보상. boxPrefab/흡수VFX 는 BaseEnemy 에서 참조 재사용(경로 하드코딩 회피).
    private static void WireRewards(GameObject go)
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
        else Debug.LogWarning($"[IceBoss] BaseEnemy 못 찾음(드롭 박스/흡수 VFX 미연결): {BaseEnemyPath}");

        var drop = GetOrAdd<EnemyDropOnDeath>(go);
        var ds = new SerializedObject(drop);
        var sid = ds.FindProperty("sourceId"); if (sid != null) sid.stringValue = "ice_elemental_boss";
        SetRef(ds, "boxPrefab", boxPrefab);
        ds.ApplyModifiedProperties();

        var absorb = GetOrAdd<EnemyAbsorbOnDeath>(go);
        var abs = new SerializedObject(absorb);
        SetRef(abs, "absorbVfxPrefab", absorbVfx);
        var bonus = abs.FindProperty("bonusHealPercent"); if (bonus != null) bonus.floatValue = 0.5f;
        abs.ApplyModifiedProperties();
    }

    private static GameObject Frost(string name)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{FrostFolder}/{name}.prefab");
        if (p == null) Debug.LogWarning($"[IceBoss] VFX 없음: {FrostFolder}/{name}.prefab");
        return p;
    }

    private static void SetRef(SerializedObject sobj, string prop, Object value)
    {
        var p = sobj.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[IceBoss] 직렬화 필드 없음: {prop}");
    }

    // 언팩된 계층에서 본 Transform 을 이름으로 찾는다.
    // 정확 일치 먼저, 없으면 부분 일치(righthand/hand_r)로 폴백. 못 찾으면 null.
    private static Transform FindBone(GameObject root, params string[] names)
    {
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var n in names)
            foreach (var t in all)
                if (t.name == n) return t;
        foreach (var t in all)
        {
            string lower = t.name.ToLower();
            if (lower.Contains("righthand") || lower.Contains("hand_r") || lower.Contains("r_hand"))
                return t;
        }
        return null;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static void EnsureFolder(string fullPath)
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
