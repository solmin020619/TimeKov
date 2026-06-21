using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 와이번 지상 보스 풀세트 자동 조립: 전용 애니 컨트롤러 + 파이어볼 발사체 프리팹 + 보스 프리팹 + 데이터 SO.
/// 기존 적 BT(EnemyBrain)는 안 붙임 - 보스는 전용 WyvernBossController로 굴림.
/// 메뉴: Tools > Enemy > Build Wyvern Boss (prefab + SO)
/// </summary>
public static class WyvernBossBuilder
{
    const string ModelPath     = "Assets/03.Model/Enemy/10.Wyvern/Prefabs/Wyvern_PBR.prefab";
    const string SoFolder      = "Assets/05.Prefabs/Enemy/SO";
    const string SoPath        = "Assets/05.Prefabs/Enemy/SO/EnemyData_WyvernBoss.asset";
    const string PrefabPath    = "Assets/05.Prefabs/Enemy/Enemy_Wyvern_Boss.prefab";
    const string FireballPath  = "Assets/05.Prefabs/Enemy/Wyvern_Fireball.prefab";
    const string VfxFireball   = "Assets/Vefects/Anime Stylized VFX/Shared/Particles/VFX_Fireball.prefab";
    const string VfxExplosion  = "Assets/Vefects/Anime Stylized VFX/Shared/Particles/VFX_Explosion_Omni.prefab";
    const string VfxFire       = "Assets/Vefects/Anime Stylized VFX/Shared/Particles/VFX_Fire.prefab";
    const string BaseEnemyPath = "Assets/05.Prefabs/Enemy/BaseEnemy.prefab";   // 드롭박스/흡수VFX 참조 재사용원

