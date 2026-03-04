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
        if (questWindow != null) questWindow.SetActive(false);
        if (acceptButton != null) acceptButton.onClick.AddListener(OnClickAccept);

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
            // PausePanel이 따로 켜져있으면 퀘스트 열기 금지 (기존 로직 유지)
            if (pausePanel != null && pausePanel.activeSelf) return;

            // ✅ [추가] UIStateManager가 있으면 무조건 거기로 위임
            if (UIStateManager.Instance != null)
            {
                UIStateManager.Instance.ToggleQuest();

                // 퀘스트가 열리는 프레임이면 UI 내용 갱신만 해준다(입력/커서/타임스케일은 UIStateManager가 담당)
                if (UIStateManager.Instance.GetCurrentState() == UIStateManager.UIState.Quest)
                {
                    PrepareQuestUIOnly();
                }
                return;
            }

            // ------------------------------
            // (레거시) UIStateManager가 없는 씬에서는 기존 방식 유지
            // ------------------------------
            if (questWindow != null && !questWindow.activeSelf)
            {
                OpenQuestWindow();
            }
        }
    }

    // ✅ [추가] UI 내용만 갱신 (입력/커서/타임스케일/플컨 enable 건드리지 않음)
    private void PrepareQuestUIOnly()
    {
        if (questWindow == null) return;

        // questWindow.SetActive(true)는 UIStateManager가 켜주지만, 혹시 직접 켜진 경우도 대비해서 안전하게 처리
        if (!questWindow.activeSelf) questWindow.SetActive(true);

        UpdateQuestList();

        if (detailTitle != null) detailTitle.text = "";
        if (detailDesc != null) detailDesc.text = "";
        if (acceptButton != null) acceptButton.gameObject.SetActive(false);
    }

    public void OpenQuestWindow()
    {
        // ✅ [추가] UIStateManager가 있으면 상태 전환은 거기로 위임
        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.SetState(UIStateManager.UIState.Quest);
            PrepareQuestUIOnly();
            return;
        }

        // ------------------------------
        // (레거시) 기존 동작 유지
        // ------------------------------
        if (questWindow != null) questWindow.SetActive(true);
        UpdateQuestList();
        if (detailTitle != null) detailTitle.text = "";
        if (detailDesc != null) detailDesc.text = "";
        if (acceptButton != null) acceptButton.gameObject.SetActive(false);

        if (crosshairUI != null) crosshairUI.SetActive(false);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (playerController != null) playerController.enabled = false;
        Time.timeScale = 0f;
    }

    public void CloseQuestWindow()
    {
        // ✅ [추가] UIStateManager가 있으면 닫기는 CloseAllUI로 통일 (입력/커서/타임스케일 복구 포함)
        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.CloseAllUI();
            return;
        }

        // ------------------------------
        // (레거시) 기존 동작 유지
        // ------------------------------
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
        if (questListContent != null)
        {
            foreach (Transform child in questListContent)
            {
                if (child != null) Destroy(child.gameObject);
            }
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
        if (detailTitle != null) detailTitle.text = quest.title;
        if (detailDesc != null) detailDesc.text = $"{quest.description}\n\n<color=yellow>Progress: ({quest.currentAmount} / {quest.targetAmount})</color>";

        if (acceptButton != null)
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
                if (questWindow != null && questWindow.activeSelf)
                {
                    UpdateQuestList();
                    if (selectedQuest == quest) ShowQuestDetail(quest);
                }
                break;
            }
        }
    }
}