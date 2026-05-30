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

        /// <summary>
        /// 이 설비가 해당 아이템을 받을 수 있는지 확인한다.
        /// ProcessingMachine 등의 서브클래스에서 레시피 기반으로 오버라이드한다.
        /// 기본값은 true (무조건 수락).
        /// </summary>
        public virtual bool CanReceive(int itemId) => true;

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
                OnOutputCleared();
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
            // 라운드 로빈: 현재 순번 벨트로 시도, 인덱스는 항상 진행
            BeltSegment belt = GetCurrentOutputBelt();
            AdvanceOutputBeltIndex();

            if (belt != null && belt.IsReady && !belt.IsBusy && belt.targetM != this)
                belt.TryTransport(itemId, amount);
            else
            {
                OutputBuffer.Add(itemId, amount);
                SetStatus(MachineStatus.OutputReady);
                NotifyBufferChanged();
            }
        }

        /// <summary>현재 라운드 로빈 순번의 벨트를 반환한다.</summary>
        private BeltSegment GetCurrentOutputBelt()
        {
            if (outputBelts.Count == 0) return null;
            return outputBelts[_nextBeltIndex % outputBelts.Count];
        }

        /// <summary>라운드 로빈 인덱스를 다음으로 진행한다.</summary>
        private void AdvanceOutputBeltIndex()
        {
            if (outputBelts.Count == 0) { _nextBeltIndex = 0; return; }
            _nextBeltIndex = (_nextBeltIndex + 1) % outputBelts.Count;
        }

        /// <summary>OutputBuffer 대기 아이템을 사용 가능한 벨트로 발송한다.</summary>
        private BeltSegment FindAvailableOutputBelt()
        {
            for (int i = 0; i < outputBelts.Count; i++)
            {
                var b = outputBelts[i];
                if (b != null && b.IsReady && !b.IsBusy && b.targetM != this)
                    return b;
            }
            return null;
        }

        /// <summary>
        /// OutputBuffer가 완전히 비워졌을 때 호출된다.
        /// 서브클래스에서 오버라이드해 후속 처리를 수행한다.
        /// </summary>
        protected virtual void OnOutputCleared() { }

        /// <summary>
        /// OutputBuffer에 대기 중인 아이템이 있을 때 사용 가능한 벨트로 내보낸다.
        /// 벨트가 아이템 전달 완료 후 호출한다.
        /// </summary>
        public void TryDispatchPendingOutput()
        {
            if (OutputBuffer.Stock.Count == 0) return;

            // 버퍼 드레인은 여유 있는 벨트 아무거나 사용 (라운드 로빈 인덱스 비소모)
            BeltSegment belt = FindAvailableOutputBelt();
            if (belt == null) return;

            int dispatchId = -1;
            foreach (var kv in OutputBuffer.Stock)
            {
                if (kv.Value <= 0) continue;
                dispatchId = kv.Key;
                break;
            }

            if (dispatchId < 0) return;
            // 스택된 아이템은 1개씩 순차 발송 — 벨트 완료 후 2초 딜레이와 맞물려 하나씩 이동
            if (!OutputBuffer.Consume(dispatchId, 1)) return;

            belt.TryTransport(dispatchId, 1);
            NotifyBufferChanged();

            if (OutputBuffer.Stock.Count == 0)
            {
                SetStatus(MachineStatus.Idle);
                OnOutputCleared();
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
    }
}