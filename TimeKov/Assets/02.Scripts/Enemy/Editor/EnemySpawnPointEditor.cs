using System.Reflection;
using UnityEditor;
using UnityEngine;

// 적 스폰 구역 인스펙터를 한글로 그린다.
//
// ★필드 이름 자체는 영어로 둔다. C# 은 한글 식별자를 허용하지만,
//   필드명을 바꾸면 유니티가 다른 필드로 보고 씬에 저장된 값을 전부 잃는다.
//   그래서 "코드는 영어 / 화면은 한글" 로 분리한다.
// 툴팁은 원본 스크립트의 [Tooltip] 을 리플렉션으로 그대로 가져온다(설명을 두 군데 안 적게).
[CustomEditor(typeof(EnemySpawnPoint))]
public class EnemySpawnPointEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawStatusBox();

        Section("스폰 목록");
        Row("spawnEntries", "몬스터 목록");

        Section("구역 전체 제한");
        Row("totalMaxAlive",         "동시 생존 상한 (0=무제한)");
        Row("minSpawnSpacing",       "최소 간격 (m, 0=안씀)");
        Row("respawnPlayerDistance", "리스폰 최소 거리 (m, 0=안씀)");

        Section("시작할 때");
        Row("spawnOnStart",         "시작하자마자 스폰");
        Row("initialSpawnInterval", "한 마리당 터울 (초)");

        Section("순찰");
        Row("patrolPointsPerEnemy", "마리당 순찰 지점 수");
        Row("patrolRadius",         "순찰 반경 (m, 0=구역 전체)");
        Row("navMeshSampleRadius",  "길찾기 보정 반경 (m)");

        Section("스폰 제외 구역");
        Row("excludeZones",                "제외할 박스들");
        Row("autoExcludeOtherSpawnPoints", "다른 스폰 구역 자동 제외");

        Section("지면 맞춤");
        Row("snapToGround", "바닥에 붙이기");
        Row("groundMask",   "바닥으로 볼 레이어");

        Section("보기");
        Row("drawGizmos",    "구역 표시");
        Row("areaColor",     "구역 색");
        Row("waypointColor", "순찰 지점 색");

        serializedObject.ApplyModifiedProperties();
    }

    // 플레이 중 현황 — 상한을 얼마로 잡을지 감을 잡는 용도
    private void DrawStatusBox()
    {
        if (!Application.isPlaying) return;

        var sp = (EnemySpawnPoint)target;
        int alive = sp.EditorAliveCount;
        int cap = sp.EditorTotalMaxAlive;

        string msg = cap > 0
            ? $"현재 {alive}마리 / 상한 {cap}마리"
            : $"현재 {alive}마리 (상한 없음)";

        EditorGUILayout.HelpBox(msg, cap > 0 && alive >= cap ? MessageType.Warning : MessageType.Info);
        EditorGUILayout.Space(4f);
    }

    private void Section(string title)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private void Row(string fieldName, string korean)
    {
        var p = serializedObject.FindProperty(fieldName);
        if (p == null) return;   // 필드가 지워졌으면 조용히 건너뛴다
        EditorGUILayout.PropertyField(p, EditorUtil.Label(p, korean), true);
    }
}

// 스폰 목록의 항목 한 줄. 한글 라벨로 접었다 폈다 한다.
[CustomPropertyDrawer(typeof(EnemySpawnPoint.SpawnEntry))]
public class SpawnEntryDrawer : PropertyDrawer
{
    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        EditorGUI.BeginProperty(pos, label, prop);

        var prefab = prop.FindPropertyRelative("prefab");
        var count  = prop.FindPropertyRelative("maxCount");

        // 접혀 있을 때도 뭐가 몇 마리인지 보이게 제목을 만든다
        string title = prefab != null && prefab.objectReferenceValue != null
            ? $"{prefab.objectReferenceValue.name}  x{(count != null ? count.intValue : 1)}"
            : "(비어 있음)";

        float h = EditorGUIUtility.singleLineHeight;
        var line = new Rect(pos.x, pos.y, pos.width, h);
        prop.isExpanded = EditorGUI.Foldout(line, prop.isExpanded, title, true);

        if (prop.isExpanded)
        {
            EditorGUI.indentLevel++;
            line.y += h + 2f;
            Field(ref line, prop, "prefab",                 "몬스터 프리팹");
            Field(ref line, prop, "maxCount",               "동시 생존 최대");
            Field(ref line, prop, "respawnDelay",           "재등장 대기 (초)");
            Field(ref line, prop, "isElite",                "엘리트로 취급");
            Field(ref line, prop, "unlockAfterNormalKills", "해금 조건 (일반몹 처치수, 0=바로)");
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
    {
        float h = EditorGUIUtility.singleLineHeight;
        return prop.isExpanded ? h * 6f + 12f : h;
    }

    private void Field(ref Rect line, SerializedProperty parent, string name, string korean)
    {
        var p = parent.FindPropertyRelative(name);
        if (p == null) return;
        EditorGUI.PropertyField(line, p, EditorUtil.Label(p, korean));
        line.y += EditorGUIUtility.singleLineHeight + 2f;
    }
}

// 한글 라벨 + 원본 [Tooltip] 을 합쳐주는 공용 헬퍼.
// 설명을 인스펙터 코드에 또 적지 않게 하려고 리플렉션으로 원본에서 읽어온다.
public static class EditorUtil
{
    public static GUIContent Label(SerializedProperty p, string korean)
        => new GUIContent(korean, FindTooltip(p));

    private static string FindTooltip(SerializedProperty p)
    {
        var obj = p.serializedObject.targetObject;
        if (obj == null) return "";

        // "spawnEntries.Array.data[0].prefab" 같은 경로에서 타입을 따라 내려간다
        var type = obj.GetType();
        FieldInfo field = null;
        foreach (var part in p.propertyPath.Split('.'))
        {
            if (part == "Array") { field = null; continue; }
            if (part.StartsWith("data[")) continue;

            if (field != null)
            {
                var t = field.FieldType;
                if (t.IsArray) t = t.GetElementType();
                else if (t.IsGenericType) t = t.GetGenericArguments()[0];
                type = t;
            }
            field = type.GetField(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return "";
        }

        var attr = field != null ? field.GetCustomAttribute<TooltipAttribute>() : null;
        return attr != null ? attr.tooltip : "";
    }
}
