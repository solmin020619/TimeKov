using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// 기존 적 프리팹을 '엘리트'(덩치 키우고 HP/공격력 올린 별개 몬스터) 변형으로 일괄 생성.
//   베이스 Enemy_X.prefab + EnemyData_X.asset
//     -> Enemy_X_Elite.prefab + EnemyData_X_Elite.asset (+ 도감 엔트리)
//
// 처리:
//   - SO: HP/공격력/시야에 배율 적용, enemyName=한글 칭호("늑대 왕" 등), enemyId=base+"_elite"
//   - 프리팹: 루트 스케일 + NavMeshAgent radius/height 키움, EnemyBrain.data=엘리트SO,
//             EnemyDropOnDeath.sourceId=base+"_Elite" (= 드롭/도감이 별개 몬스터로 잡힘)
//   - 도감: CodexPreviewConfig에 엘리트 엔트리 추가(베이스 흉상 프레이밍 복사)
//
// 멱등: 기존 에셋은 guid 유지하며 덮어씀(스폰 배치 안 깨짐). 단 재실행하면 스탯/스케일이 base x 배율로 리셋됨.
// 배율/칭호/대상은 아래 상수에서 조절. 드롭 아이템은 시트(DropTable) sourceId 행 추가 별도 필요.
// 메뉴: Tools > Enemy > Build Elite Variants
public static class EliteVariantBuilder
{
    const string PrefabFolder = "Assets/05.Prefabs/Enemy";
    const string SoFolder = "Assets/06.ScriptableObjects/Enemy";

    // ===== 조절용 배율 =====
    const float HpMul = 2.5f;       // 체력
    const float DamageMul = 1.5f;   // 공격력
    const float VisionMul = 1.15f;  // 시야 거리
    const float ScaleMul = 1.4f;    // 덩치(루트 스케일 + 네비)
    // 속도는 그대로(요청대로). 필요하면 moveSpeed 줄에 배율 추가.

    // (베이스 shortName, 엘리트 한글 칭호). 오크트리/늑대인간(Werewolf) 제외.
    // 정확히 5종만 쓰려면 안 쓸 한 줄을 지우면 됨.
    // ★'왕/군' 글자가 폴백 폰트(Pretendard SDF) 아틀라스에 있어야 □ 안 뜸 -> FontGlyphPatcher로 미리 구울 것.
    static readonly (string shortName, string eliteName)[] Elites =
    {
        ("EvilWatcher",    "아귀 군주"),
        ("FantasyWolf",    "늑대 왕"),
        ("DarknessSpider", "거미 여왕"),
        ("Undead",         "언데드 군주"),
        ("GiantRat",       "쥐 왕"),
        ("SkeletonKnight", "해골 장군"),
    };

    [MenuItem("Tools/Enemy/Build Elite Variants")]
    public static void Build()
    {
        bool ok = EditorUtility.DisplayDialog("엘리트 변형 생성",
            $"{Elites.Length}종 엘리트(덩치+스탯 키운 별개 몬스터) 생성/덮어쓰기.\n\n" +
            $"HP x{HpMul} / 공격력 x{DamageMul} / 시야 x{VisionMul} / 크기 x{ScaleMul}\n" +
            "Enemy_X_Elite.prefab + EnemyData_X_Elite.asset + 도감 엔트리.\n\n" +
            "(재실행 시 스탯/스케일은 base x 배율로 리셋됨)\n계속?", "생성", "취소");
        if (!ok) return;

        var cfg = LoadCodexConfig();
        var summary = new List<string>();
        var dropRows = new List<string>();
        int made = 0;

        foreach (var (shortName, eliteName) in Elites)
        {
            string basePrefabPath = $"{PrefabFolder}/Enemy_{shortName}.prefab";
            string baseSoPath = $"{SoFolder}/EnemyData_{shortName}.asset";
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePrefabPath);
            var baseSo = AssetDatabase.LoadAssetAtPath<MeleeEnemyData>(baseSoPath);
            if (basePrefab == null || baseSo == null)
            {
                summary.Add($"  {shortName} -> SKIP (베이스 없음: prefab={(basePrefab != null)}, so={(baseSo != null)})");
                continue;
            }

            // 1) 엘리트 SO (guid 유지하며 덮어씀)
            string eliteSoPath = $"{SoFolder}/EnemyData_{shortName}_Elite.asset";
            var eliteSo = AssetDatabase.LoadAssetAtPath<MeleeEnemyData>(eliteSoPath);
            if (eliteSo == null)
            {
                eliteSo = ScriptableObject.CreateInstance<MeleeEnemyData>();
                AssetDatabase.CreateAsset(eliteSo, eliteSoPath);
            }
            EditorUtility.CopySerialized(baseSo, eliteSo);   // base 값 복사
            eliteSo.maxHP = Mathf.Round(baseSo.maxHP * HpMul);
            eliteSo.attackDamage = Mathf.Round(baseSo.attackDamage * DamageMul);
            eliteSo.visionRange = baseSo.visionRange * VisionMul;
            eliteSo.enemyName = eliteName;
            eliteSo.enemyId = (string.IsNullOrEmpty(baseSo.enemyId) ? shortName.ToLower() : baseSo.enemyId) + "_elite";
            EditorUtility.SetDirty(eliteSo);

            // 2) 엘리트 프리팹 (base에서 매번 새로 떠서 일관성 보장, guid는 덮어쓰며 유지)
            string elitePrefabPath = $"{PrefabFolder}/Enemy_{shortName}_Elite.prefab";
            var contents = PrefabUtility.LoadPrefabContents(basePrefabPath);

            contents.transform.localScale *= ScaleMul;

            var agent = contents.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.radius *= ScaleMul;
                agent.height *= ScaleMul;
                agent.baseOffset *= ScaleMul;
            }

