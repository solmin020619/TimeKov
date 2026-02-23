using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestUIManager : MonoBehaviour
{
    [Header("UI Objects")]
    public GameObject questWindow;      
    public Transform questListContent;   
    public GameObject questSlotPrefab;   

    [Header("Right Side Details")]
    public TextMeshProUGUI detailTitle;  
    public TextMeshProUGUI detailDesc;  
    public Button acceptButton;        

    [Header("Quest Data")]
    public List<Quest> allQuests = new List<Quest>();

    [Header("Player Control")]
    private PlayerController playerController;

    private Quest selectedQuest;

    void Start()
    {
        questWindow.SetActive(false);
        acceptButton.onClick.AddListener(OnClickAccept);
        playerController = FindFirstObjectByType<PlayerController>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            ToggleQuestWindow();
        }
    }

    public void ToggleQuestWindow()
    {
        bool isActive = questWindow.activeSelf;
        questWindow.SetActive(!isActive);

        if (!isActive)
        {
            UpdateQuestList();
            detailTitle.text = "";
            detailDesc.text = "";
            acceptButton.gameObject.SetActive(false);

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (playerController != null) playerController.enabled = false;
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            if (playerController != null) playerController.enabled = true;
        }
    }

    void UpdateQuestList()
    {
        foreach (Transform child in questListContent)
        {
            Destroy(child.gameObject);
        }

        foreach (Quest quest in allQuests)
        {
            GameObject newSlot = Instantiate(questSlotPrefab, questListContent);
            QuestSlotUI slotScript = newSlot.GetComponent<QuestSlotUI>();
            slotScript.Setup(quest, this);
        }
    }
    public void ShowQuestDetail(Quest quest)
    {
        selectedQuest = quest;

        detailTitle.text = quest.title;

        detailDesc.text = $"{quest.description}\n\n<color=yellow>Progress: ({quest.currentAmount} / {quest.targetAmount})</color>";

        if (quest.state == QuestState.Available)
        {
            acceptButton.gameObject.SetActive(true);
        }
        else
        {
            acceptButton.gameObject.SetActive(false);
        }
    }

    void OnClickAccept()
    {
        if (selectedQuest != null && selectedQuest.state == QuestState.Available)
        {
            selectedQuest.state = QuestState.Accepted;

            UpdateQuestList();
            ShowQuestDetail(selectedQuest);

            Debug.Log($"Äù½ºÆ® ¼ö¶ôµÊ: {selectedQuest.title}");
        }
    }

    public void AddQuestProgress(string questTitle, int amount)
    {
        foreach (Quest quest in allQuests)
        {
            if (quest.title == questTitle && quest.state == QuestState.Accepted)
            {
                quest.AddProgress(amount);

                if (questWindow.activeSelf)
                {
                    UpdateQuestList();
                    if (selectedQuest == quest) ShowQuestDetail(quest);
                }
                break;
            }
        }
    }

    public void CloseQuestWindow()
    {
        if (questWindow != null && questWindow.activeSelf)
        {
            questWindow.SetActive(false);

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            if (playerController != null) playerController.enabled = true;
        }
    }
}