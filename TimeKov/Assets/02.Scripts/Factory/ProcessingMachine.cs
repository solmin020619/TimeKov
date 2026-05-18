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

            foreach (var output in recipe.outputs)
                Dispatch(output.itemId, output.amount);

            if (OutputBuffer.Stock.Count == 0)
                SetStatus(MachineStatus.Idle);

            NotifyBufferChanged();
            TryStartProcessing();
        }
    }
}