#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;

public static class LocalizedLabelEditorUtil
{
    // 빌더에서 호출: GO에 LocalizedLabel 붙이고 키 저장
    // 빌더 재실행 시 중복 방지를 위해 기존 컴포넌트는 먼저 제거
    public static void Attach(GameObject go, string koreanKey)
    {
        var existing = go.GetComponent<LocalizedLabel>();
        if (existing != null) Object.DestroyImmediate(existing);

        var label = go.AddComponent<LocalizedLabel>();
        var so = new SerializedObject(label);
        so.FindProperty("_koreanKey").stringValue = koreanKey;
        so.ApplyModifiedProperties();
    }

    public static void Attach(TMP_Text tmp, string koreanKey)
        => Attach(tmp.gameObject, koreanKey);
}
#endif
