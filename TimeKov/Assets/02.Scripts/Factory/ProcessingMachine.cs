using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public class ProcessingMachine : MachineBase
    {
        [Header("설비 이름 (UI 표시용)")]
        public string machineName = "설비";
        public override string MachineName => !string.IsNullOrEmpty(machineName) ? machineName : base.MachineName;

        [Header("제작 시간")]
        [Tooltip("1회 가공 소요 시간(초). 레벨 배율이 곱해져 실제 시간이 결정됨.")]
        public float processingTime = 5f;

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

        // ── 자동 창고 전송 ─────────────────────────────────────────────────
        [Header("자동 창고 전송 설정")]
        [Tooltip("자동 전송 주기 (초)")]
        public float autoStorageInterval = 5f;

        /// <summary>모든 튜토리얼 완료 시 true. 모든 설비 공유. (테스트 중: 상시 개방)</summary>
        public static bool AutoStorageUnlocked { get; private set; } = true;

        private bool      _autoStorageEnabled;
        private Coroutine _autoStorageRoutine;

        public bool AutoStorageEnabled => _autoStorageEnabled;
        protected override bool UseAutoStorage => _autoStorageEnabled;

        // ── 초기화 ─────────────────────────────────────────────────────────

        protected virtual void Start()
        {
            _loopSound = GetComponent<MachineLoopSound>();

            // 이미 모든 튜토리얼을 마친 상태면 즉시 잠금 해제
            CheckAutoStorageUnlock();

            // 이후 완료 시 이벤트로 감지
            var qm = QuestManager.Instance;
            if (qm != null) qm.OnAllCompleted += OnAllTutorialsCompleted;
        }

        private void OnDestroy()
        {
            var qm = QuestManager.Instance;
            if (qm != null) qm.OnAllCompleted -= OnAllTutorialsCompleted;

            if (_autoStorageRoutine != null)
            {
                StopCoroutine(_autoStorageRoutine);
                _autoStorageRoutine = null;
            }
        }

        private void OnAllTutorialsCompleted() => AutoStorageUnlocked = true;

        private static void CheckAutoStorageUnlock()
        {
            if (AutoStorageUnlocked) return;
            var qm = QuestManager.Instance;
            if (qm == null) return;
            foreach (var rt in qm.Runtimes)
                if (!rt.IsCategoryDone) return;
            AutoStorageUnlocked = true;
        }

        // ── 자동 창고 전송 제어 ───────────────────────────────────────────

        /// <summary>자동 창고 전송을 켜거나 끈다. 잠금 해제 전에는 무시.</summary>
        public void SetAutoStorage(bool enabled)
        {
            if (!AutoStorageUnlocked) return;
            _autoStorageEnabled = enabled;

            if (_autoStorageEnabled)
            {
                if (_autoStorageRoutine == null)
                    _autoStorageRoutine = StartCoroutine(AutoStorageRoutine());
            }
            else
            {
                if (_autoStorageRoutine != null)
                {
                    StopCoroutine(_autoStorageRoutine);
                    _autoStorageRoutine = null;
                }
                // OFF 전환 시 OutputBuffer에 남은 아이템을 즉시 벨트로 발송
                TryDispatchPendingOutput();
            }
        }

        private IEnumerator AutoStorageRoutine()
        {
            while (_autoStorageEnabled)
            {
                yield return new WaitForSeconds(autoStorageInterval);
                if (!_autoStorageEnabled) break;

                var storage = InventoryManager.StorageInstance;
                if (storage == null || OutputBuffer.Stock.Count == 0) continue;

                var snapshot = new Dictionary<int, int>(OutputBuffer.Stock);
                foreach (var kv in snapshot)
                {
                    if (kv.Value <= 0) continue;
                    if (!OutputBuffer.Consume(kv.Key, kv.Value)) continue;
                    storage.AddItem(kv.Key, kv.Value);
                    GameEvents.RaiseFacilityProcessComplete(FacilityId, kv.Key, kv.Value);
                }

                storage.ForceRefreshUI();
                if (OutputBuffer.Stock.Count == 0) SetStatus(MachineStatus.Idle);
                NotifyBufferChanged();
            }

            _autoStorageRoutine = null;
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

            // 코루틴 중복 진입 방지 — 연료 대기 전에 먼저 true로 설정
            _processing = true;

            // ── 연료 없으면 연료가 채워질 때까지 대기 ──────────────────
            if (!HasFuel)
            {
                SetStatus(MachineStatus.NoFuel);
                while (!HasFuel) yield return null;
                // 연료 대기 중 재료가 사라졌을 경우 중단
                if (!InputBuffer.HasAll(recipe.inputs))
                {
                    _processing = false;
                    SetStatus(MachineStatus.Idle);
                    yield break;
                }
            }
            ActiveRecipe = recipe;
            SetStatus(MachineStatus.Processing);
            _loopSound?.StartProduction();  // 생산 시작 → 루프 사운드 ON

            InputBuffer.ConsumeAll(recipe.inputs);
            NotifyBufferChanged();

            // 설비 단위 제작 시간에 레벨 배율 적용
            var facilityInst = GetComponent<FacilityInstance>();
            float actualTime = facilityInst != null
                ? facilityInst.GetFinalProcessTime(processingTime)
                : processingTime;

            float elapsed = 0f;
            while (elapsed < actualTime)
            {
                // ── 가동 중 연료 소진 → 일시정지 ────────────────────────
                if (!HasFuel)
                {
                    SetStatus(MachineStatus.NoFuel);
                    _loopSound?.StopProduction(playDoneSound: false);
                    while (!HasFuel) yield return null;
                    SetStatus(MachineStatus.Processing);
                    _loopSound?.StartProduction();
                }

                ConsumeFuelDelta(Time.deltaTime);
                elapsed += Time.deltaTime;
                Progress = elapsed / actualTime;
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