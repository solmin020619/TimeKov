using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// BaseEnemy.prefab 기준 10마리 Variant prefab 일괄 생성.
/// - SK_*.fbx 자식 추가 (모델)
/// - Animator Controller 슬롯에 적별 Override 연결
/// - SkinnedMesh 머티리얼 fbx에서 External로 추출 (Extract Materials 자동화)
/// - CapsuleCollider 자동 fit
/// 메뉴: Tools > Enemy > Build All Enemy Variants From BaseEnemy
/// </summary>
public static class EnemyVariantBuilder
{
    const string BasePrefabPath = "Assets/05.Prefabs/Enemy/BaseEnemy.prefab";
    const string OutFolder = "Assets/05.Prefabs/Enemy";
    const float ColliderWidthMultiplier = 0.35f;

    // (folder, shortName, modelFileWithoutExt, overrideFileWithoutExt)
    static readonly (string folder, string shortName, string modelName, string overrideName)[] Enemies =
    {
        ("01.Evil Watcher",      "EvilWatcher",     "SK_EvilWatcher",     "Evil Watcher_Override"),
        ("02.Skeleton Knight",   "SkeletonKnight",  "SK_SkeletonKnight",  "Skeleton Knight_Override"),
        ("03.Undead",            "Undead",          "SK_Undead",          "Undead_Override"),
        ("04.Darkness Spider",   "DarknessSpider",  "SK_DarknessSpider",  "Darkness Spider_Override"),
        ("05.Giant Rat",         "GiantRat",        "SK_GiantRat",        "Giant Rat_Override"),
        ("06.Fantasy Wolf",      "FantasyWolf",     "SK_FantasyWolf",     "Fantasy Wolf_Override"),
        ("07.Oak Tree Ent",      "OakTreeEnt",      "SK_OakTreeEnt",      "Oak Tree Ent_Override"),
        ("08.Werewolf",          "Werewolf",        "SK_Werewolf",        "Werewolf_Override"),
        ("09.Mummy",             "Mummy",           "SK_Mummy",           "Mummy_Override"),
        ("10.Wyvern",            "Wyvern",          "SK_Wyvern",          "Wyvern_Override"),
    };

    [MenuItem("Tools/Enemy/Build All Enemy Variants From BaseEnemy")]
    public static void BuildAll()
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        if (basePrefab == null)
        {
            Debug.LogError($"[VariantBuilder] BaseEnemy.prefab 못 찾음: {BasePrefabPath}");
            return;
        }

        var summary = new List<string>();
        int created = 0;

        foreach (var (folder, shortName, modelName, overrideName) in Enemies)
        {
            string enemyFolder = $"Assets/03.Model/Enemy/{folder}";

            // 1. SK fbx 찾기 (대소문자 무관)
            string modelPath = FindModelFbx(enemyFolder, modelName);
            if (modelPath == null)
            {
                summary.Add($"  {folder} → SKIP (SK_{modelName} fbx 못 찾음)");
                continue;
            }

            // 2. Material External + Search (자동 매핑 시도)
            ExtractMaterialsIfNeeded(modelPath);

            // 3. Override Controller 찾기
            var overridePath = $"{enemyFolder}/{overrideName}.overrideController";
            var overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);

            // 4. 출력 path
            string variantPath = $"{OutFolder}/Enemy_{shortName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(variantPath) != null)
                AssetDatabase.DeleteAsset(variantPath);

