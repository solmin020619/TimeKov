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

    [Header("Quest Data (Initial Setup)")]
    public List<Quest> initialQuests = new List<Quest>();

    [Header("Player Control")]
    private PlayerController playerController;

    public GameObject crosshairUI;
    public GameObject pausePanel;

    private Quest selectedQuest;

    void Start()
    {
        questWindow.SetActive(false);
        acceptButton.onClick.AddListener(OnClickAccept);
        playerController = FindFirstObjectByType<PlayerController>();

        if (!QuestDataManager.isInitialized)
        {
            QuestDataManager.globalQuests = new List<Quest>(initialQuests);
            QuestDataManager.isInitialized = true;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            if (pausePanel != null && pausePanel.activeSelf) return;

            if (!questWindow.activeSelf)
            {
                OpenQuestWindow();
            }
        }
    }

    public void OpenQuestWindow()
    {
        questWindow.SetActive(true);
        UpdateQuestList();
        detailTitle.text = "";
        detailDesc.text = "";
        acceptButton.gameObject.SetActive(false);

        if (crosshairUI != null) crosshairUI.SetActive(false);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (playerController != null) playerController.enabled = false;
        Time.timeScale = 0f;
    }

    public void CloseQuestWindow()
    {
        if (questWindow != null) questWindow.SetActive(false);
        if (crosshairUI != null) crosshairUI.SetActive(true);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        if (playerController != null)
        {
            playerController.enabled = true;
            playerController.SyncSettings();
        }
        Time.timeScale = 1f;
    }

    void UpdateQuestList()
    {
        foreach (Transform child in questListContent)
        {
            if (child != null) Destroy(child.gameObject);
        }

        foreach (Quest quest in QuestDataManager.globalQuests)
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

        acceptButton.gameObject.SetActive(quest.state == QuestState.Available);
    }

    void OnClickAccept()
    {
        if (selectedQuest != null && selectedQuest.state == QuestState.Available)
        {
            selectedQuest.state = QuestState.Accepted;
            UpdateQuestList();
            ShowQuestDetail(selectedQuest);
        }
    }

    public void AddQuestProgress(string questTitle, int amount)
    {
        foreach (Quest quest in QuestDataManager.globalQuests)
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
}