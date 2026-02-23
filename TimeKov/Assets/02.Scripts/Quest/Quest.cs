using UnityEngine;

public enum QuestState
{
    Available,
    Accepted,
    Completed
}

[System.Serializable]
public class Quest
{
    public string title;
    [TextArea]
    public string description;
    public QuestState state;

    public int currentAmount;
    public int targetAmount;

    public Quest(string t, string d, QuestState s, int current, int target)
    {
        title = t;
        description = d;
        state = s;
        currentAmount = current;
        targetAmount = target;
    }

    public void AddProgress(int amount)
    {
        if (state == QuestState.Accepted)
        {
            currentAmount += amount;

            if (currentAmount >= targetAmount)
            {
                currentAmount = targetAmount;
                state = QuestState.Completed;
                Debug.Log($"퀘스트 완료: {title}");
            }
        }
    }
}