    [MenuItem("Tools/Enemy/Build Wyvern Boss (prefab + SO)")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog(
            "와이번 보스 생성",
            "지상 보스 풀세트를 만든다:\n" +
            $"  - 전용 애니 컨트롤러(WyvernBoss.controller)\n" +
            $"  - 파이어볼 발사체 프리팹({FireballPath})\n" +
            $"  - 보스 프리팹({PrefabPath})\n" +
            $"  - 데이터 SO({SoPath}, 없으면 보스 기본값/있으면 보존)\n\n" +
            "기존 적 BT는 안 붙임 - 전용 컨트롤러.\n\n계속?",
            "생성", "취소")) return;

        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelPrefab == null) { Debug.LogError($"[WyvernBoss] 모델 프리팹 없음: {ModelPath}"); return; }

        // 0) 전용 애니 컨트롤러 보장
        WyvernBossAnimatorBuilder.EnsureBuilt();
        var ctrlAsset = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(WyvernBossAnimatorBuilder.CtrlPath);
        if (ctrlAsset == null) Debug.LogWarning("[WyvernBoss] 애니 컨트롤러 로드 실패(애니 안 나올 수 있음)");

        // 1) 데이터 SO (보스 기본값. 밸런스는 종욱이 F7로 테스트하며 인스펙터서 튜닝)
        var so = AssetDatabase.LoadAssetAtPath<MeleeEnemyData>(SoPath);
        if (so == null)
        {
            EnsureFolder(SoFolder);
            so = ScriptableObject.CreateInstance<MeleeEnemyData>();
            so.enemyName = "와이번"; so.enemyId = "wyvern_boss";
            so.maxHP = 800f;
            so.moveSpeed = 3.5f; so.acceleration = 14f; so.angularSpeed = 220f; so.stoppingDistance = 0f;
            so.visionRange = 30f; so.visionAngle = 360f;
            so.attackDamage = 30f; so.attackRange = 4f; so.attackApproachRatio = 0.9f; so.attackCooldown = 2.5f;
            so.hitDelay = 0.6f; so.animLength = 1.6f;
            so.hitTrigger = "Hit"; so.detectTrigger = "Detect"; so.dieTrigger = "Die";
            so.targetLostMemory = 3f; so.deathAnimDuration = 3.5f; so.detectStunDuration = 0f;
            AssetDatabase.CreateAsset(so, SoPath);
            AssetDatabase.SaveAssets();
        }

        // 2) 파이어볼 발사체 프리팹
        var fireball = EnsureFireballPrefab();

        // 3) 보스 프리팹 조립
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Enemy_Wyvern_Boss";

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0) enemyLayer = 6;
        go.layer = enemyLayer;

        var animator = go.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            if (ctrlAsset != null) animator.runtimeAnimatorController = ctrlAsset;
            animator.applyRootMotion = false;
        }

        // 콜라이더/네비 자동 핏 (모델 실제 bounds 기준 - 스케일 무관)
        float bodyH = 4f, bodyR = 1.5f; Vector3 localCenter = new Vector3(0f, 2f, 0f);
        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null)
        {
            var b = smr.bounds;
            bodyH = Mathf.Max(1f, b.size.y);
            bodyR = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.35f, 0.5f, 2.5f);
            localCenter = go.transform.InverseTransformPoint(b.center);
        }

        var agent = GetOrAdd<NavMeshAgent>(go);
        agent.radius = Mathf.Clamp(bodyR, 0.5f, 2f);
        agent.height = bodyH; agent.baseOffset = 0f;
        agent.speed = so.moveSpeed; agent.acceleration = so.acceleration;
        agent.angularSpeed = so.angularSpeed;
        agent.stoppingDistance = Mathf.Max(0f, so.attackRange * so.attackApproachRatio);

        var rb = GetOrAdd<Rigidbody>(go);
        rb.isKinematic = true; rb.useGravity = false;

        var col = GetOrAdd<CapsuleCollider>(go);
        col.center = localCenter; col.radius = bodyR; col.height = bodyH; col.isTrigger = false;

        var audio = GetOrAdd<AudioSource>(go);
        audio.playOnAwake = false; audio.spatialBlend = 1f;

        var health = GetOrAdd<EnemyHealth>(go);
        health.maxHP = so.maxHP; health.currentHP = so.maxHP;

        GetOrAdd<EnemyFeedback>(go);

        var ctrl = GetOrAdd<WyvernBossController>(go);
        var sobj = new SerializedObject(ctrl);
        SetRef(sobj, "data", so);
        SetRef(sobj, "fireballPrefab", fireball);
        SetRef(sobj, "spreadFireVfx", AssetDatabase.LoadAssetAtPath<GameObject>(VfxFire));
        sobj.ApplyModifiedProperties();

        WireRewards(go);   // 시간 흡수(보스 대량) + 아이템 드롭 + 도감 등록

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out bool ok);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (saved != null) { Selection.activeObject = saved; EditorGUIUtility.PingObject(saved); }

        Debug.Log(
            $"[WyvernBoss] 생성 {(ok ? "완료" : "실패")}.\n" +
            $"  보스: {PrefabPath} (HP {so.maxHP}, 근접 {so.attackDamage}/{so.attackRange}m, 파이어볼 사거리 22m)\n" +
            $"  파이어볼: {FireballPath}\n  컨트롤러: {WyvernBossAnimatorBuilder.CtrlPath}\n\n" +
            "다음(종욱):\n" +
            "1. 보스 프리팹을 결계 밖 사냥터(NavMesh 베이크된 곳) 배치.\n" +
            "2. Play -> 멀면 파이어볼, 가까우면 물기. 때리면 사망 애니.\n" +
            "3. 파이어볼 발사 위치 안 맞으면 WyvernBossController.fireOffset 조정(입 근처).\n" +
            "4. 애니 T포즈면 Tools>Enemy>Build Wyvern Boss Animator 재실행.\n" +
            "5. 밸런스는 EnemyData_WyvernBoss.asset / 파이어볼 수치는 컨트롤러 인스펙터.");
    }

    // 파이어볼 발사체 프리팹: 루트(WyvernFireball) + VFX_Fireball(자식 비주얼) + 착탄 VFX 할당
    private static GameObject EnsureFireballPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(FireballPath);
        if (existing != null) return existing;

        var root = new GameObject("Wyvern_Fireball");
        var fb = root.AddComponent<WyvernFireball>();

        var vfxFb = AssetDatabase.LoadAssetAtPath<GameObject>(VfxFireball);
        if (vfxFb != null)
        {
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(vfxFb);
            vis.transform.SetParent(root.transform, false);
        }
        else Debug.LogWarning($"[WyvernBoss] 파이어볼 VFX 없음: {VfxFireball}");

        var vfxEx = AssetDatabase.LoadAssetAtPath<GameObject>(VfxExplosion);
        if (vfxEx != null)
        {
            var sobj = new SerializedObject(fb);
            SetRef(sobj, "explodeVfx", vfxEx);
            sobj.ApplyModifiedProperties();
        }
        else Debug.LogWarning($"[WyvernBoss] 폭발 VFX 없음: {VfxExplosion}");

        PrefabUtility.SaveAsPrefabAsset(root, FireballPath);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(FireballPath);
    }

    // 보스 사망 보상 컴포넌트 부착. boxPrefab/흡수VFX는 BaseEnemy에서 참조 재사용(경로 하드코딩 회피).
    // 확장: 나중에 '특수 해금' 등은 EnemyHealth.OnDeath 구독 컴포넌트를 추가로 붙이면 끝(보상=컴포넌트 단위).
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
        else Debug.LogWarning($"[WyvernBoss] BaseEnemy 못 찾음(드롭 박스/흡수 VFX 미연결): {BaseEnemyPath}");

        // 아이템 드롭 + 도감 등록(HandleDeath서 CodexDiscovery.DiscoverMonster 호출). sourceId=드롭테이블/도감 매칭키.
        var drop = GetOrAdd<EnemyDropOnDeath>(go);
        var ds = new SerializedObject(drop);
        var sid = ds.FindProperty("sourceId"); if (sid != null) sid.stringValue = "wyvern_boss";
        SetRef(ds, "boxPrefab", boxPrefab);
        ds.ApplyModifiedProperties();

        // 시간 흡수(보스=최대 시간의 50% 대량 흡수)
        var absorb = GetOrAdd<EnemyAbsorbOnDeath>(go);
        var abs = new SerializedObject(absorb);
        SetRef(abs, "absorbVfxPrefab", absorbVfx);
        var bonus = abs.FindProperty("bonusHealPercent"); if (bonus != null) bonus.floatValue = 0.5f;
        abs.ApplyModifiedProperties();
    }

    private static void SetRef(SerializedObject sobj, string prop, Object value)
    {
        var p = sobj.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[WyvernBoss] 직렬화 필드 없음: {prop}");
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
