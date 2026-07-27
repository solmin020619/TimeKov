using UnityEditor;
using UnityEngine;

/// <summary>
/// 화염보스(고정형 아틸러리) 풀세트 자동 조립: 멀티레이어 애니 컨트롤러 + 보스 프리팹 + SO + VFX 배선 + 파이어볼 프리팹 + 주황 텔레그래프.
///
/// ★얼음/모래와 다른 점:
///   - 모델 = 화염보스_풀이펙트.prefab(자체 파티클 39개 포함). ActionCTRL(데모) 제거, uv_move(소용돌이) 유지.
///   - 고정형 = NavMeshAgent 안 붙임(BossMotor 가 null 방어).
///   - 파이어볼 프리팹을 VFX_Fireball_Projectile_Static + WyvernFireball 로 조립.
///   - VFX 는 VFX(전조) 킷의 불 서브셋 사용.
///
/// 메뉴: Tools > Enemy > Build Fire Boss (prefab + SO)
/// </summary>
public static class FireBossBuilder
{
    const string ModelPath  = "Assets/00.창동에셋/몬스터/화염보스/Prefabs/화염보스_풀이펙트.prefab";
    const string SoFolder   = "Assets/05.Prefabs/Enemy/SO";
    const string SoPath     = "Assets/05.Prefabs/Enemy/SO/EnemyData_FireBoss.asset";
    const string PrefabPath = "Assets/05.Prefabs/Enemy/Enemy_Fire_Boss.prefab";
    const string FireballPath = "Assets/05.Prefabs/Enemy/FireBoss_Fireball.prefab";
    const string BaseEnemyPath = "Assets/05.Prefabs/###/BaseEnemy.prefab";

    // VFX(전조) 킷은 하위 폴더로 나뉘어 있다(Anime VFX URP / Sci-fi effects 2). J() 가 순회 검색.
    static readonly string[] VfxFolders =
    {
        "Assets/00.창동에셋/VFX/VFX(전조)/Anime VFX URP/Shared/Particles",
        "Assets/00.창동에셋/VFX/VFX(전조)/Sci-fi effects 2/Prefabs",
        "Assets/00.창동에셋/VFX/VFX(전조)",
    };
    const string TelegraphSrc  = "Assets/12.VFX/WyvernFire/Wyvern_Telegraph.prefab";
    const string TelegraphFire = "Assets/12.VFX/WyvernFire/Fire_Telegraph.prefab";   // 와이번 것 복제 + 주황색

    // 화염보스는 목적형 대형 모델(약 6m 컨테이너 + 하반신 소용돌이). 기본 native(1.0), 필요시 종욱이 조정.
    // 원본 모델은 약 16m 짜리(얼음/모래 보스는 4m). 그대로 쓰면 화면을 다 잡아먹어서 줄인다.
    const float BossScale = 0.45f;      // 16.3m * 0.45 = 약 7.3m
    const float ModelHeight = 16.276306f;   // 스케일 적용 전 모델 실측 높이
    const float ModelRadius = 3.5f;

