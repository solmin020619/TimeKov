// =====================================================================
// ProcessingMachine.cs
// "재료가 모이면 자동으로 가공 → 결과물 배출" 패턴의 공통 구현.
//
// 설비마다 설정할 것 (Inspector):
//   recipes      : 조합식 목록 (기획서 조합식 그대로 입력)
//   outputBelt   : 결과물을 보낼 컨베이어 벨트
//
// 조합식 예시 (9mm 탄약 / AmmoPress):
//   Recipe 0
//     inputs  [0] itemId: "경량_금속판"  amount: 1
//             [1] itemId: "무연_화약"    amount: 1
//             [2] itemId: "납_구슬"      amount: 1
//     outputs [0] itemId: "9mm_탄약"    amount: 30
//     processingTime: 10
// =====================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public class ProcessingMachine : MachineBase
    {
        // ---------------------------------------------------------------
        // Inspector — 여기서 조합식 편집
        // ---------------------------------------------------------------
        [Header("조합식 (기획서 그대로 입력)")]
        [SerializeField] private List<FactoryRecipe> recipes = new();

        [Header("결과물 배출 벨트")]
        [SerializeField] private ConveyorBelt outputBelt;

        // ---------------------------------------------------------------
        // 상태
        // ---------------------------------------------------------------
        private bool _processing;

        // 진행도 (0~1) — UI에서 진행 바에 연결 가능
        public float Progress { get; private set; }

        // ---------------------------------------------------------------
        // 재료 도착 시 자동 처리 시도
        // ---------------------------------------------------------------
        protected override void OnReceived(string itemId, int amount)
        {
            if (!_processing) TryStartProcessing();
        }

        // ---------------------------------------------------------------
        // 재료 충족 레시피 탐색 → 코루틴 시작
        // ---------------------------------------------------------------
        private bool TryStartProcessing()
        {
            foreach (var recipe in recipes)
            {
                if (inputBuffer.HasAll(recipe.inputs))
                {
                    StartCoroutine(ProcessRoutine(recipe));
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------
        // 가공 코루틴
        // ---------------------------------------------------------------
        private IEnumerator ProcessRoutine(FactoryRecipe recipe)
        {
            _processing = true;
            SetStatus(MachineStatus.Processing);

            // 재료 즉시 소모
            inputBuffer.ConsumeAll(recipe.inputs);

            // 가공 시간 경과
            float elapsed = 0f;
            while (elapsed < recipe.processingTime)
            {
                elapsed  += Time.deltaTime;
                Progress  = elapsed / recipe.processingTime;
                yield return null;
            }
            Progress = 0f;

            // 결과물 배출
            foreach (var output in recipe.outputs)
                Dispatch(outputBelt, output.itemId, output.amount);

            _processing = false;
            SetStatus(MachineStatus.Idle);

            // 버퍼에 재료가 남아있으면 연속 처리
            TryStartProcessing();
        }
    }
}
