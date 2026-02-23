using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestSlotUI : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI statusText; 
    public Button slotButton;        

    private Quest myQuest;           
    private QuestUIManager uiManager;  

    public void Setup(Quest quest, QuestUIManager manager)
    {
        myQuest = quest;
        uiManager = manager;

        titleText.text = quest.title;

        switch (quest.state)
        {
            case QuestState.Available:
                statusText.text = "<color=green>New</color>";
                break;
            case QuestState.Accepted:
                statusText.text = $"<color=yellow>Accepted ({quest.currentAmount}/{quest.targetAmount})</color>";
                break;
            case QuestState.Completed:
                statusText.text = "<color=gray>Completed</color>";
                break;
        }

        slotButton.onClick.RemoveAllListeners();
        slotButton.onClick.AddListener(() => {
            uiManager.ShowQuestDetail(myQuest);
        });
    }
}