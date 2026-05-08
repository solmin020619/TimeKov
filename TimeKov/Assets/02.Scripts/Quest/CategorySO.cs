using UnityEngine;

[CreateAssetMenu(menuName = "Quest/Category")]
public class CategorySO : ScriptableObject
{
    public string id;
    public string title;
    public Sprite icon;
    public QuestSO[] quests;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name;
    }
#endif
}
