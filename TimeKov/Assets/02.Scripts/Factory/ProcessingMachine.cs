// =====================================================================
// ProcessingMachine.cs
// 레시피 기반 자동 가공 설비.
// 재료가 InputBuffer에 모이면 자동 가공 시작 → OutputBuffer로 배출.
// =====================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public class ProcessingMachine : MachineBase
    {
        [Header("조합식 목록 (Inspector에서 편집)")]
        [SerializeField] private List<FactoryRecipe> recipes = new();

        // 진행도 0~1 (UI 진행 바용)
        public float Progress    { get; private set; }
        public bool  IsProcessing => _processing;

        // 현재 진행 중인 레시피 (UI에 표시)
        public FactoryRecipe ActiveRecipe { get; private set; }

        public List<FactoryRecipe> Recipes => recipes;

        private bool _processing;

        // ── 아이템 수신 시 자동 처리 시도 ──────────────────────────
        protected override void OnItemReceived(int itemId, int amount)
        {
            if (!_processing) TryStartProcessing();
        }

        // ── 레시피 탐색 → 가공 시작 ────────────────────────────────
        public bool TryStartProcessing()
        {
            if (_processing) return false;

            foreach (var recipe in recipes)
            {
                if (InputBuffer.HasAll(recipe.inputs))
                {
                    StartCoroutine(ProcessRoutine(recipe));
                    return true;
                }
            }
            return false;
        }

        private IEnumerator ProcessRoutine(FactoryRecipe recipe)
        {
            _processing  = true;
            ActiveRecipe = recipe;
            SetStatus(MachineStatus.Processing);

            // 재료 즉시 소모
            InputBuffer.ConsumeAll(recipe.inputs);
            NotifyBufferChanged();

            // 가공 시간 경과
            float elapsed = 0f;
            while (elapsed < recipe.processingTime)
            {
                elapsed  += Time.deltaTime;
                Progress  = elapsed / recipe.processingTime;
                yield return null;
            }

            Progress     = 0f;
            ActiveRecipe = null;
            _processing  = false;

            // 결과물 배출
            foreach (var output in recipe.outputs)
                Dispatch(output.itemId, output.amount);

            if (OutputBuffer.Stock.Count == 0)
                SetStatus(MachineStatus.Idle);
            NotifyBufferChanged();

            // 버퍼에 재료 남아있으면 연속 처리
            TryStartProcessing();
        }
    }
}
