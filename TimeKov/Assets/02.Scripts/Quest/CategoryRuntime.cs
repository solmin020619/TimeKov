public class CategoryRuntime
{
    public CategorySO data;
    public int activeQuestIndex;
    public ObjectiveSO[] activeObjectives;
    public bool IsActivated;            // UI 슬라이드 인 후 true
    public bool IsCheckingComplete;     // 재진입 방어
    public bool IsCategoryDone => activeQuestIndex >= data.quests.Length;
}
