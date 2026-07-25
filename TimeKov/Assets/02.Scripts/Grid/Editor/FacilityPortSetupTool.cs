using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 옛날 설비 프리팹(포트/콜라이더 세팅 완료본)을 템플릿으로 읽어,
/// 새 설비 프리팹(05.Prefabs/Grid)에 footprint별로 BuildPort 자식 + 바디 BoxCollider 를 복제한다.
///
/// 복제 대상: BuildPort 가진 자식(포트) + Renderer 없는 Collider 자식(바디 콜라이더)
/// 복제 제외: 아트 모델(Renderer 있는 자식), 머신 스크립트(ProcessingMachine 등 = 재원 담당, 루트 컴포넌트라 애초에 자식 아님)
///
/// 재실행 안전: 이전에 이 툴이 넣은 포트(BuildPort)와 바디("[Build] BodyCollider")만 지우고 다시 복제.
/// 아트가 원래 갖고 있던 콜라이더/오브젝트는 건드리지 않음.
///
/// 메뉴: Tools > Grid > Setup Facility Ports From Old Templates
/// </summary>
public static class FacilityPortSetupTool
{
    const string BuildPortLayerName = "BuildPort";
    const string BodyColliderName   = "[Build] BodyCollider";

    // footprint 템플릿 (포트/콜라이더가 이미 세팅된 옛날 프리팹)
    const string Template3x3 = "Assets/02.Scripts/Grid/Prefab/옛날 생체 추출기.prefab";
    const string Template5x5 = "Assets/02.Scripts/Grid/Prefab/옛날 화학 정제기.prefab";

    // (대상 프리팹, 사용할 템플릿)
    static readonly (string target, string template)[] Targets =
    {
        ("Assets/05.Prefabs/Grid/3X3/생체 추출기.prefab",  Template3x3),
        ("Assets/05.Prefabs/Grid/3X3/에너지 변환기.prefab", Template3x3),
        ("Assets/05.Prefabs/Grid/3X3/저장고.prefab",       Template3x3),
        ("Assets/05.Prefabs/Grid/5X5/생체 배양기.prefab",  Template5x5),
        ("Assets/05.Prefabs/Grid/5X5/생체 분리기.prefab",  Template5x5),
        ("Assets/05.Prefabs/Grid/5X5/용해로.prefab",       Template5x5),
        ("Assets/05.Prefabs/Grid/5X5/코어 합성기.prefab",  Template5x5),
        ("Assets/05.Prefabs/Grid/5X5/화학 정제기.prefab",  Template5x5),
    };

    [MenuItem("Tools/Grid/Setup Facility Ports From Old Templates")]
    public static void SetupAll()
    {
        int layer = LayerMask.NameToLayer(BuildPortLayerName);
        if (layer < 0)
        {
            Debug.LogError($"[FacilityPortSetup] '{BuildPortLayerName}' 레이어가 없음. Project Settings > Tags and Layers 확인.");
            return;
        }

        var summary = new List<string>();
        int done = 0;

        foreach (var (targetPath, templatePath) in Targets)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPath) == null)
            {
                summary.Add($"  SKIP 대상없음: {targetPath}");
                continue;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(templatePath) == null)
            {
                summary.Add($"  SKIP 템플릿없음: {templatePath}");
                continue;
            }

            GameObject template = PrefabUtility.LoadPrefabContents(templatePath);
            GameObject target   = PrefabUtility.LoadPrefabContents(targetPath);

            // 1. 이전에 이 툴이 넣은 요소만 제거 (idempotent)
            RemovePreviousBuildElements(target);

            // 2. 템플릿에서 포트 + 바디콜라이더 복제
            int ports = 0, bodies = 0;
            foreach (Transform child in template.transform)
            {
                bool isPort = child.GetComponent<BuildPort>() != null;
                bool isBody = !isPort
                              && child.GetComponent<Collider>() != null
                              && child.GetComponentInChildren<Renderer>(true) == null;

                if (!isPort && !isBody) continue; // 아트 모델 등은 스킵

                GameObject copy = Object.Instantiate(child.gameObject, target.transform);
                copy.name = isBody ? BodyColliderName : child.name;
                copy.transform.localPosition = child.localPosition;
                copy.transform.localRotation = child.localRotation;
                copy.transform.localScale    = child.localScale;
                SetLayerRecursive(copy, layer);

                if (isPort) ports++; else bodies++;
            }

            // 3. 루트 레이어도 BuildPort 로
            target.layer = layer;

            // 4. 저장
            bool ok = PrefabUtility.SaveAsPrefabAsset(target, targetPath) != null;
            PrefabUtility.UnloadPrefabContents(template);
            PrefabUtility.UnloadPrefabContents(target);

            done++;
            summary.Add($"  {Path.GetFileNameWithoutExtension(targetPath)} <- {Path.GetFileNameWithoutExtension(templatePath)} (포트 {ports} / 바디 {bodies}) {(ok ? "OK" : "저장실패")}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[FacilityPortSetup] 완료 {done}/{Targets.Length}\n" +
            string.Join("\n", summary) +
            "\n\n[직접 확인할 것]\n" +
            "1. 바디 BoxCollider 는 footprint(XZ)만 템플릿 기준 - 높이/Center 는 새 모델에 맞게 인스펙터 조정\n" +
            "2. 포트는 템플릿 레이아웃 그대로 복제됨 - 설비별 입출력 수(시트 input/outputSlotCount)에 맞게 불필요 포트 삭제 / portType 조정\n" +
            "3. 머신 스크립트(ProcessingMachine 등)는 재원 담당 - 이 툴은 안 건드림\n" +
            "4. 완료 후 FacilityPrefabDatabase 에 프리팹 등록 + FacilityData 시트에 행 추가(facilityName/gridW/gridH/iconKey/buildSlot)\n" +
            "   (이름/아이콘/슬롯순서 전부 시트 기준. BuildManager.buildSlots 는 폐지됨, FacilityIconDatabase 는 폴백)");
    }

    // 이전 실행에서 넣은 포트(BuildPort)와 바디("[Build] BodyCollider")만 제거
    static void RemovePreviousBuildElements(GameObject root)
    {
        var toDelete = new List<GameObject>();
        foreach (Transform child in root.transform)
        {
            bool isPort = child.GetComponent<BuildPort>() != null;
            bool isBody = child.name == BodyColliderName;
            if (isPort || isBody) toDelete.Add(child.gameObject);
        }
        foreach (var go in toDelete) Object.DestroyImmediate(go);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }
}
