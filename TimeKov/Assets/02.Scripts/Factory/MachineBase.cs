using System;
using System.Collections.Generic;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public enum MachineStatus { Disconnected, Idle, Processing, OutputReady, NoFuel }

    public abstract class MachineBase : MonoBehaviour
    {
        public readonly ItemBuffer InputBuffer = new();
        public readonly ItemBuffer OutputBuffer = new();

        public MachineStatus Status { get; protected set; } = MachineStatus.Idle;

        /// <summary>UI에 표시할 설비 이름. 서브클래스에서 override해 사용한다.</summary>
        public virtual string MachineName => gameObject.name;

        public event Action OnBufferChanged;
        public event Action<MachineStatus> OnStatusChanged;

        // ── 연료 ──────────────────────────────────────────────────────────
        /// <summary>남은 연료 가동 시간 (초). 0 이하면 연료 없음.</summary>
        public float FuelTimeRemaining { get; private set; }

        /// <summary>투입된 연료 아이템 총 개수 (소수점 포함 — 사용 중인 개수 반영).</summary>
        public int FuelItemCount { get; private set; }

        /// <summary>연료가 남아있는지 여부.</summary>
        public bool HasFuel => FuelTimeRemaining > 0f;

        /// <summary>연료가 추가되거나 소진될 때 발생.</summary>
        public event Action OnFuelChanged;

        /// <summary>연결된 출력 벨트 목록. 라운드 로빈으로 순서대로 사용.</summary>
        [HideInInspector] public List<BeltSegment> outputBelts = new();
        [HideInInspector] public BeltSegment inputBelt;

        // 라운드 로빈 인덱스 — 다음에 사용할 outputBelt 순번
        private int _nextBeltIndex = 0;

        [Header("입출구 포트")]
        public Transform outputPort;
        public Transform inputPort;

        int _facilityIdCache = -1;
        public int FacilityId
        {
            get
            {
                if (_facilityIdCache < 0)
                {
                    var inst = GetComponent<FacilityInstance>();
                    _facilityIdCache = inst != null ? inst.FacilityId : 0;
                }
                return _facilityIdCache;
            }
        }

        // 모든 설비에 엔드필드 스타일 월드 표시(이름/제작아이콘/막힘) 자동 부착.
        // 서브클래스가 Start 를 가지면 base.Start() 를 호출해야 한다(ProcessingMachine 참고).
        protected virtual void Start()
        {
            if (GetComponent<FacilityWorldDisplay>() == null)
                gameObject.AddComponent<FacilityWorldDisplay>();
        }

        /// <summary>
        /// 이 설비가 해당 아이템을 받을 수 있는지 확인한다.
        /// ProcessingMachine 등의 서브클래스에서 레시피 기반으로 오버라이드한다.
        /// 기본값은 true (무조건 수락).
        /// </summary>
        public virtual bool CanReceive(int itemId) => true;

        /// <summary>
        /// false이면 MachineInteraction이 이 설비를 감지해도
        /// 선택 패널·외곽선·F키 힌트를 표시하지 않는다.
        /// </summary>
        public virtual bool ShowInteraction => true;

        public virtual void Receive(int itemId, int amount)
        {
            InputBuffer.Add(itemId, amount);
            NotifyBufferChanged();

            // 퀘스트 시스템 통지
            GameEvents.RaiseFacilityInput(FacilityId, itemId, amount);

            OnItemReceived(itemId, amount);
        }

        protected virtual void OnItemReceived(int itemId, int amount) { }

        public bool TryTakeOutput(int itemId, int amount)
        {
            if (!OutputBuffer.Consume(itemId, amount)) return false;
            NotifyBufferChanged();
            if (OutputBuffer.Stock.Count == 0)
            {
                SetStatus(MachineStatus.Idle);
                OnOutputCleared();   // 버퍼 완전 비움 → 생산 재개 시도
            }
            else
            {
                OnOutputDrained();   // 일부만 꺼냄(상한에서 자리가 남) → 생산 재개 시도
            }
            return true;
        }

        // ── 벨트 관리 ────────────────────────────────────────────────────

        /// <summary>출력 벨트를 목록에 추가한다. 중복 추가는 무시.</summary>
        public void AddOutputBelt(BeltSegment belt)
        {
            if (belt == null || outputBelts.Contains(belt)) return;
            outputBelts.Add(belt);
            // 인덱스가 범위를 벗어나지 않도록 보정
            if (_nextBeltIndex >= outputBelts.Count)
                _nextBeltIndex = 0;
        }

        /// <summary>출력 벨트를 목록에서 제거한다. 라운드 로빈 인덱스 보정 포함.</summary>
        public void RemoveOutputBelt(BeltSegment belt)
        {
            int idx = outputBelts.IndexOf(belt);
            if (idx < 0) return;
            outputBelts.RemoveAt(idx);
            // 제거 후 인덱스 범위 보정
            if (outputBelts.Count == 0)
                _nextBeltIndex = 0;
            else
                _nextBeltIndex = _nextBeltIndex % outputBelts.Count;
        }

        // ── 아이템 배출 ──────────────────────────────────────────────────

        protected void Dispatch(int itemId, int amount)
        {
            // 머리 칸이 비어 있는 준비된 벨트로 발송. 없으면 출력 버퍼에 쌓아 두고(역압) 대기.
            BeltSegment belt = FindFreeOutputBelt();
            if (belt != null && belt.TryTransport(itemId, amount))
                return;

            OutputBuffer.Add(itemId, amount);
            SetStatus(MachineStatus.OutputReady);
            NotifyBufferChanged();
        }

        /// <summary>라운드 로빈 순서로, 머리 칸이 비어 있고 연결된 출력 벨트를 찾는다(없으면 null).</summary>
        private BeltSegment FindFreeOutputBelt()
        {
            if (outputBelts.Count == 0) return null;
            for (int i = 0; i < outputBelts.Count; i++)
            {
                int idx = (_nextBeltIndex + i) % outputBelts.Count;
                var b = outputBelts[idx];
                if (b != null && b.IsReady && b.targetM != this && !b.IsOccupied)
                {
                    _nextBeltIndex = (idx + 1) % outputBelts.Count;
                    return b;
                }
            }
            return null;
        }

        /// <summary>
        /// OutputBuffer가 완전히 비워졌을 때 호출된다.
        /// 서브클래스에서 오버라이드해 후속 처리를 수행한다.
        /// </summary>
        protected virtual void OnOutputCleared() { }

        /// <summary>
        /// OutputBuffer에서 아이템 1개가 벨트로 빠져나가 자리가 났을 때 호출된다(완전히 비지 않아도).
        /// 서브클래스에서 오버라이드해 생산 재개 등에 사용한다.
        /// </summary>
        protected virtual void OnOutputDrained() { }

        /// <summary>
        /// OutputBuffer에 대기 중인 아이템이 있으면 머리 칸이 빈 벨트로 1개 내보낸다.
        /// 벨트의 머리 칸이 비는 순간(아이템이 한 칸 전진)마다 호출돼 역압을 따라 한 개씩 흐른다.
        /// </summary>
        public void TryDispatchPendingOutput()
        {
            if (OutputBuffer.Stock.Count == 0) return;

            BeltSegment belt = FindFreeOutputBelt();
            if (belt == null) return;   // 보낼 벨트의 머리 칸이 차 있음 → 그대로 대기(역압)

            int dispatchId = -1;
            foreach (var kv in OutputBuffer.Stock)
            {
                if (kv.Value <= 0) continue;
                dispatchId = kv.Key;
                break;
            }

            if (dispatchId < 0) return;
            if (!OutputBuffer.Consume(dispatchId, 1)) return;

            // 머리 칸이 빈 것을 FindFreeOutputBelt 가 보장하지만, 만일 실패하면 도로 버퍼에 넣어 유실 방지.
            if (!belt.TryTransport(dispatchId, 1))
            {
                OutputBuffer.Add(dispatchId, 1);
                return;
            }

            NotifyBufferChanged();

            // 재개 훅은 상태(Idle/OutputReady)를 먼저 정한 뒤에 호출한다.
            // 서브클래스가 생산을 재개하면 내부에서 Processing 으로 바꾸므로, 순서가 뒤바뀌면
            // 방금 켠 Processing 을 Idle 이 덮어쓴다(상태 오표시 버그) — 그래서 분기 끝에서 호출.
            if (OutputBuffer.Stock.Count == 0)
            {
                SetStatus(MachineStatus.Idle);
                OnOutputCleared();   // 버퍼 완전 비움 → 생산 재개 시도(서브클래스)
            }
            else
            {
                OnOutputDrained();   // 자리가 남(상한에 걸려 멈췄을 수 있음) → 생산 재개 시도
            }
        }

        // ── 연료 메서드 ───────────────────────────────────────────────────

        /// <summary>
        /// 연료 아이템 itemCount개를 투입한다.
        /// FuelConfig.secondsPerFuel 기준으로 가동 시간이 늘어난다.
        /// </summary>
        public void AddFuel(int itemCount)
        {
            var cfg = FuelConfig.Instance;
            float secs = cfg != null ? cfg.secondsPerFuel : 40f;
            FuelTimeRemaining += itemCount * secs;
            FuelItemCount     += itemCount;
            OnFuelChanged?.Invoke();
            GameEvents.RaiseFuelAdded(FacilityId);   // 튜토리얼 등 통지
        }

        /// <summary>
        /// 남은 연료를 아이템으로 회수한다. 소수점은 버림(부분 소모된 연료는 손실).
        /// 반환값: 회수된 아이템 개수.
        /// </summary>
        public int TakeFuel()
        {
            var cfg = FuelConfig.Instance;
            float secs = cfg != null ? cfg.secondsPerFuel : 40f;
            int count = Mathf.FloorToInt(FuelTimeRemaining / secs);
            if (count <= 0) return 0;

            FuelTimeRemaining -= count * secs;
            FuelTimeRemaining  = Mathf.Max(0f, FuelTimeRemaining);
            FuelItemCount      = Mathf.CeilToInt(FuelTimeRemaining / secs);
            OnFuelChanged?.Invoke();
            return count;
        }

        /// <summary>
        /// ProcessingMachine의 ProcessRoutine에서 매 프레임 호출.
        /// 연료가 소진되는 순간에만 OnFuelChanged를 발생시킨다.
        /// </summary>
        protected void ConsumeFuelDelta(float delta)
        {
            if (FuelTimeRemaining <= 0f) return;

            var cfg = FuelConfig.Instance;
            float secs = cfg != null ? cfg.secondsPerFuel : 40f;

            float before = FuelTimeRemaining;
            FuelTimeRemaining = Mathf.Max(0f, FuelTimeRemaining - delta);

            // 연료 개수 동기화 (남은 시간 기준으로 역산)
            FuelItemCount = Mathf.CeilToInt(FuelTimeRemaining / secs);

            // 연료 소진 순간만 이벤트 발생
            if (before > 0f && FuelTimeRemaining <= 0f)
                OnFuelChanged?.Invoke();
        }

        protected void SetStatus(MachineStatus s)
        {
            if (Status == s) return;
            Status = s;
            OnStatusChanged?.Invoke(s);
        }

        protected void NotifyBufferChanged() => OnBufferChanged?.Invoke();
        public void PublicNotifyBufferChanged() => OnBufferChanged?.Invoke();

        // ── 세이브 복원 전용 ──────────────────────────────────────────────
        // 진행 중이던 정확한 제작 시점(코루틴 elapsed)까지는 복원하지 않는다 — 버퍼/연료만
        // 직접 채워두면, 재료가 충분할 때 ProcessingMachine이 레시피 로드 이후 스스로 생산을
        // 재개한다(이벤트/알림 없이 조용히).
        public void RestoreBuffers(List<ItemStackData> input, List<ItemStackData> output, float fuelSecondsRemaining)
        {
            if (input != null)
                foreach (var s in input) InputBuffer.Add(s.itemId, s.amount);
            if (output != null)
                foreach (var s in output) OutputBuffer.Add(s.itemId, s.amount);

            FuelTimeRemaining = Mathf.Max(0f, fuelSecondsRemaining);
            var cfg = FuelConfig.Instance;
            float secs = cfg != null ? cfg.secondsPerFuel : 40f;
            FuelItemCount = Mathf.CeilToInt(FuelTimeRemaining / secs);
        }
    }
}