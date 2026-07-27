using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 모래정령(사막 보스) 풀세트 자동 조립: 전용 애니 컨트롤러 + 보스 프리팹 + 데이터 SO + VFX 배선 + 황토색 텔레그래프.
/// 기존 적 BT(EnemyBrain)는 안 붙임 - 보스는 전용 SandElementalBossController 로 굴림.
///
/// ★얼음정령과 동일 구조. 원본 모델이 약 1.9m 라 플레이어(2m)와 동급이다 -> BossScale 로 키운다.
///   NavMeshAgent 는 스케일을 무시하므로(radius/height 가 절대값) 콜라이더와 계산을 따로 한다.
///
/// 메뉴: Tools > Enemy > Build Sand Elemental Boss (prefab + SO)
/// </summary>
public static class SandElementalBossBuilder
{
    const string ModelPath  = "Assets/00.창동에셋/몬스터/모래정령/Prefabs/모래정령.prefab";
    const string SoFolder   = "Assets/05.Prefabs/Enemy/SO";
    const string SoPath     = "Assets/05.Prefabs/Enemy/SO/EnemyData_SandElementalBoss.asset";
    const string PrefabPath = "Assets/05.Prefabs/Enemy/Enemy_Sand_Elemental_Boss.prefab";
    const string BaseEnemyPath = "Assets/05.Prefabs/###/BaseEnemy.prefab";   // 드롭박스/흡수VFX 참조 재사용원

    const string SandFolder = "Assets/00.창동에셋/VFX/VFX(사막 보스)/Sand VFX pack";
    const string EnvFolder  = "Assets/00.창동에셋/VFX/VFX(사막 환경효과)/Sand Effects pack/Prefabs";
    const string TelegraphSrc  = "Assets/12.VFX/WyvernFire/Wyvern_Telegraph.prefab";
    const string TelegraphSand = "Assets/12.VFX/WyvernFire/Sand_Telegraph.prefab";   // 와이번 것 복제 + 황토색

    // 원본 약 1.9m -> 보스감. 정령이라 무기가 없어 리치도 짧은데 키우면 팔 스팬이 같이 늘어 해결된다.
    const float BossScale = 2.2f;

