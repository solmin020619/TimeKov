using System.Collections.Generic;
using UnityEngine;

// 설비별 "전 레시피 마스터" 보상 설정 (인스펙터 편집).
// 설비마다 레시피 개수가 달라(3번설비는 3개뿐) 100% 채우는 난이도가 다르므로 보상 개수를 설비별로 조절.
// Resources/Codex/CodexRecipeRewardConfig 로 로드.
[CreateAssetMenu(menuName = "TIMEKOV/Codex Recipe Reward Config")]
public class CodexRecipeRewardConfig : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("설비 ID (1~8)")]
        public int facilityId;
        [Tooltip("이 설비 전 레시피 마스터(각 10회 제작) 시 지급할 보상 아이템 개수")]
        public int rewardCount = 1;
    }

    [Tooltip("보상 아이템 ID. 기본 6101 = 코어 키트 I")]
    public int rewardItemId = 6101;

    [Tooltip("설비별 보상 개수. 미설정 설비는 보상 0(레시피 적은 설비는 적게 줘서 밸런스).")]
    public List<Entry> facilities = new List<Entry>();

    public int RewardCountFor(int facilityId)
    {
        foreach (var e in facilities)
            if (e != null && e.facilityId == facilityId) return Mathf.Max(0, e.rewardCount);
        return 0;
    }
}
