using UnityEditor;
using UnityEngine;

/// <summary>
/// [FilledBy] 달린 필드를 인스펙터에서 회색으로 잠그고,
/// 바로 아래 줄에 "값을 바꾸려면: (진짜 주인)" 을 적어 준다.
/// </summary>
[CustomPropertyDrawer(typeof(FilledByAttribute))]
public class FilledByDrawer : PropertyDrawer
{
    private const float HintHeight = 14f;
    private static GUIStyle _hintStyle;

    private static GUIStyle HintStyle
    {
        get
        {
            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(EditorStyles.miniLabel);
                _hintStyle.fontStyle = FontStyle.Italic;
                _hintStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
            }
            return _hintStyle;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true) + HintHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var attr = (FilledByAttribute)attribute;

        float fieldH = EditorGUI.GetPropertyHeight(property, label, true);
        var fieldRect = new Rect(position.x, position.y, position.width, fieldH);

        // 힌트는 라벨 칸이 아니라 값 칸 아래에 붙인다(어느 필드 얘기인지 눈으로 바로 이어지게).
        float labelW = EditorGUIUtility.labelWidth;
        var hintRect = new Rect(position.x + labelW, position.y + fieldH,
                                Mathf.Max(0f, position.width - labelW), HintHeight);

        string note = "여기서 바꿔도 실행하면 " + attr.Source + " 값으로 덮어쓴다.";
        label.tooltip = string.IsNullOrEmpty(label.tooltip) ? note : label.tooltip + "\n" + note;

        bool prevEnabled = GUI.enabled;
        GUI.enabled = false;
        EditorGUI.PropertyField(fieldRect, property, label, true);
        GUI.enabled = prevEnabled;

        EditorGUI.LabelField(hintRect, "값을 바꾸려면: " + attr.Source, HintStyle);
    }
}
