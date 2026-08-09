using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 설비 콜라이더에서 "플레이어가 겹쳐서 미끄러지는" 원인을 제거하는 툴.
///
/// 원인: 설비 콜라이더는 전부 BuildPort 레이어라 플레이어 GroundMask 에 없어
/// 닿아도 공중 취급(_isGrounded=false) → 평지 안정화가 안 걸리고, 캡슐이 콜라이더와
/// 겹친 채 점프하면 물리 depenetration 이 위치를 밀어내며 미끄러진다.
///   - 작은 BuildPort 포트(지면 높이 박스)들 = 플레이어가 올라타 겹침
///   - 3x3 바디 박스 = 바닥이 지면에서 떠(lip) 캡슐 하단이 그 아래로 파고든 뒤 점프 시 겹침
///
/// 해결:
///   1) BuildPort 포트 콜라이더 → isTrigger=true (플레이어와 물리 충돌 안 함).
///      레일 스냅(Raycast portMask, queriesHitTriggers=1)·설비 감지(OverlapSphere)는
///      트리거도 감지하므로 정상 동작. 카메라는 BuildPort 를 Ignore 라 영향 없음.
///   2) "[Build] BodyCollider" 솔리드 박스 → 바닥을 지면(GroundLocalY) 아래까지 연장(lip 제거).
///      상호작용용 트리거 박스는 그대로 둔다.
///
/// 메뉴: Tools > Grid > Fix Facility Colliders (anti-slide)
/// 재실행 안전: 이미 처리된 항목은 건너뜀.
/// </summary>
public static class FacilityColliderFixTool
{
    const string BodyColliderName = "[Build] BodyCollider";
    const float GroundLocalY = -0.6f; // 설비 원점=지면 기준, 바디 박스 바닥을 여기까지 내림

    static readonly string[] Targets =
    {
        "Assets/05.Prefabs/Grid/3X3/생체 추출기.prefab",
        "Assets/05.Prefabs/Grid/3X3/에너지 변환기.prefab",
        "Assets/05.Prefabs/Grid/3X3/저장고.prefab",
        "Assets/05.Prefabs/Grid/5X5/생체 배양기.prefab",
        "Assets/05.Prefabs/Grid/5X5/생체 분리기.prefab",
        "Assets/05.Prefabs/Grid/5X5/용해로.prefab",
        "Assets/05.Prefabs/Grid/5X5/코어 합성기.prefab",
        "Assets/05.Prefabs/Grid/5X5/화학 정제기.prefab",
    };

    [MenuItem("Tools/TIMEKOV/건축/설비 콜라이더 고치기 (미끄러짐 방지)")]
    public static void FixAll()
    {
        var summary = new List<string>();
        int changed = 0;

        foreach (var path in Targets)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                summary.Add($"  SKIP 대상없음: {path}");
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);

            int portFixed = 0;
            int bodyFixed = 0;

            // 1) BuildPort 포트 콜라이더 → 트리거
            foreach (var port in root.GetComponentsInChildren<BuildPort>(true))
            {
                foreach (var box in port.GetComponents<BoxCollider>())
                {
                    if (box.isTrigger) continue;
                    box.isTrigger = true;
                    portFixed++;
                }
            }

            // 2) "[Build] BodyCollider" 솔리드 박스 → 지면까지 연장
            Transform body = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == BodyColliderName) { body = t; break; }

            if (body != null)
            {
                foreach (var box in body.GetComponents<BoxCollider>())
                {
                    if (box.isTrigger) continue; // 상호작용 트리거는 유지
                    float top    = box.center.y + box.size.y * 0.5f;
                    float bottom = box.center.y - box.size.y * 0.5f;
                    if (bottom <= GroundLocalY + 0.001f) continue; // 이미 지면 아래
                    float newHeight = top - GroundLocalY;
                    box.size   = new Vector3(box.size.x, newHeight, box.size.z);
                    box.center = new Vector3(box.center.x, (top + GroundLocalY) * 0.5f, box.center.z);
                    bodyFixed++;
                }
            }

            if (portFixed > 0 || bodyFixed > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed++;
                summary.Add($"  OK {Path.GetFileNameWithoutExtension(path)} (포트 트리거 {portFixed} / 바디 지면연장 {bodyFixed})");
            }
            else
            {
                summary.Add($"  - {Path.GetFileNameWithoutExtension(path)} 변경없음(이미 처리됨)");
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FacilityColliderFix] 완료 {changed}/{Targets.Length}\n" + string.Join("\n", summary));
    }
}
