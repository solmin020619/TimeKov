using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 10종 적별 MeleeEnemyData SO 자동 생성 + Enemy_*.prefab의 EnemyBrain.Data 슬롯에 자동 연결.
/// 기존엔 BaseEnemy.prefab의 Brain.Data가 단일 SO(Skeltonzombie.asset, enemyName=tutorial_enemy)를
/// 가리켜서 10종 prefab 다 같은 이름으로 표시되던 문제 해결.
///
/// SO의 HP/공격력 등 수치는 기본값으로 두므로 생성 후 인스펙터에서 적별 튜닝 필요.
/// 메뉴: Tools > Enemy > Generate Per-Enemy Data SO (10)
/// </summary>
public static class EnemyDataSOBuilder
{
    const string DataFolder = "Assets/05.Prefabs/Enemy/SO";
    const string PrefabFolder = "Assets/05.Prefabs/Enemy";

    // (shortName, displayName, enemyId)
    //   displayName → SO.enemyName (HP 바 표시 이름, 한국어)
    //   enemyId     → SO.enemyId (퀘스트/드롭/시스템 매칭 ID, 영어 snake_case)
    // Undead의 enemyId="tutorial_enemy"는 obj_kill_tutorial_enemy.asset의 enemyId와 정확히 일치해야 함.
    static readonly (string shortName, string displayName, string enemyId)[] Enemies =
    {
        ("EvilWatcher",     "사악한 감시자",   "evil_watcher"),
        ("SkeletonKnight",  "해골 기사",       "skeleton_knight"),
        ("Undead",          "언데드",          "tutorial_enemy"),  // ← Q8 매칭 (TutorialAssetBuilder의 enemyId와 일치)
        ("DarknessSpider",  "어둠 거미",       "darkness_spider"),
        ("GiantRat",        "거대 쥐",         "giant_rat"),
        ("FantasyWolf",     "늑대",            "fantasy_wolf"),
        ("OakTreeEnt",      "떡갈나무 엔트",   "oak_tree_ent"),
        ("Werewolf",        "늑대인간",        "werewolf"),
        ("Mummy",           "미라",            "mummy"),
        ("Wyvern",          "와이번",          "wyvern"),
    };

    [MenuItem("Tools/Enemy/Generate Per-Enemy Data SO (10)")]
    public static void GenerateAll()
    {
        bool ok = EditorUtility.DisplayDialog(
            "적별 Data SO 생성",
            $"10개 MeleeEnemyData SO 생성:\n" +
            $"  - 경로: {DataFolder}/EnemyData_*.asset\n" +
            $"  - 각 Enemy_*.prefab의 EnemyBrain.Data 슬롯에 자동 연결\n\n" +
            $"동작:\n" +
            $"  - 같은 이름 SO 존재 시 enemyName만 갱신 (수치는 보존)\n" +
            $"  - prefab Override로 Data 슬롯만 갈아끼움 (BaseEnemy 영향 X)\n\n계속?",
            "생성", "취소");
        if (!ok) return;

        EnsureFolder(DataFolder);

        int created = 0;
        int updated = 0;
        var summary = new List<string>();

        foreach (var (shortName, displayName, enemyId) in Enemies)
        {
            // 1. SO 생성/갱신
            string soPath = $"{DataFolder}/EnemyData_{shortName}.asset";
            var so = AssetDatabase.LoadAssetAtPath<MeleeEnemyData>(soPath);
            bool isNew = (so == null);
            if (isNew)
            {
                so = ScriptableObject.CreateInstance<MeleeEnemyData>();
                so.enemyName = displayName;
                so.enemyId = enemyId;
                AssetDatabase.CreateAsset(so, soPath);
                created++;
            }
            else
            {
                // 기존 SO는 enemyName/enemyId만 덮어쓰기 (사용자가 수치 조정한 거 보존)
                so.enemyName = displayName;
                so.enemyId = enemyId;
                EditorUtility.SetDirty(so);
                updated++;
            }

            // 2. Enemy_*.prefab Brain.Data 슬롯 연결
            string prefabPath = $"{PrefabFolder}/Enemy_{shortName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                summary.Add($"  EnemyData_{shortName}.asset ← (prefab 없음, 연결 SKIP)");
                continue;
            }

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            var brain = contents.GetComponent<EnemyBrain>();
            if (brain != null)
            {
                var sobj = new SerializedObject(brain);
                var dataProp = sobj.FindProperty("data");
                if (dataProp != null)
                {
                    dataProp.objectReferenceValue = so;
                    sobj.ApplyModifiedProperties();
                }
            }
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            summary.Add($"  EnemyData_{shortName}.asset name=\"{displayName}\" id=\"{enemyId}\" → Enemy_{shortName}.prefab");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[EnemyDataSOBuilder] 신규 {created}개, 갱신 {updated}개.\n" +
            string.Join("\n", summary) + "\n\n" +
            "다음 단계:\n" +
            "1. 각 EnemyData_*.asset 더블클릭 → HP/공격력/속도/Detect Stun Duration 등 적별 튜닝\n" +
            "2. 튜토리얼 적 = EnemyData_Undead (enemyName=\"언데드\", enemyId=\"tutorial_enemy\")\n" +
            "   - obj_kill_tutorial_enemy.asset의 enemyId가 \"tutorial_enemy\"인지 확인 (TutorialAssetBuilder 디폴트)\n" +
            "   - Enemy_Undead.prefab의 EnemyDropOnDeath.sourceId도 \"tutorial_enemy\"로 박혀있는지 확인\n" +
            "3. 시트(DropTable)에 sourceId=\"tutorial_enemy\" 행 2개 추가 필수 (1101 x2, 1102 x1)");
    }

    static void EnsureFolder(string fullPath)
    {
        if (AssetDatabase.IsValidFolder(fullPath)) return;
        var parts = fullPath.Split('/');
        string curr = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{curr}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(curr, parts[i]);
            curr = next;
        }
    }
}
