using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public class ProcessingMachine : MachineBase
    {
        [Header("설비 이름 (UI 표시용)")]
        public string machineName = "설비";

        [Header("조합식 목록")]
        [SerializeField] private List<FactoryRecipe> recipes = new();

        public float Progress { get; private set; }
        public bool IsProcessing => _processing;
        public FactoryRecipe ActiveRecipe { get; private set; }
        public List<FactoryRecipe> Recipes => recipes;

        /// <summary>
        /// 고정된 레시피 인덱스. -1이면 미선택(첫 번째 레시피 자동 사용).
        /// MachineUI에서 SetLockedRecipe()로 변경한다.
        /// </summary>
        [HideInInspector] public int LockedRecipeIndex = -1;

        private bool _processing;
        private MachineLoopSound _loopSound;

        protected virtual void Start()
        {
            // 같은 오브젝트에서 MachineLoopSound 캐싱 (없으면 그냥 null)
            _loopSound = GetComponent<MachineLoopSound>();
        }

        /// <summary>
        /// 고정 레시피를 설정한다. index가 범위를 벗어나면 -1(자동)으로 초기화.
        /// </summary>
        public void SetLockedRecipe(int index)
        {
            if (recipes == null || index < 0 || index >= recipes.Count)
                LockedRecipeIndex = -1;
            else
                LockedRecipeIndex = index;
        }

        /// <summary>
        /// 플레이어가 아이템을 모두 꺼낸 후 설비 상태를 수동으로 리셋한다.
        /// </summary>
        public void ResetStatusIfIdle()
        {
            if (!_processing && InputBuffer.Stock.Count == 0 && OutputBuffer.Stock.Count == 0)
                SetStatus(MachineStatus.Idle);
        }

        private FactoryRecipe GetLockedRecipe()
        {
            if (recipes == null || recipes.Count == 0) return null;
            if (LockedRecipeIndex >= 0 && LockedRecipeIndex < recipes.Count)
                return recipes[LockedRecipeIndex];
            return recipes[0];  // 미선택 시 첫 번째 레시피 사용
        }

        /// <summary>
        /// 고정 레시피의 입력 재료이면 true. 레시피가 없으면 false.
        /// </summary>
        public override bool CanReceive(int itemId)
        {
            var recipe = GetLockedRecipe();
            if (recipe == null || recipe.inputs == null) return false;
            foreach (var slot in recipe.inputs)
                if (slot.itemId == itemId) return true;
            return false;
        }

        protected override void OnItemReceived(int itemId, int amount)
        {
            if (!_processing) TryStartProcessing();
        }

        public bool TryStartProcessing()
        {
            if (_processing) return false;

            var recipe = GetLockedRecipe();
            if (recipe == null) return false;

            // OutputBuffer에 현재 레시피와 다른 아이템이 있으면 생산 블로킹
            // 같은 레시피의 output 아이템이면 계속 쌓으면서 생산 허용
            if (OutputBuffer.Stock.Count > 0 && !IsOutputCompatibleWith(recipe))
                return false;

            if (InputBuffer.HasAll(recipe.inputs))
            {
                StartCoroutine(ProcessRoutine(recipe));
                return true;
            }
            return false;
        }

        /// <summary>
        /// OutputBuffer에 있는 아이템이 모두 지정 레시피의 output인지 확인한다.
        /// 현재 레시피와 다른 아이템이 하나라도 있으면 false.
        /// </summary>
        private bool IsOutputCompatibleWith(FactoryRecipe recipe)
        {
            if (recipe == null || recipe.outputs == null) return false;

            foreach (var kv in OutputBuffer.Stock)
            {
                if (kv.Value <= 0) continue;

                bool found = false;
                foreach (var output in recipe.outputs)
                {
                    if (output.itemId == kv.Key) { found = true; break; }
                }
                if (!found) return false;
            }
            return true;
        }

        /// <summary>OutputBuffer가 비면 생산 재개를 시도한다.</summary>
        protected override void OnOutputCleared()
        {
            TryStartProcessing();
        }

        private IEnumerator ProcessRoutine(FactoryRecipe recipe)
        {
            if (!InputBuffer.HasAll(recipe.inputs))
            {
                _processing = false;
                SetStatus(MachineStatus.Idle);
                yield break;
            }

            _processing = true;
            ActiveRecipe = recipe;
            SetStatus(MachineStatus.Processing);
            _loopSound?.StartProduction();  // 생산 시작 → 루프 사운드 ON

            InputBuffer.ConsumeAll(recipe.inputs);
            NotifyBufferChanged();

            float elapsed = 0f;
            while (elapsed < recipe.processingTime)
            {
                elapsed += Time.deltaTime;
                Progress = elapsed / recipe.processingTime;
                yield return null;
            }

            Progress = 0f;
            ActiveRecipe = null;
            _processing = false;
            _loopSound?.StopProduction(playDoneSound: true);  // 생산 완료 → 루프 OFF + 완료음 1회

            foreach (var output in recipe.outputs)
            {
                // 퀘스트 시스템 통지 (Dispatch 전 = 가공 완료 시점)
                GameEvents.RaiseFacilityProcessComplete(FacilityId, output.itemId, output.amount);
                Dispatch(output.itemId, output.amount);
            }

            // 같은 레시피 output이면 쌓아두면서 계속 생산, 다른 아이템이면 대기
            if (OutputBuffer.Stock.Count == 0)
                SetStatus(MachineStatus.Idle);

            NotifyBufferChanged();
            TryStartProcessing();  // 같은 레시피면 바로 다음 생산 시작, 다르면 내부에서 블로킹
        }
    }
}