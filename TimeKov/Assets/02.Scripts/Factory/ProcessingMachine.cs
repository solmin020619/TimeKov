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

        private bool _processing;
        private MachineLoopSound _loopSound;

        protected virtual void Start()
        {
            // 같은 오브젝트에서 MachineLoopSound 캐싱 (없으면 그냥 null)
            _loopSound = GetComponent<MachineLoopSound>();
        }

        protected override void OnItemReceived(int itemId, int amount)
        {
            if (!_processing) TryStartProcessing();
        }

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

            if (OutputBuffer.Stock.Count == 0)
                SetStatus(MachineStatus.Idle);

            NotifyBufferChanged();
            TryStartProcessing();
        }
    }
}