            var brain = contents.GetComponent<EnemyBrain>();
            if (brain != null)
            {
                var so = new SerializedObject(brain);
                var dataProp = so.FindProperty("data");
                if (dataProp != null) { dataProp.objectReferenceValue = eliteSo; so.ApplyModifiedProperties(); }
            }

            string eliteSourceId = "";
            var drop = contents.GetComponent<EnemyDropOnDeath>();
            if (drop != null)
            {
                var so = new SerializedObject(drop);
                var idProp = so.FindProperty("sourceId");
                if (idProp != null)
                {
                    string baseId = string.IsNullOrEmpty(idProp.stringValue) ? $"MeleeBot_{shortName}" : idProp.stringValue;
                    // 이미 _Elite 면 중복 안 붙임(멱등)
                    eliteSourceId = baseId.EndsWith("_Elite") ? baseId : baseId + "_Elite";
                    idProp.stringValue = eliteSourceId;
                    so.ApplyModifiedProperties();
                }
            }

            var elitePrefab = PrefabUtility.SaveAsPrefabAsset(contents, elitePrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            // 3) 도감 엔트리
            if (cfg != null && elitePrefab != null)
                AddOrUpdateCodexEntry(cfg, basePrefab, elitePrefab, eliteName);

            made++;
            summary.Add($"  {shortName} -> Enemy_{shortName}_Elite ('{eliteName}'  HP {eliteSo.maxHP} / 공격 {eliteSo.attackDamage} / sourceId={eliteSourceId})");
            if (!string.IsNullOrEmpty(eliteSourceId))
                dropRows.Add($"    sourceType=Monster | sourceId={eliteSourceId} | dropTier=? | dropWeight=? | pickCount=? | itemId=?(엘리트 전용템)");
        }

        if (cfg != null) EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[EliteVariantBuilder] {made}/{Elites.Length}종 엘리트 생성 완료.\n" +
            string.Join("\n", summary) + "\n\n" +
            "## 종욱 할 것\n" +
            "1) 스폰존(EnemySpawnPoint)의 enemyPrefabs에 Enemy_X_Elite 배치\n" +
            "2) 드롭 시트(DropTable)에 아래 sourceId 행 추가(엘리트 전용 아이템):\n" +
            string.Join("\n", dropRows) + "\n" +
            "3) 도감: 엔트리 자동 추가됨. 흉상 프레이밍 어긋나면 CodexPreviewConfig에서 미세조정\n" +
            "4) 스탯/크기 더 손보려면 위 상수 바꿔 재실행 OR 생성된 SO/프리팹 직접 튜닝");
    }

    static CodexPreviewConfig LoadCodexConfig()
    {
        var found = AssetDatabase.FindAssets("t:CodexPreviewConfig");
        return found.Length > 0
            ? AssetDatabase.LoadAssetAtPath<CodexPreviewConfig>(AssetDatabase.GUIDToAssetPath(found[0]))
            : null;
    }

    // 엘리트 도감 엔트리 추가/갱신. 베이스 엔트리의 흉상 프레이밍을 복사해 와 비슷하게 잡아준다.
    static void AddOrUpdateCodexEntry(CodexPreviewConfig cfg, GameObject basePrefab, GameObject elitePrefab, string displayName)
    {
        var baseEntry = cfg.monsters.Find(m => m != null && m.prefab == basePrefab);
        var entry = cfg.monsters.Find(m => m != null && m.prefab == elitePrefab);
        if (entry == null)
        {
            entry = new CodexPreviewConfig.MonsterEntry();
            cfg.monsters.Add(entry);
        }
        entry.prefab = elitePrefab;
        entry.displayName = displayName;
        entry.state = CodexPreviewConfig.RevealState.Revealed;
        if (baseEntry != null)
        {
            entry.yaw = baseEntry.yaw;
            entry.pitch = baseEntry.pitch;
            entry.aimHeight = baseEntry.aimHeight;
            entry.fillScale = baseEntry.fillScale * 0.92f;   // 덩치 커졌으니 살짝 줌아웃
            entry.xOffset = baseEntry.xOffset;
        }
    }
}
