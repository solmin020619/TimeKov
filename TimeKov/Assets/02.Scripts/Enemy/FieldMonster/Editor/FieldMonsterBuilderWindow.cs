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
        ("거미S3",          SpiderS3Builder.Build),
        ("거미여왕(설산)",  SpiderQueenBuilder.BuildSnow),
        ("거미여왕(사막)",  SpiderQueenBuilder.BuildDesert),
        ("거미여왕(자연)",  SpiderQueenBuilder.BuildNature),
        ("록몬스터(자연)",  RockMonsterBuilder.BuildNature),
        ("록몬스터(설산)",  RockMonsterBuilder.BuildSnow),
        ("록몬스터(사막)",  RockMonsterBuilder.BuildDesert),
        ("록몬스터(용암)",  RockMonsterBuilder.BuildLava),
        ("머쉬룸(자연)",    MushroomBuilder.BuildNature),
        ("머쉬룸(설산)",    MushroomBuilder.BuildSnow),
        ("머쉬룸(사막)",    MushroomBuilder.BuildDesert),
        ("머쉬룸(용암)",    MushroomBuilder.BuildLava),
        ("본드래곤(설산)",  BoneDragonBuilder.Build),
        ("자이언트웜(사막)", WormBuilder.Build),
    };

    [MenuItem("Tools/TIMEKOV/적/필드 몬스터 만들기")]
    static void Open()
    {
        var w = GetWindow<FieldMonsterBuilderWindow>("Field Monster");
        w.minSize = new Vector2(240f, 120f);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("필드 몬스터 만들기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("원본 참조 방식. 재실행해도 안전(멱등). 빌드 후 씬의 인스턴스는 재생 시 자가복구.", MessageType.None);
        EditorGUILayout.Space();

        // 일괄 빌드 - 전 종 한 번에. 스탯 밸런싱처럼 전체 반영할 때 개별로 14번 안 눌러도 된다.
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.6f, 0.85f, 1f);
        if (GUILayout.Button($"전체 빌드 ({Builders.Length}종)", GUILayout.Height(34f)))
            BuildAll();
        GUI.backgroundColor = prev;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("개별 빌드", EditorStyles.miniBoldLabel);

        foreach (var b in Builders)
            if (GUILayout.Button($"Build {b.label}", GUILayout.Height(28f)))
                b.build();
    }

    // 등록된 빌더를 순서대로 전부 실행. 하나 실패해도 나머지는 계속 굽는다(집계 후 로그).
    static void BuildAll()
    {
        if (!EditorUtility.DisplayDialog("필드 몬스터 전체 빌드",
            $"{Builders.Length}종을 전부 다시 굽는다(프리팹/SO 덮어씀). 스폰VFX는 sourceId 기준 자동 재배정.\n계속?",
            "빌드", "취소")) return;

        int ok = 0, fail = 0;
        try
        {
            for (int i = 0; i < Builders.Length; i++)
            {
                var b = Builders[i];
                EditorUtility.DisplayProgressBar("필드 몬스터 전체 빌드",
                    $"({i + 1}/{Builders.Length}) {b.label}", (float)i / Builders.Length);
                try { b.build(); ok++; }
                catch (System.Exception e) { fail++; Debug.LogError($"[FieldBuildAll] {b.label} 실패: {e.Message}\n{e}"); }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        Debug.Log($"[FieldBuildAll] 완료: 성공 {ok} / 실패 {fail} (총 {Builders.Length}). 실패 있으면 위 에러 로그 확인.");
    }
}
