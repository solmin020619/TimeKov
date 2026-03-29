// =====================================================================
// FactoryRecipe.cs
// 설비 하나의 조합식을 Inspector에서 직접 편집하는 구조체 모음.
//
// 사용 방법:
//   - ItemSlot : 아이템 ID(string) + 수량. 입력/출력 슬롯에 사용.
//   - FactoryRecipe : inputs + outputs + 소요시간 한 묶음.
//
// 아이템 ID는 다른 팀원이 만든 ItemData.itemId 와 동일한 값을 쓰면 된다.
// 코드는 itemId 문자열만 비교하므로 ItemData 구조 변경에 영향받지 않는다.
// =====================================================================

using UnityEngine;

namespace TIMEKOV.Factory
{
    [System.Serializable]
    public struct ItemSlot
    {
        [Tooltip("ItemData.itemId 와 동일한 값")]
        public string itemId;
        public int amount;
    }

    [System.Serializable]
    public class FactoryRecipe
    {
        [Tooltip("입력 재료 목록")]
        public ItemSlot[] inputs;

        [Tooltip("출력 결과물 목록")]
        public ItemSlot[] outputs;

        [Tooltip("1회 가공 소요 시간(초)")]
        public float processingTime = 5f;
    }
}
