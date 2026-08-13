using System.Collections.Generic;
using UnityEngine;

// 설비별 "전 레시피 마스터" 보상 설정 (인스펙터 편집).
// 설비마다 레시피 개수가 달라 100% 채우는 난이도가 다르므로 보상 개수를 설비별로 조절한다.
// Resources/Codex/CodexRecipeRewardConfig 로 로드.
//
// ★여기만 시트가 아니라 수동이다. 레시피를 옮기거나 설비를 추가하면 같이 고쳐야 한다.
//   기준(08-13) = 마스터는 레시피당 10회 제작이므로 대략 20회당 보상 1개.
//     3개=2 / 4개=2 / 6개=3 / 8~9개=4 / 12개=5
//   빠진 설비는 보상 0 이다 - 연마기(10)가 이 표에 없어서 0 이던 것을 08-13 에 채웠다.
[CreateAssetMenu(menuName = "TIMEKOV/도감/레시피 보상 설정")]
public class CodexRecipeRewardConfig : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("설비 ID. 건축바 슬롯 번호가 아니라 시트의 facilityId 다.")]
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
