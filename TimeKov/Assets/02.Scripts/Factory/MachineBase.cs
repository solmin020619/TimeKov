// =====================================================================
// MachineBase.cs
// 모든 공장 설비 기반 클래스.
// int itemId 기반으로 DataManager/InventoryManager와 완전 연동.
// =====================================================================

using System;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public enum MachineStatus
    {
        Disconnected,   // 🔴 벨트 없음
        Idle,           // ⚪ 대기
        Processing,     // 🟢 가공 중
        OutputReady,    // 🔵 출력 대기
    }

    public abstract class MachineBase : MonoBehaviour
    {
        // ── 버퍼 ───────────────────────────────────────────────────
        public readonly ItemBuffer InputBuffer  = new();
        public readonly ItemBuffer OutputBuffer = new();

        // ── 상태 ───────────────────────────────────────────────────
        public MachineStatus Status { get; protected set; } = MachineStatus.Idle;

        // ── 이벤트 (UI / 벨트가 구독) ──────────────────────────────
        public event Action OnBufferChanged;   // 버퍼 내용 바뀔 때
        public event Action<MachineStatus> OnStatusChanged;

        // ── 연결된 출력 벨트 ───────────────────────────────────────
        // ProcessingMachine이 결과물 배출 시 사용
        [HideInInspector] public ConveyorBelt outputBelt;

        // ── 수신 (벨트 또는 플레이어가 직접 호출) ──────────────────
        public virtual void Receive(int itemId, int amount)
        {
            InputBuffer.Add(itemId, amount);
            NotifyBufferChanged();
            OnItemReceived(itemId, amount);
        }

        protected virtual void OnItemReceived(int itemId, int amount) { }

        // ── 출력 버퍼에서 꺼내기 (플레이어 수동 회수) ──────────────
        public bool TryTakeOutput(int itemId, int amount)
        {
            if (!OutputBuffer.Consume(itemId, amount)) return false;
            NotifyBufferChanged();
            return true;
        }

        // ── 출력 버퍼 → 벨트 배출 ──────────────────────────────────
        protected void Dispatch(int itemId, int amount)
        {
            if (outputBelt != null && outputBelt.IsConnected)
            {
                outputBelt.TryTransport(itemId, amount);
            }
            else
            {
                // 벨트 없으면 출력 버퍼에 쌓아둠 (플레이어가 수동 회수)
                OutputBuffer.Add(itemId, amount);
                SetStatus(MachineStatus.OutputReady);
                NotifyBufferChanged();
            }
        }

        // ── 상태 변경 ───────────────────────────────────────────────
        protected void SetStatus(MachineStatus s)
        {
            if (Status == s) return;
            Status = s;
            OnStatusChanged?.Invoke(s);
        }

        protected void NotifyBufferChanged() => OnBufferChanged?.Invoke();
    }
}