    [MenuItem("Tools/Enemy/Build Sand Elemental Boss (prefab + SO)")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog(
            "모래정령 보스 생성",
            "사막 보스 풀세트를 만든다:\n" +
            "  - 전용 애니 컨트롤러(모래정령_Boss.controller)\n" +
            $"  - 보스 프리팹({PrefabPath}, scale {BossScale})\n" +
            $"  - 데이터 SO({SoPath}, 없으면 기본값/있으면 보존)\n" +
            "  - 사막 VFX 배선(파도/가시/늪/감금/다이브/궁극/방패)\n" +
            "  - 황토색 텔레그래프(와이번 것 복제)\n\n" +
            "기존 적 BT는 안 붙임 - 전용 컨트롤러.\n\n계속?",
            "생성", "취소")) return;

        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelPrefab == null) { Debug.LogError($"[SandBoss] 모델 프리팹 없음: {ModelPath}"); return; }

        // 0) 전용 애니 컨트롤러 보장
        SandElementalAnimatorBuilder.EnsureBuilt();
        var ctrlAsset = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SandElementalAnimatorBuilder.CtrlPath);
        if (ctrlAsset == null) Debug.LogWarning("[SandBoss] 애니 컨트롤러 로드 실패(애니 안 나올 수 있음)");

        // 1) 데이터 SO (밸런스는 인스펙터에서 튜닝)
        var so = AssetDatabase.LoadAssetAtPath<MeleeEnemyData>(SoPath);
        if (so == null)
        {
            EnsureFolder(SoFolder);
            so = ScriptableObject.CreateInstance<MeleeEnemyData>();
            so.enemyName = "모래정령"; so.enemyId = "sand_elemental_boss";
            so.maxHP = 1224f;   // 사막 보스. 얼음정령과 비슷한 급(근접 브루저).
            so.moveSpeed = 3.4f; so.acceleration = 12f; so.angularSpeed = 240f; so.stoppingDistance = 0f;
            so.visionRange = 30f; so.visionAngle = 360f;
            so.attackDamage = 46f; so.attackRange = 3.5f; so.attackApproachRatio = 0.9f; so.attackCooldown = 2.5f;
            so.hitDelay = 0.4f; so.animLength = 1.3f;   // Attack1 = 1.33s
            so.attackTrigger = "Attack"; so.hitTrigger = "Hit"; so.detectTrigger = "Detect"; so.dieTrigger = "Die";
            so.targetLostMemory = 3f;
            so.deathAnimDuration = 3.7f;   // Death = 3.67s
            so.detectStunDuration = 0f;
            AssetDatabase.CreateAsset(so, SoPath);
            AssetDatabase.SaveAssets();
        }

        // 2) 황토색 텔레그래프 보장(와이번 것 복제 후 색만 변경)
        var telegraph = EnsureSandTelegraph();

        // 3) 보스 프리팹 조립
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Enemy_Sand_Elemental_Boss";

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0) enemyLayer = 6;
        go.layer = enemyLayer;

        var animator = go.GetComponentInChildren<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        if (ctrlAsset != null) animator.runtimeAnimatorController = ctrlAsset;
        animator.applyRootMotion = false;

        // ★콜라이더/네비 핏: 스케일 적용 "전"(scale 1)에 bounds 를 재서 로컬 값을 얻는다.
        //   CapsuleCollider 는 transform 스케일이 곱해지므로 로컬 값, NavMeshAgent 는 스케일 무시라 월드 값.
        float bodyH = 2.0f, bodyR = 0.6f; Vector3 localCenter = new Vector3(0f, 1.0f, 0f);
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
        agent.baseOffset = 0f;   // 부유형(다리 없음)이라 0 유지
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
        var ctrl = GetOrAdd<SandElementalBossController>(go);
        var sobj = new SerializedObject(ctrl);
        SetRef(sobj, "data", so);
        SetRef(sobj, "waveVfx",      Sand("SandWave"));                // 전방 부채꼴 파도
        SetRef(sobj, "tornadoVfx",   Env("Sand tornado loop"));        // 세로 회오리 기둥(사방 지형방해)
        SetRef(sobj, "quicksandVfx", Sand("QuickSand"));               // 늪 장판(스크립트 0, 가장 안전)
        SetRef(sobj, "coffinVfx",    Sand("Sand_Coffin"));             // 감금(2.6s 후 붕괴)
        SetRef(sobj, "slamVfx",      Sand("Sand_Smash"));              // 다이브 강타(내장 예고 1.6s)
        SetRef(sobj, "ultVfx",       Sand("Pyramid_Explosion"));       // 궁극(지면 융기 대폭발)
        SetRef(sobj, "guardVfx",     Sand("Sand_shield"));             // 가드 실드(유일 loop)
        SetRef(sobj, "telegraphVfx", telegraph);
        // 전조(응축) - windup 동안 손/몸에 재생
        SetRef(sobj, "chargeVfxHand", Env("Sand FX 5"));               // 손 근처 미세 모래 흩날림
        SetRef(sobj, "chargeVfxBody", Env("Sand FX 4"));               // 몸 주위 모래 안개
        // 임팩트(타격) - 맞는 순간
        SetRef(sobj, "impactVfxMelee",  Sand("SandImpact"));           // 근접 피격(모래 튐)
        SetRef(sobj, "impactVfxHeavy",  Sand("Sand_combo_Smash"));     // 감금 붕괴/궁극 대형 착탄
        // 전조가 붙을 손 본(언팩된 계층에서 이름으로 탐색. 못 찾으면 spawnOffset 위치로 폴백)
        var handBone = FindBone(go, "SandElemental_RightHand", "RightHand", "Hand_R");
        if (handBone != null) SetRef(sobj, "chargeAnchor", handBone);
        else Debug.LogWarning("[SandBoss] 손 본 못 찾음 -> 전조가 spawnOffset 위치에 붙는다(무해)");
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
            $"[SandBoss] 생성 {(ok ? "완료" : "실패")}.\n" +
            $"  보스: {PrefabPath} (HP {so.maxHP}, 근접 {so.attackDamage}/{so.attackRange}m, scale {BossScale} = 약 {1.9f * BossScale:F1}m)\n" +
            $"  SO: {SoPath}\n  컨트롤러: {SandElementalAnimatorBuilder.CtrlPath}\n\n" +
            "정체성: 근접 위치지배/카운터.\n" +
            "패턴: P1 근접+파도+가시+감금 / P2 +늪+가드+다이브 / P3 +궁극(피라미드)\n\n" +
            "다음(종욱):\n" +
            "1. 보스 프리팹을 결계 밖 사냥터(NavMesh 베이크된 곳) 배치.\n" +
            "2. Play -> 붙으면 근접/가드, 중거리 파도/가시, 감금 시그니처. HP 66%/33%서 포효.\n" +
            "3. 손 본/전조 위치 안 맞으면 컨트롤러 chargeAnchor/spawnOffset 조정.\n" +
            "4. 애니 T포즈면 Tools>Enemy>Build Sand Elemental Animator 재실행.\n" +
            "5. Pyramid_Explosion 핑크면 미싱 서브머티리얼 교체(에디터서 재생 확인).\n" +
            "6. 밸런스는 EnemyData_SandElementalBoss.asset / 패턴 수치는 컨트롤러 인스펙터.");
    }

    // 와이번 텔레그래프를 복제해 황토색으로. 텔레그래프 문법을 보스 간 통일(형태 유지, 색만 변경).
    private static GameObject EnsureSandTelegraph()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphSand);
        if (existing != null) return existing;

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphSrc);
        if (src == null) { Debug.LogWarning($"[SandBoss] 와이번 텔레그래프 없음(텔레그래프 미연결): {TelegraphSrc}"); return null; }

        if (!AssetDatabase.CopyAsset(TelegraphSrc, TelegraphSand))
        { Debug.LogWarning("[SandBoss] 텔레그래프 복제 실패"); return null; }
        AssetDatabase.Refresh();

        var root = PrefabUtility.LoadPrefabContents(TelegraphSand);
        if (root != null)
        {
            // 파티클 startColor 를 황토색으로(셰이더가 COLOR 스트림으로 틴트한다)
            var sandColor = new Color(0.85f, 0.65f, 0.3f, 1f);
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = sandColor;
            }
            PrefabUtility.SaveAsPrefabAsset(root, TelegraphSand);
            PrefabUtility.UnloadPrefabContents(root);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphSand);
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
        else Debug.LogWarning($"[SandBoss] BaseEnemy 못 찾음(드롭 박스/흡수 VFX 미연결): {BaseEnemyPath}");

        var drop = GetOrAdd<EnemyDropOnDeath>(go);
        var ds = new SerializedObject(drop);
        var sid = ds.FindProperty("sourceId"); if (sid != null) sid.stringValue = "sand_elemental_boss";
        SetRef(ds, "boxPrefab", boxPrefab);
        ds.ApplyModifiedProperties();

        var absorb = GetOrAdd<EnemyAbsorbOnDeath>(go);
        var abs = new SerializedObject(absorb);
        SetRef(abs, "absorbVfxPrefab", absorbVfx);
        var bonus = abs.FindProperty("bonusHealPercent"); if (bonus != null) bonus.floatValue = 0.5f;
        abs.ApplyModifiedProperties();
    }

    private static GameObject Sand(string name)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{SandFolder}/{name}.prefab");
        if (p == null) Debug.LogWarning($"[SandBoss] VFX 없음: {SandFolder}/{name}.prefab");
        return p;
    }

    private static GameObject Env(string name)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvFolder}/{name}.prefab");
        if (p == null) Debug.LogWarning($"[SandBoss] 환경 VFX 없음: {EnvFolder}/{name}.prefab");
        return p;
    }

    private static void SetRef(SerializedObject sobj, string prop, Object value)
    {
        var p = sobj.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[SandBoss] 직렬화 필드 없음: {prop}");
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
