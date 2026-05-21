using UnityEditor;
using UnityEngine;

/// <summary>
/// EnemySpawnPoint를 Hierarchy에서 빠르게 만드는 메뉴.
/// GameObject > Enemy > Spawn Point (Box Area)
/// </summary>
public static class EnemySpawnPointMenu
{
    [MenuItem("GameObject/Enemy/Spawn Point (Box Area)", false, 10)]
    public static void CreateSpawnPoint(MenuCommand cmd)
    {
        var go = new GameObject("EnemySpawnPoint");

        // Scene 뷰 카메라 앞에 배치 (기본 위치 원점이면 안 보임)
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv != null && sv.camera != null)
        {
            Vector3 pos = sv.camera.transform.position + sv.camera.transform.forward * 8f;
            pos.y = 0f;
            go.transform.position = pos;
        }

        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(10f, 2f, 10f);
        box.center = new Vector3(0f, 1f, 0f);

        go.AddComponent<EnemySpawnPoint>();

        // 부모 지정 (우클릭한 오브젝트가 있으면 그 자식으로)
        GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);

        Undo.RegisterCreatedObjectUndo(go, "Create EnemySpawnPoint");
        Selection.activeObject = go;
        EditorGUIUtility.PingObject(go);
    }
}
