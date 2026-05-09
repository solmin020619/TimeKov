/// <summary>
/// 진행도를 어디에도 저장하지 않는 storage — 개발/테스트용.
/// 매 Play마다 모든 카테고리 인덱스 0부터 시작.
/// QuestManager 인스펙터의 useNoOpStorage 토글로 활성화.
/// </summary>
public class NoOpQuestStorage : IQuestSaveStorage
{
    public int GetCategoryIndex(string categoryId, int defaultValue = 0) => defaultValue;
    public void SetCategoryIndex(string categoryId, int value) { }
    public void Flush() { }
}
