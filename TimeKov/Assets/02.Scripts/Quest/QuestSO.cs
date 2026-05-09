using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "Quest/Quest")]
public class QuestSO : ScriptableObject
{
    public string id;
    public string title;
    [TextArea] public string description;
    public ObjectiveSO[] objectives;

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
