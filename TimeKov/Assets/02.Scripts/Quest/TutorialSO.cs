using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Quest/Tutorial")]
public class TutorialSO : ScriptableObject
{
    [Tooltip("PlayerPrefs 키 prefix. 본 게임 메인 퀘스트와 충돌 방지. 변경 시 진행도 리셋")]
    public string savePrefix = "tutorial";
    public CategorySO[] categories;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (categories == null) return;
        var ids = new HashSet<string>();
        foreach (var c in categories)
        {
            if (c == null) continue;
            if (!ids.Add(c.id)) Debug.LogError($"[TutorialSO] 중복 카테고리 ID: {c.id}", c);
        }
    }
#endif
}
