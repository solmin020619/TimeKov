using UnityEditor;
using UnityEngine;

/// <summary>
/// 선택된 GameObject(들)의 CapsuleCollider를 자식 Renderer bounds 기반 자동 셋업.
/// T-pose 모델의 양팔 폭 영향 줄이기 위해 width multiplier 적용.
/// 메뉴: Tools > Enemy > Auto-fit CapsuleCollider To Selected
/// </summary>
public static class EnemyColliderAutoFit
{
    // T-pose에서 양팔 펼친 width를 보정. 0.35 = 몸통 폭 추정 (양팔 펼친 길이의 35%)
    const float WidthMultiplier = 0.35f;

    // [HIDDEN] [MenuItem("Tools/Enemy/Auto-fit CapsuleCollider To Selected")]
    public static void Fit()
    {
        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
        {
            Debug.LogWarning("[ColliderFit] GameObject 선택 안 됨. Hierarchy에서 적 prefab/인스턴스 선택 후 다시 실행.");
            return;
        }

        int fitted = 0;
        foreach (var go in Selection.gameObjects)
        {
            // 자식 Renderer 모두 모아서 bounds 합침 (Skinned + Mesh)
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[ColliderFit] {go.name}: 자식에 Renderer 없음. SK_*.fbx 자식으로 박았는지 확인.");
                continue;
            }

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                worldBounds.Encapsulate(renderers[i].bounds);

            // world → root local 변환
            Vector3 localCenter = go.transform.InverseTransformPoint(worldBounds.center);
            Vector3 worldSize = worldBounds.size;
            Vector3 localScale = go.transform.lossyScale;
            Vector3 localSize = new Vector3(
                worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(localScale.x)),
                worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(localScale.y)),
                worldSize.z / Mathf.Max(0.0001f, Mathf.Abs(localScale.z))
            );

            // CapsuleCollider 추가/갱신
            var capsule = go.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = go.AddComponent<CapsuleCollider>();

            capsule.center = localCenter;
            capsule.height = Mathf.Max(0.2f, localSize.y);

            // T-pose 양팔 폭 보정: x/z 중 작은 값 기준 + multiplier
            float rawWidth = Mathf.Min(localSize.x, localSize.z);
            float radius = Mathf.Max(0.1f, rawWidth * 0.5f * WidthMultiplier * 2f);
            // 너무 작으면 height의 1/6로 보정 (얇은 모델 방지)
            radius = Mathf.Max(radius, localSize.y * 0.15f);
            capsule.radius = radius;
            capsule.direction = 1; // Y-axis

            EditorUtility.SetDirty(go);
            fitted++;

            Debug.Log($"[ColliderFit] {go.name}: Center={localCenter}, Height={capsule.height:F2}, Radius={capsule.radius:F2}");
        }

        Debug.Log($"[ColliderFit] 완료. {fitted}개 적용.\n" +
                  "양팔 폭 보정(WidthMultiplier=0.35)이 부정확하면 인스펙터에서 Radius 미세조정 권장.");
    }
}
