using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 와이번 지상 보스 프리팹 + 데이터 SO 자동 조립.
/// 모델(Wyvern_PBR) + Wyvern_Override 애니 + EnemyHealth/EnemyFeedback/NavMeshAgent/콜라이더 + WyvernBossController를 한 번에.
/// 기존 적 BT(EnemyBrain)는 안 붙임 - 보스는 전용 컨트롤러로 굴림.
/// 메뉴: Tools > Enemy > Build Wyvern Boss (prefab + SO)
/// </summary>
public static class WyvernBossBuilder
{
    const string ModelPath    = "Assets/03.Model/Enemy/10.Wyvern/Prefabs/Wyvern_PBR.prefab";
    const string OverridePath = "Assets/03.Model/Enemy/10.Wyvern/Wyvern_Override.overrideController";
    const string SoFolder     = "Assets/05.Prefabs/Enemy/SO";
    const string SoPath       = "Assets/05.Prefabs/Enemy/SO/EnemyData_WyvernBoss.asset";
    const string PrefabPath   = "Assets/05.Prefabs/Enemy/Enemy_Wyvern_Boss.prefab";

    [MenuItem("Tools/Enemy/Build Wyvern Boss (prefab + SO)")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog(
            "와이번 보스 생성",
            "지상 보스 프리팹 + 데이터 SO를 만든다:\n" +
            $"  - SO: {SoPath} (없으면 보스 기본값으로 생성, 있으면 수치 보존)\n" +
            $"  - 프리팹: {PrefabPath}\n\n" +
            "구성: Wyvern 모델 + Wyvern_Override 애니 + EnemyHealth/EnemyFeedback/NavMeshAgent/콜라이더 + WyvernBossController.\n" +
            "(기존 적 BT는 안 붙임 - 전용 컨트롤러)\n\n계속?",
            "생성", "취소")) return;

        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelPrefab == null) { Debug.LogError($"[WyvernBoss] 모델 프리팹 없음: {ModelPath}"); return; }

        var overrideCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(OverridePath);
        if (overrideCtrl == null) Debug.LogWarning($"[WyvernBoss] 오버라이드 컨트롤러 없음(애니 안 나올 수 있음): {OverridePath}");

        // 1) 데이터 SO (보스 기본값. 밸런스는 종욱이 F7로 테스트하며 인스펙터서 튜닝)
        var so = AssetDatabase.LoadAssetAtPath<MeleeEnemyData>(SoPath);
        if (so == null)
        {
            EnsureFolder(SoFolder);
            so = ScriptableObject.CreateInstance<MeleeEnemyData>();
            so.enemyName = "와이번";
            so.enemyId = "wyvern_boss";
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

        // 2) 모델 인스턴스화 + 언팩(독립 프리팹으로)
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Enemy_Wyvern_Boss";

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0) enemyLayer = 6;
        go.layer = enemyLayer;

        // 애니메이터
        var animator = go.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            if (overrideCtrl != null) animator.runtimeAnimatorController = overrideCtrl;
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
        agent.height = bodyH;
        agent.baseOffset = 0f;
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
        var dataProp = sobj.FindProperty("data");
        if (dataProp != null) { dataProp.objectReferenceValue = so; sobj.ApplyModifiedProperties(); }

        // 3) 프리팹 저장
        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out bool ok);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (saved != null) { Selection.activeObject = saved; EditorGUIUtility.PingObject(saved); }

        Debug.Log(
            $"[WyvernBoss] 생성 {(ok ? "완료" : "실패")}.\n" +
            $"  프리팹: {PrefabPath}\n  SO: {SoPath} (HP {so.maxHP}, 공격 {so.attackDamage}, 사거리 {so.attackRange})\n\n" +
            "다음 단계(종욱):\n" +
            "1. 프리팹을 결계 밖 사냥터(NavMesh 베이크된 곳)에 배치.\n" +
            "2. Play -> 다가가면 추적+물기, 때리면 피격/사망 애니 확인.\n" +
            "3. 콜라이더(하늘색 캡슐)가 몸통에 안 맞으면 인스펙터서 조정.\n" +
            "4. 애니가 T포즈면 Wyvern_Override에 클립 매핑 비어있는 것 -> EnemyOverrideControllerBuilder 재실행.\n" +
            "5. 밸런스(HP/공격력/속도)는 EnemyData_WyvernBoss.asset에서 튜닝(F7로 코어 올려보며).");
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
