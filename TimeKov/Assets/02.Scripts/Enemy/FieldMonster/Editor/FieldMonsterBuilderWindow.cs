using UnityEditor;
using UnityEngine;

// 필드 몬스터 빌더 모음 창. 각 몬스터 빌더는 [MenuItem] 을 두지 않아 Tools 메뉴에 안 뜨고,
// 여기 버튼으로만 실행한다. 새 몬스터 추가 시 아래 buttons 배열에 한 줄만 추가하면 된다.
// 열기: Window > Field Monster Builder
public class FieldMonsterBuilderWindow : EditorWindow
{
    // (버튼 라벨, 빌드 동작)
    static readonly (string label, System.Action build)[] Builders =
    {
        ("거미S3",   SpiderS3Builder.Build),
        ("거미여왕", SpiderQueenBuilder.Build),
    };

    [MenuItem("Window/Field Monster Builder")]
    static void Open()
    {
        var w = GetWindow<FieldMonsterBuilderWindow>("Field Monster");
        w.minSize = new Vector2(240f, 120f);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("필드 몬스터 빌더", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("원본 참조 방식. 재실행해도 안전(멱등). 빌드 후 씬의 인스턴스는 재생 시 자가복구.", MessageType.None);
        EditorGUILayout.Space();

        foreach (var b in Builders)
            if (GUILayout.Button($"Build {b.label}", GUILayout.Height(28f)))
                b.build();
    }
}
