// =====================================================================
// FactoryRecipe.cs
// DataManager 파싱 기준 int ID 기반 조합식.
// Inspector에서 직접 편집한다.
// =====================================================================

using UnityEngine;

namespace TIMEKOV.Factory
{
    [System.Serializable]
    public struct FactorySlot
    {
        [Tooltip("DataManager 아이템 ID (파싱된 정수값)")]
        public int itemId;
        public int amount;
    }

    [System.Serializable]
    public class FactoryRecipe
    {
        [Tooltip("입력 재료")]
        public FactorySlot[] inputs;

        [Tooltip("출력 결과물")]
        public FactorySlot[] outputs;

        [Tooltip("1회 가공 소요 시간(초)")]
        public float processingTime = 5f;
    }
}
