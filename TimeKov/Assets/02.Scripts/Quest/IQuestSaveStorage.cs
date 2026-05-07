public interface IQuestSaveStorage
{
    int GetCategoryIndex(string categoryId, int defaultValue = 0);
    void SetCategoryIndex(string categoryId, int value);
    void Flush();
    // BeginBatch/EndBatch는 본 게임 통합 세이브 갈 때 추가 (YAGNI)
}
