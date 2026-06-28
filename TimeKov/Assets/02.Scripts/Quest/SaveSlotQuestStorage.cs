// =====================================================================
// SaveSlotQuestStorage.cs
// IQuestSaveStorage를 통합 SaveSlotManager 위에 구현.
// PlayerPrefsQuestStorage(개별 저장)를 대체 — 퀘스트 진행도를 PlayerPrefs가 아니라
// 현재 활성 슬롯의 GameSaveData.questProgress에 적는다.
//
// Flush()는 PlayerPrefs.Save()와 동일한 "지금 디스크에 확실히 써라" 의도를 보존하기 위해
// SaveSlotManager.SaveActive()를 호출한다 (등록된 다른 ISaveable까지 한 번에 같이 기록됨 —
// QuestManager가 퀘스트 완료 시 Flush를 호출하던 시점은 원래도 "지금 잃으면 안 되는 진행" 시점이므로
// 이 타이밍에 전체 저장을 같이 트리거하는 것이 자연스럽다).
// =====================================================================

using System.Collections.Generic;

public class SaveSlotQuestStorage : IQuestSaveStorage
{
    static GameSaveData Data => SaveSlotManager.Instance.Data;

    public int GetCategoryIndex(string categoryId, int defaultValue = 0)
    {
        var entry = Find(categoryId);
        return entry?.questIndex ?? defaultValue;
    }

    public void SetCategoryIndex(string categoryId, int value)
    {
        var entry = Find(categoryId);
        if (entry == null)
        {
            entry = new QuestCategoryProgress { categoryId = categoryId, questIndex = value };
            Data.questProgress.Add(entry);
        }
        else
        {
            entry.questIndex = value;
        }
    }

    public void Flush() => SaveSlotManager.Instance.SaveActive();

    static QuestCategoryProgress Find(string categoryId)
    {
        List<QuestCategoryProgress> list = Data.questProgress;
        for (int i = 0; i < list.Count; i++)
            if (list[i].categoryId == categoryId) return list[i];
        return null;
    }
}