            // 5. Base prefab 인스턴스화 (Variant base)
            var variantRoot = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);

            // 6. SK 모델 자식 추가
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                Object.DestroyImmediate(variantRoot);
                summary.Add($"  {folder} → SKIP (모델 로드 실패)");
                continue;
            }
            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, variantRoot.transform);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;

            // 7. Animator (모델 자식에 자동 생성). Controller 슬롯에 Override 연결
            var animator = modelInstance.GetComponent<Animator>();
            if (animator == null) animator = modelInstance.AddComponent<Animator>();
            if (overrideController != null)
                animator.runtimeAnimatorController = overrideController;

            // 8. Auto-fit CapsuleCollider on root
            AutoFitCollider(variantRoot);

            // 9. EnemyDropOnDeath.sourceId 적별 매핑 (이름 일치하면)
            var dropOnDeath = variantRoot.GetComponent<EnemyDropOnDeath>();
            if (dropOnDeath != null)
            {
                var so = new SerializedObject(dropOnDeath);
                var sourceIdProp = so.FindProperty("sourceId");
                if (sourceIdProp != null)
                {
                    sourceIdProp.stringValue = $"MeleeBot_{shortName}";
                    so.ApplyModifiedProperties();
                }
            }

            // 10. 저장
            PrefabUtility.SaveAsPrefabAsset(variantRoot, variantPath);
            Object.DestroyImmediate(variantRoot);

            created++;
            string ctrlInfo = overrideController != null ? $"Override={overrideName}" : "Override 없음";
            summary.Add($"  Enemy_{shortName}.prefab ← SK={modelName}, {ctrlInfo}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[VariantBuilder] Variant {created}/10개 생성 완료.\n" +
            string.Join("\n", summary) + "\n\n" +
            "다음 단계:\n" +
            "1. 각 Enemy_*.prefab 더블클릭해서 Scene 뷰에 모델 + 콜라이더 위치 확인\n" +
            "2. 콜라이더 안 맞으면 인스펙터에서 Radius/Height 미세조정\n" +
            "3. 머티리얼 분홍색이면 fbx Materials 탭에서 Extract Materials 수동 실행\n" +
            "4. NavMeshAgent의 Radius/Height도 콜라이더에 맞춰 조정\n" +
            "5. EnemyDropOnDeath.sourceId가 DropTable의 ID와 매칭되는지 확인");
    }

    // ---- helpers ----

    static string FindModelFbx(string enemyFolder, string modelName)
    {
        // FBX Files 또는 FBX files (대소문자 무관)
        string[] candidateSubs = { "/FBX Files", "/FBX files", "/FBX" };
        foreach (var sub in candidateSubs)
        {
            string subPath = enemyFolder + sub;
            if (!AssetDatabase.IsValidFolder(subPath)) continue;
            // .fbx + .FBX
            string[] exts = { ".fbx", ".FBX" };
            foreach (var ext in exts)
            {
                string full = $"{subPath}/{modelName}{ext}";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(full) != null)
                    return full;
            }
        }
        return null;
    }

    static void ExtractMaterialsIfNeeded(string fbxPath)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) return;

        bool changed = false;
        if (importer.materialLocation == ModelImporterMaterialLocation.InPrefab)
        {
            importer.materialLocation = ModelImporterMaterialLocation.External;
            changed = true;
        }
        if (importer.materialImportMode == ModelImporterMaterialImportMode.None)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
            // 같은 폴더 + RecursiveUp으로 머티리얼 자동 검색/매핑
            importer.SearchAndRemapMaterials(
                ModelImporterMaterialName.BasedOnMaterialName,
                ModelImporterMaterialSearch.RecursiveUp);
            importer.SaveAndReimport();
        }
    }

    static void AutoFitCollider(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            worldBounds.Encapsulate(renderers[i].bounds);

        Vector3 localCenter = root.transform.InverseTransformPoint(worldBounds.center);
        Vector3 worldSize = worldBounds.size;
        Vector3 localScale = root.transform.lossyScale;
        Vector3 localSize = new Vector3(
            worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(localScale.x)),
            worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(localScale.y)),
            worldSize.z / Mathf.Max(0.0001f, Mathf.Abs(localScale.z))
        );

        var capsule = root.GetComponent<CapsuleCollider>();
        if (capsule == null) capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = localCenter;
        capsule.height = Mathf.Max(0.2f, localSize.y);
        float rawWidth = Mathf.Min(localSize.x, localSize.z);
        float radius = Mathf.Max(0.1f, rawWidth * 0.5f * ColliderWidthMultiplier * 2f);
        radius = Mathf.Max(radius, localSize.y * 0.15f);
        capsule.radius = radius;
        capsule.direction = 1;
    }
}
