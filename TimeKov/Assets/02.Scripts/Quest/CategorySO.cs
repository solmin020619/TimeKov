using UnityEngine;

[CreateAssetMenu(menuName = "Quest/Category")]
public class CategorySO : ScriptableObject
{
    public string id;
    public string title;
    public Sprite icon;
    public QuestSO[] quests;

    [Tooltip("이 카테고리 id 가 완료된 뒤에야 트래커에 등장(휴면 시작). 비우면 시작부터 활성.\n" +
             "예: 엔드게임 라인에 'tutorial_main' 을 넣으면 메인 튜토 완료 후 등장.")]
    public string activateAfterCategoryId = "";

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name;
    }
#endif
}
