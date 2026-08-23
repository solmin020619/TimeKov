using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "TIMEKOV/퀘스트/퀘스트")]
public class QuestSO : ScriptableObject
{
    // 퀘스트 완료 보상 1건 (아이템 ID + 개수)
    [System.Serializable]
    public class QuestReward
    {
        [Tooltip("지급할 아이템 ID (ItemData 시트 기준)")]
        public int itemId;

        [Tooltip("지급 개수")]
        public int amount = 1;
    }

    public string id;
    public string title;
    [TextArea] public string description;
    public ObjectiveSO[] objectives;

    [Header("완료 보상 (선택)")]
    [Tooltip("퀘스트 완료 시 인벤토리로 지급할 아이템 목록.\n" +
             "비워두면 보상 없음. 여러 종류 주려면 항목을 추가.")]
    public QuestReward[] rewards;

    [Tooltip("퀘스트 트래커에 보상을 이 한 마디로 묶어서 보여준다(예: 초급 앰플 꾸러미).\n" +
             "비워두면 rewards 를 아이템별로 한 줄씩 나열한다.\n" +
             "★실제 지급 내용은 안 바뀐다 - 표시만 묶는 것이다.\n" +
             "보상이 3~4종이 되면 나열이 길어져 트래커를 잡아먹으므로 그때만 쓴다.")]
    public string rewardSummary;

    [Header("Designer Hooks (optional)")]
    [Tooltip("UI 슬라이드 인 시작. 등장 사운드, 카메라 줌 등")]
    public UnityEvent onShown;
    [Tooltip("입력 카운트 시작. 게임플레이 큐, 적 스폰 등")]
    public UnityEvent onActivated;
    [Tooltip("완료. 사운드, 다음 단계 트리거 (보상은 본 게임 갈 때)")]
    public UnityEvent onCompleted;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name;
        if (objectives == null || objectives.Length == 0)
            Debug.LogWarning($"[QuestSO] '{name}' has no objectives.", this);
    }
#endif
}
