using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public class ProcessingMachine : MachineBase
    {
        // 설비 이름(UI 표시용) — 시트 facilityName. 시트가 원본이라 인스펙터 노출/직렬화 안 함
        // (옛 프리팹 값은 어차피 덮어써지던 죽은 값).
        // ★Start 의 LoadRecipesFromSheet 에만 맡기면 안 된다. DataBoot 로드가 늦으면 그때까지 비어 있고,
        //   그 사이에 이름을 물어본 쪽(F 선택 패널)이 폴백 문자열을 캐시해버려 계속 남는다.
        //   -> 물어볼 때마다 늦게라도 시트에서 채운다(한 번 채워지면 그대로 재사용).
        private string machineName;
        public override string MachineName
        {
            get
            {
                if (string.IsNullOrEmpty(machineName)) ResolveMachineName();
                return !string.IsNullOrEmpty(machineName) ? machineName : base.MachineName;
            }
        }

        private void ResolveMachineName()
        {
            int fid = FacilityId;
            if (fid <= 0) return;
            var facility = GameDataUtility.GetFacility(fid);
            if (facility != null && !string.IsNullOrEmpty(facility.facilityName))
                machineName = facility.facilityName;
        }

        [Header("제작 시간 (폴백)")]
        [Tooltip("시트 레시피에 craftTime 이 없을 때만 쓰는 폴백(초). 평소엔 레시피별 시트 craftTime 사용.")]
        public float processingTime = 5f;

        [Header("출력 버퍼 상한")]
        [Tooltip("출력이 이만큼 쌓이면(다운스트림이 막혀 벨트가 가득 찬 상태 = 역압) 생산을 멈춘다.\n" +
                 "벨트 머리 칸이 비어 버퍼가 한 개라도 빠지면 자동으로 생산을 재개한다.")]
        public int maxOutputStock = 3;

        // 레시피는 시트(RecipeData/RecipeInputData)에서 런타임 로드 — 인스펙터 편집 안 함.
        private List<FactoryRecipe> recipes = new();

        public float Progress { get; private set; }
        public bool IsProcessing => _processing;
        public FactoryRecipe ActiveRecipe { get; private set; }
        public List<FactoryRecipe> Recipes => recipes;

        /// <summary>재료가 들어있거나 가공 중 = 한 레시피에 "커밋"된 상태. 이때는 다른 레시피로 못 바꾼다(재료 회수 필요).
        /// 상태를 저장하지 않고 버퍼/가공 상태에서 그때그때 유도한다(설비 껐다 켜도 동일하게 판정).</summary>
        public bool IsCommitted => _processing || (InputBuffer != null && InputBuffer.Stock.Count > 0);

        /// <summary>제작이 진행 중이라 이미 소모해 버린 재료(저장용). 진행 중이 아니면 null.
        /// 생산은 시작 즉시 InputBuffer 에서 재료를 빼고 완료 시점에야 결과물을 넣는다.
        /// 그 사이엔 재료가 어느 버퍼에도 없어서, 그대로 저장하면 재료도 결과물도 없이 증발한다.
        /// 저장(BuildManager.Capture)은 이 값을 입력 재고에 합쳐 기록한다 -> 복원 시 처음부터 다시 제작.
        /// (진행률까지 이어붙이지는 않는다. 시간은 다시 흐르면 되지만 재료는 되돌릴 수 없다)</summary>
        public FactorySlot[] InFlightInputs => _processing && ActiveRecipe != null ? ActiveRecipe.inputs : null;

        /// <summary>지금 고정/가동 중인 레시피 인덱스(GetLockedRecipe 와 동일 기준). 커밋 판정·표시용.</summary>
        public int EffectiveRecipeIndex =>
            (recipes != null && LockedRecipeIndex >= 0 && LockedRecipeIndex < recipes.Count) ? LockedRecipeIndex : 0;

        /// <summary>실제 제작시간(초) = 레시피 craftTime(없으면 processingTime 폴백) x 레벨배율 x 공장속도.
        /// 가공 코루틴과 UI 표시가 이 한 메서드를 공유한다(표시-실제 어긋남 방지).</summary>
        public float ResolveProcessTime(FactoryRecipe recipe)
        {
            if (recipe == null) return 0f;
            float baseTime = recipe.craftTime > 0f ? recipe.craftTime : processingTime;
            var inst = GetComponent<FacilityInstance>();
            return inst != null ? inst.GetFinalProcessTime(baseTime) : baseTime;
        }

        /// <summary>
        /// 고정된 레시피 인덱스. -1이면 미선택(첫 번째 레시피 자동 사용).
        /// MachineUI에서 SetLockedRecipe()로 변경한다.
        /// </summary>
        [HideInInspector] public int LockedRecipeIndex = -1;

        // 세이브 복원이 LoadRecipesFromSheet()보다 먼저 끝날 수 있어(레시피 미로딩 상태),
        // 복원된 인덱스를 잠깐 들고 있다가 레시피 로드 완료 시점에 적용한다.
        private int? _pendingLockedRecipeIndex;

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

            if (_pendingLockedRecipeIndex.HasValue)
            {
                SetLockedRecipe(_pendingLockedRecipeIndex.Value);
                _pendingLockedRecipeIndex = null;
            }
            else
            {
                LockedRecipeIndex = -1;
            }

            ResolveMachineName();

            // 세이브 복원으로 버퍼가 미리 채워져 있었다면, 레시피가 준비된 지금 생산 재개 시도.
            if (!_processing)
                TryStartProcessing();
        }

        /// <summary>세이브 복원 전용 — 레시피가 아직 로드되지 않았을 수 있어 지연 적용한다.</summary>
        public void RestoreLockedRecipe(int index)
        {
            if (recipes != null && recipes.Count > 0)
                SetLockedRecipe(index);
            else
                _pendingLockedRecipeIndex = index;
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
            // ★벨트로 들어온 재료는 레시피를 정하지 않는다 — SetLockedRecipe 를 부르는 건
            //   손으로 드래그해 넣는 경로(RecipeDropSlot)뿐이다. 그래서 아직 아무 레시피도
            //   안 잡힌 설비에 벨트가 재료를 밀어 넣으면, 설비는 0번 레시피를 쓰는 것으로
            //   취급되어 (a) 생산이 영영 시작되지 않고 (b) 그 재료칸이 0 으로 보인다
            //   (다른 레시피 칸은 IsSuppressed 로 가려지기 때문). 여기서 맞춰 준다.
            if (!_processing && LockedRecipeIndex < 0)
            {
                int idx = FindRecipeUsing(itemId);
                if (idx >= 0) SetLockedRecipe(idx);
            }

            if (!_processing) TryStartProcessing();
        }

        /// <summary>이 아이템을 입력으로 쓰는 첫 레시피의 인덱스. 없으면 -1.</summary>
        private int FindRecipeUsing(int itemId)
        {
            if (recipes == null) return -1;
            for (int i = 0; i < recipes.Count; i++)
            {
                var inputs = recipes[i]?.inputs;
                if (inputs == null) continue;
                foreach (var s in inputs)
                    if (s.itemId == itemId) return i;
            }
            return -1;
        }

        /// <summary>지금 잡힌 레시피로는 못 돌리는데 당장 돌릴 수 있는 레시피가 있으면 그쪽으로 넘어간다.
        ///
        /// 판정 기준이 두 개 다 '지금 돌릴 수 있는가(HasAll)'다.
        ///   ★'재료가 하나도 안 남았을 때만 옮긴다'로 하면 안 된다. 위쪽 설비가 벨트로 재료를
        ///     계속 흘려보내면 한 번 만들 양에 못 미치는 부스러기가 항상 남아 있어서, 조건이
        ///     영영 성립하지 않는다 — 다른 레시피 재료가 가득해도 설비가 멈춰 선다.
        ///   ★옮길 대상도 '당장 다 갖춘' 레시피만 본다. 재료가 한 개라도 있으면 옮기게 하면,
        ///     플레이어가 손으로 모으던 레시피를 벨트가 밀어 넣은 다른 재료가 가로챈다.
        ///   ★가공 중에는 옮기지 않는다 — 돌던 레시피는 끝까지 돌린다.</summary>
        private void TryAdvanceRecipe()
        {
            if (_processing || recipes == null || recipes.Count == 0) return;

            var cur = GetLockedRecipe();
            if (cur?.inputs != null && InputBuffer.HasAll(cur.inputs)) return;   // 지금 것으로 돌릴 수 있다

            int curIdx = EffectiveRecipeIndex;
            for (int i = 0; i < recipes.Count; i++)
            {
                if (i == curIdx) continue;
                var inputs = recipes[i]?.inputs;
                if (inputs == null || inputs.Length == 0) continue;
                if (!InputBuffer.HasAll(inputs)) continue;
                SetLockedRecipe(i);
                NotifyBufferChanged();   // 열려 있는 설비 창이 바뀐 레시피로 다시 그리게
                return;
            }
        }

        public bool TryStartProcessing()
        {
            if (_processing) return false;

            // 재료가 바닥난 레시피에 묶여 있으면, 재료가 있는 쪽으로 넘어간 뒤 판정한다.
            TryAdvanceRecipe();

            var recipe = GetLockedRecipe();
            if (recipe == null) return false;

            // ★출력 버퍼에 '다른 레시피의 완성품'이 남아 있어도 생산을 막지 않는다.
            //   레시피가 자동으로 넘어가게 된 뒤로는 완성품 두 종류가 한 버퍼에 있는 게 정상이다.
            //   막아 두면: 공격력 앰플을 만든 뒤 회복으로 넘어가려는데, 안 가져간 공격력 앰플
            //   한 개 때문에 설비가 회색으로 멈춰 선다(재료는 충분한데).
            //   과부하는 아래 maxOutputStock 상한이 그대로 막아 준다.
            //   재료 버퍼(InputBuffer)도 이미 여러 레시피 재료를 같이 담는다 — 같은 규칙이다.

            // 출력 버퍼가 상한까지 찼으면 생산 중지 — 다운스트림이 막혀 벨트가 가득 찬 상태(역압).
            // 벨트 머리 칸이 비어 버퍼가 한 개라도 빠지면 OnOutputDrained 로 재개된다.
            if (OutputBuffer.TotalCount >= maxOutputStock)
                return false;

            if (InputBuffer.HasAll(recipe.inputs))
            {
                StartCoroutine(ProcessRoutine(recipe));
                return true;
            }
            return false;
        }

        // IsOutputCompatibleWith 는 삭제했다 — '출력 버퍼에 다른 레시피 완성품이 있으면 생산 금지'
        //   판정이었는데, 레시피 자동 전환이 생긴 뒤로는 그게 정상 상황이 됐다(TryStartProcessing 주석 참고).

        /// <summary>OutputBuffer가 비면 생산 재개를 시도한다.</summary>
        protected override void OnOutputCleared()
        {
            TryStartProcessing();
        }

        /// <summary>출력 버퍼에서 한 개가 빠져 자리가 나면(상한에 걸려 멈춰 있었을 수 있음) 생산 재개를 시도한다.</summary>
        protected override void OnOutputDrained()
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

            // 실제 제작시간 = 레시피 craftTime(없으면 폴백) x 레벨배율 x 공장속도. UI 표시와 동일 계산.
            float actualTime = ResolveProcessTime(recipe);

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