    [MenuItem("Tools/Enemy/Build Fire Boss (prefab + SO)")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog(
            "화염보스 생성",
            "고정형 아틸러리 보스 풀세트를 만든다:\n" +
            "  - 멀티레이어 애니 컨트롤러(화염보스_Boss.controller)\n" +
            $"  - 보스 프리팹({PrefabPath}, 자체 파티클 유지, ActionCTRL 제거)\n" +
            $"  - 데이터 SO({SoPath}, 없으면 기본값/있으면 보존)\n" +
            "  - 파이어볼 프리팹 조립 + VFX(전조) 불 배선\n" +
            "  - 주황색 텔레그래프(와이번 것 복제)\n\n계속?",
            "생성", "취소")) return;

        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelPrefab == null) { Debug.LogError($"[FireBoss] 모델 프리팹 없음: {ModelPath}"); return; }

        // 0) 멀티레이어 애니 컨트롤러 보장
        FireBossAnimatorBuilder.EnsureBuilt();
        var ctrlAsset = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FireBossAnimatorBuilder.CtrlPath);
        if (ctrlAsset == null) Debug.LogWarning("[FireBoss] 애니 컨트롤러 로드 실패(애니 안 나올 수 있음)");

        // 1) 데이터 SO
        var so = AssetDatabase.LoadAssetAtPath<MeleeEnemyData>(SoPath);
        if (so == null)
        {
            EnsureFolder(SoFolder);
            so = ScriptableObject.CreateInstance<MeleeEnemyData>();
            so.enemyName = "화염정령"; so.enemyId = "fire_boss";
            so.maxHP = 2000f;   // 찐최종보스 = 최강(종욱 지시). 이동 못하니 카이팅 안티어트리션(리쉬 리셋)과 광역으로 압박
            so.moveSpeed = 0f; so.acceleration = 0f; so.angularSpeed = 240f; so.stoppingDistance = 0f;
            so.visionRange = 35f; so.visionAngle = 360f;
            so.attackDamage = 60f; so.attackRange = 4f; so.attackApproachRatio = 0.9f; so.attackCooldown = 3f;
            so.hitDelay = 0.5f; so.animLength = 1.5f;
            so.attackTrigger = "Attack"; so.hitTrigger = "Hit"; so.detectTrigger = "Detect"; so.dieTrigger = "Die";
            so.targetLostMemory = 3f;
            so.deathAnimDuration = 8f;   // body_disappear = 8s
            so.detectStunDuration = 0f;
            AssetDatabase.CreateAsset(so, SoPath);
            AssetDatabase.SaveAssets();
        }

        // 2) 주황 텔레그래프 + 파이어볼 프리팹
        var telegraph = EnsureFireTelegraph();
        var fireball = EnsureFireballPrefab();

        // 3) 보스 프리팹 조립
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Enemy_Fire_Boss";

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0) enemyLayer = 6;
        go.layer = enemyLayer;

        // 데모 스크립트(ActionCTRL) 제거, uv_move(소용돌이 UV)는 유지. 하드참조 회피 위해 이름으로.
        foreach (var mb in go.GetComponents<MonoBehaviour>())
            if (mb != null && mb.GetType().Name == "ActionCTRL") Object.DestroyImmediate(mb);

        // 등장 데모 페인트 파티클(초록/주황 얼룩)을 불/용암 톤으로 재색. 화염보스에 초록 페인트는 오설정.
        RecolorPaintParticles(go);
        // 열굴절(heat refraction) 파티클 렌더 끔 = 화면 전체 일렁임 제거(값 조절보다 확실).
        DisableDistortionParticles(go);

        var animator = go.GetComponentInChildren<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        if (ctrlAsset != null) animator.runtimeAnimatorController = ctrlAsset;
        animator.applyRootMotion = false;

        // 콜라이더 핏(피격용). 고정형이라 NavMeshAgent 없음.
        // ★smr.bounds 로 재지 마라. 에디터(비재생)에서 스킨드메시 bounds 는 갱신이 안 돼서
        //   중심이 엉뚱한 곳으로 잡힌다(실제로 center.y 가 -9.4 로 나와 히트박스가 통째로 땅속에 박혔다).
        //   원점=발밑 기준으로 상수로 못박는 게 확실하다.
        var col = GetOrAdd<CapsuleCollider>(go);
        col.height = ModelHeight;
        col.radius = ModelRadius;
        col.center = new Vector3(0f, ModelHeight * 0.5f, 0f);   // 발밑에서 머리까지
        col.direction = 1;                                       // Y축
        col.isTrigger = false;

        // 모델에 딸려온 잡 콜라이더 제거(땅속에 박힌 BoxCollider 가 있었다). 피격 판정은 위 캡슐 하나로 통일.
        foreach (var stray in go.GetComponentsInChildren<Collider>(true))
            if (stray != col) Object.DestroyImmediate(stray, true);

        var rb = GetOrAdd<Rigidbody>(go);
        rb.isKinematic = true; rb.useGravity = false;

        var audio = GetOrAdd<AudioSource>(go);
        audio.playOnAwake = false; audio.spatialBlend = 1f;

        var health = GetOrAdd<EnemyHealth>(go);
        health.maxHP = so.maxHP; health.currentHP = so.maxHP;

        GetOrAdd<EnemyFeedback>(go);

        // 4) 컨트롤러 + VFX 배선
        var ctrl = GetOrAdd<FireBossController>(go);
        var sobj = new SerializedObject(ctrl);
        SetRef(sobj, "data", so);
        SetRef(sobj, "fireballPrefab",  fireball);
        SetRef(sobj, "eruptionVfx",     J("VFX_Explosion_Floor"));      // 지면 배러지 폭발
        SetRef(sobj, "meteorVfx",       J("VFX_Fireball_Projectile_Static"));  // 유성 낙하선(코드가 아래로 이동)
        SetRef(sobj, "meteorImpactVfx", J("VFX_Explosion_Floor"));      // 유성 착탄
        SetRef(sobj, "meleeImpactVfx",  J("VFX_Explosion_Omni"));       // 근접 타격 버스트
        SetRef(sobj, "forwardVfx",      J("Chain explosions forward")); // 전방 연쇄(전진형)
        SetRef(sobj, "breathVfx",       J("VFX_Fire"));                 // 브레스 보강 원뿔(선택)
        SetRef(sobj, "lavaVfx",         J("VFX_Fire"));                 // P3 잔불 장판
        SetRef(sobj, "telegraphVfx",    telegraph);
        // 발사 원점(입/머리 본). 못 찾으면 fireOffset 폴백.
        var head = FindBone(go, "Head", "head", "Mouth", "Spine02", "Spine2", "Bip001 Head");
        if (head != null) SetRef(sobj, "chargeAnchor", head);
        else Debug.LogWarning("[FireBoss] 머리/입 본 못 찾음 -> 파이어볼/브레스가 fireOffset 위치에서 발사(무해)");
        sobj.ApplyModifiedProperties();

        WireRewards(go);

        // 5) 스케일(기본 native 1.0)
        go.transform.localScale = Vector3.one * BossScale;

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out bool ok);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (saved != null) { Selection.activeObject = saved; EditorGUIUtility.PingObject(saved); }

        Debug.Log(
            $"[FireBoss] 생성 {(ok ? "완료" : "실패")}.\n" +
            $"  보스: {PrefabPath} (HP {so.maxHP}, scale {BossScale} native)\n" +
            $"  SO: {SoPath}\n  컨트롤러: {FireBossAnimatorBuilder.CtrlPath}\n  파이어볼: {FireballPath}\n\n" +
            "정체성: 고정형 아틸러리(이동 없음). 시그니처=유성비.\n" +
            "패턴: P1 파이어볼+배러지+근접+전방분출 / P2 +유성비 / P3 +궁극브레스+잔불\n\n" +
            "다음(종욱):\n" +
            "1. 애니 확인: 4레이어 / 무마스크 / body+얼굴+소용돌이 동시재생. 어긋나면 Tools>Enemy>Build Fire Boss Animator 재실행.\n" +
            "2. body.mat / firevortex.mat 핑크(마젠타) 여부 확인 -> 핑크면 URP/Lit 변환(조사 데이터 상충 항목).\n" +
            "3. body_attack_001~006 재생해보고 패턴 배정(배러지/파이어볼/유성/근접/전방/브레스) 맞는지 검증.\n" +
            "4. 결계 밖 NavMesh 베이크된 곳 배치. 스케일이 작으면 localScale 조정.\n" +
            "5. fireOffset/chargeAnchor(입 위치) 프리팹 인스펙터서 조정(파이어볼/브레스 발사 위치).\n" +
            "6. 밸런스는 EnemyData_FireBoss.asset / 패턴 수치는 컨트롤러 인스펙터.");
    }

    // 파이어볼 프리팹 = VFX_Fireball_Projectile_Static(비주얼) + WyvernFireball(이동/데미지) + 착탄 VFX.
    private static GameObject EnsureFireballPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(FireballPath);
        if (existing != null) return existing;

        var vis = J("VFX_Fireball_Projectile_Static");
        if (vis == null) { Debug.LogWarning("[FireBoss] 파이어볼 비주얼 없음 -> 파이어볼 미조립"); return null; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(vis);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "FireBoss_Fireball";

        var fb = go.AddComponent<WyvernFireball>();
        var so = new SerializedObject(fb);
        SetRef(so, "explodeVfx", J("VFX_Fireball_Impact"));
        so.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(go, FireballPath);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<GameObject>(FireballPath);
    }

    // 와이번 텔레그래프 복제 -> 주황색(형태 유지, 색만).
    private static GameObject EnsureFireTelegraph()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphFire);
        if (existing != null) return existing;

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphSrc);
        if (src == null) { Debug.LogWarning($"[FireBoss] 와이번 텔레그래프 없음(미연결): {TelegraphSrc}"); return null; }

        if (!AssetDatabase.CopyAsset(TelegraphSrc, TelegraphFire))
        { Debug.LogWarning("[FireBoss] 텔레그래프 복제 실패"); return null; }
        AssetDatabase.Refresh();

        var root = PrefabUtility.LoadPrefabContents(TelegraphFire);
        if (root != null)
        {
            var fireColor = new Color(1f, 0.45f, 0.15f, 1f);   // 주황
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = fireColor;
            }
            PrefabUtility.SaveAsPrefabAsset(root, TelegraphFire);
            PrefabUtility.UnloadPrefabContents(root);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphFire);
    }

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
        else Debug.LogWarning($"[FireBoss] BaseEnemy 못 찾음(드롭/흡수 미연결): {BaseEnemyPath}");

        var drop = GetOrAdd<EnemyDropOnDeath>(go);
        var ds = new SerializedObject(drop);
        var sid = ds.FindProperty("sourceId"); if (sid != null) sid.stringValue = "fire_boss";
        SetRef(ds, "boxPrefab", boxPrefab);
        ds.ApplyModifiedProperties();

        var absorb = GetOrAdd<EnemyAbsorbOnDeath>(go);
        var abs = new SerializedObject(absorb);
        SetRef(abs, "absorbVfxPrefab", absorbVfx);
        var bonus = abs.FindProperty("bonusHealPercent"); if (bonus != null) bonus.floatValue = 0.5f;
        abs.ApplyModifiedProperties();
    }

    // 등장 이펙트의 데모 페인트 파티클(흰 스플랫 텍스처 + 초록/주황 색)을 불/용암 톤으로 재색.
    // 텍스처가 흰색이라 색은 startColor/colorOverLifetime 이 결정 -> 둘 다 용암색으로 덮는다.
    private static void RecolorPaintParticles(GameObject go)
    {
        var lava = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.1f), new Color(1f, 0.8f, 0.25f));
        int n = 0;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r == null || r.sharedMaterial == null) continue;
            if (!r.sharedMaterial.name.ToLower().Contains("paint")) continue;

            var main = ps.main;
            main.startColor = lava;

            var col = ps.colorOverLifetime;
            if (col.enabled)
            {
                var grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0f), new GradientColorKey(new Color(0.85f, 0.18f, 0.05f), 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(grad);
            }
            n++;
        }
        if (n > 0) Debug.Log($"[FireBoss] 데모 페인트 파티클 {n}개 불/용암 톤으로 재색");
    }

    // 열굴절 파티클(HeatMat/RefrectMat/firevortex_refrect)의 렌더러를 끈다. 렌더 안 하면 왜곡 자체가 없다.
    // = 재질 강도값 조절(리임포트 타이밍 의존, Play 중 반영 안 됨)보다 확실한 제거.
    private static void DisableDistortionParticles(GameObject go)
    {
        int n = 0;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r == null || r.sharedMaterial == null) continue;
            string mn = r.sharedMaterial.name.ToLower();
            if (mn.Contains("refr") || mn.Contains("heat") || mn.Contains("distort"))
            {
                r.enabled = false;
                n++;
            }
        }
        if (n > 0) Debug.Log($"[FireBoss] 열굴절 파티클 {n}개 렌더 비활성(일렁임 제거)");
    }

    private static GameObject J(string name)
    {
        foreach (var f in VfxFolders)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{f}/{name}.prefab");
            if (p != null) return p;
        }
        Debug.LogWarning($"[FireBoss] VFX 없음: {name}.prefab (VFX(전조) 하위 검색 실패)");
        return null;
    }

    private static void SetRef(SerializedObject sobj, string prop, Object value)
    {
        var p = sobj.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[FireBoss] 직렬화 필드 없음: {prop}");
    }

    private static Transform FindBone(GameObject root, params string[] names)
    {
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var n in names)
            foreach (var t in all)
                if (t.name == n) return t;
        foreach (var t in all)
        {
            string lower = t.name.ToLower();
            if (lower.Contains("head") || lower.Contains("mouth")) return t;
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
