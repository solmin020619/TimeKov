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

        [Header("제작 시간 (폴백)")]
        [Tooltip("시트 레시피에 craftTime 이 없을 때만 쓰는 폴백(초). 평소엔 레시피별 시트 craftTime 사용.")]
        public float processingTime = 5f;

        // 레시피는 시트(RecipeData/RecipeInputData)에서 런타임 로드 — 인스펙터 편집 안 함.
        private List<FactoryRecipe> recipes = new();

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
        private MachineLoopSound       _loopSound;
        private MachineAnimationEffect _animEffect;

        // ── 초기화 ─────────────────────────────────────────────────────────

        protected override void Start()
        {
            base.Start();   // 월드 표시(FacilityWorldDisplay) 자동 부착
            _loopSound  = GetComponent<MachineLoopSound>();
            _animEffect = GetComponent<MachineAnimationEffect>();
            TryLoadRecipesFromSheet();
        }

        private void OnDestroy()
        {
            DataBoot.OnDataLoaded -= OnDataLoaded;
        }

        // ── 시트 레시피 로드 ────────────────────────────────────────────────
        // 데이터가 이미 로드됐으면 즉시, 아직이면 DataBoot.OnDataLoaded 시점에 로드.
        private void TryLoadRecipesFromSheet()
        {
            if (DataBoot.IsLoaded)
                LoadRecipesFromSheet();
            else
                DataBoot.OnDataLoaded += OnDataLoaded;
        }

        private void OnDataLoaded()
        {
            DataBoot.OnDataLoaded -= OnDataLoaded;
            LoadRecipesFromSheet();
        }

        // FacilityId 기준 시트 레시피를 빌드해 recipes 를 채운다 (인스펙터 하드코딩 대체).
        public void LoadRecipesFromSheet()
        {
            int fid = FacilityId;
            if (fid <= 0) return;

            recipes = FactoryRecipeBuilder.BuildForFacility(fid);
            LockedRecipeIndex = -1;

            var facility = GameDataUtility.GetFacility(fid);
            if (facility != null && !string.IsNullOrEmpty(facility.facilityName))
                machineName = facility.facilityName;
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
        /// 이 설비의 레시피 중 "하나라도" 이 아이템을 입력 재료로 쓰면 true.
        /// 레시피가 여러 개인 설비는 선택된 레시피뿐 아니라 모든 레시피의 재료를 받아야
        /// (다른 레시피용 재료가 창고로 빠지지 않음) 하므로 전체 레시피를 검사한다.
        /// </summary>
        public override bool CanReceive(int itemId)
        {
            if (recipes == null) return false;
            foreach (var recipe in recipes)
            {
                if (recipe?.inputs == null) continue;
                foreach (var slot in recipe.inputs)
                    if (slot.itemId == itemId) return true;
            }
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
            _loopSound?.StartProduction();   // 생산 시작 → 루프 사운드 ON
            _animEffect?.StartProduction();  // 생산 시작 → 애니메이션·이펙트 ON

            InputBuffer.ConsumeAll(recipe.inputs);
            NotifyBufferChanged();

            // 레시피별 시트 제작시간(craftTime) 우선, 없으면 processingTime 폴백. 레벨 배율 적용.
            float baseTime = recipe.craftTime > 0f ? recipe.craftTime : processingTime;
            var facilityInst = GetComponent<FacilityInstance>();
            float actualTime = facilityInst != null
                ? facilityInst.GetFinalProcessTime(baseTime)
                : baseTime;

            float elapsed = 0f;
            while (elapsed < actualTime)
            {
                // ── 가동 중 연료 소진 → 일시정지 ────────────────────────
                if (!HasFuel)
                {
                    SetStatus(MachineStatus.NoFuel);
                    _loopSound?.StopProduction(playDoneSound: false);
                    _animEffect?.StopProduction();   // 연료 소진 → 일시 정지
                    while (!HasFuel) yield return null;
                    SetStatus(MachineStatus.Processing);
                    _loopSound?.StartProduction();
                    _animEffect?.StartProduction();  // 연료 재충전 → 재시작
                }

                ConsumeFuelDelta(Time.deltaTime);
                elapsed += Time.deltaTime;
                Progress = elapsed / actualTime;
                yield return null;
            }

            Progress = 0f;
            ActiveRecipe = null;
            _processing = false;
            _loopSound?.StopProduction(playDoneSound: true);   // 생산 완료 → 루프 OFF + 완료음 1회
            _animEffect?.StopProduction();                     // 생산 완료 → 애니메이션·이펙트 부드럽게 OFF

            // 레시피 제작 진행도 +1 (한 번 제작 = +1, 출력 개수 무관). 마스터(10회) 후 도감서 잭팟 활성화해야 발동.
            RecipeProgress.RecordCraft(recipe.recipeId);
            bool jackpotOn = RecipeProgress.IsJackpotActive(recipe.recipeId);

            foreach (var output in recipe.outputs)
            {
                int amount = output.amount;
                // 잭팟: 활성화된 레시피는 일정 확률로 2배 제작
                if (jackpotOn && Random.value < RecipeProgress.JackpotChance)
                {
                    amount *= 2;
                    RecipeProgress.RaiseJackpot(FacilityId, output.itemId, transform.position);
                }
                // 퀘스트 시스템 통지 (Dispatch 전 = 가공 완료 시점). 실제 산출 개수로 통지.
                GameEvents.RaiseFacilityProcessComplete(FacilityId, output.itemId, amount);
                Dispatch(output.itemId, amount);
            }

            // 같은 레시피 output이면 쌓아두면서 계속 생산, 다른 아이템이면 대기
            if (OutputBuffer.Stock.Count == 0)
                SetStatus(MachineStatus.Idle);

            NotifyBufferChanged();
            TryStartProcessing();  // 같은 레시피면 바로 다음 생산 시작, 다르면 내부에서 블로킹
        }
    }
}