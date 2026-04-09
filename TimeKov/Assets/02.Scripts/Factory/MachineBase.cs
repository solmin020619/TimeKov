using System;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public enum MachineStatus { Disconnected, Idle, Processing, OutputReady }

    public abstract class MachineBase : MonoBehaviour
    {
        public readonly ItemBuffer InputBuffer  = new();
        public readonly ItemBuffer OutputBuffer = new();

        public MachineStatus Status { get; protected set; } = MachineStatus.Idle;

        public event Action OnBufferChanged;
        public event Action<MachineStatus> OnStatusChanged;

        // BeltSegment가 자동으로 세팅
        [HideInInspector] public BeltSegment outputBelt;
        [HideInInspector] public BeltSegment inputBelt;

        public virtual void Receive(int itemId, int amount)
        {
            InputBuffer.Add(itemId, amount);
            NotifyBufferChanged();
            OnItemReceived(itemId, amount);
        }

        protected virtual void OnItemReceived(int itemId, int amount) { }

        public bool TryTakeOutput(int itemId, int amount)
        {
            if (!OutputBuffer.Consume(itemId, amount)) return false;
            NotifyBufferChanged();
            return true;
        }

        protected void Dispatch(int itemId, int amount)
        {
            if (outputBelt != null && outputBelt.IsReady)
                outputBelt.TryTransport(itemId, amount);
            else
            {
                OutputBuffer.Add(itemId, amount);
                SetStatus(MachineStatus.OutputReady);
                NotifyBufferChanged();
            }
        }

        protected void SetStatus(MachineStatus s)
        {
            if (Status == s) return;
            Status = s;
            OnStatusChanged?.Invoke(s);
        }

        // protected → MachineUI에서 입력 반환 시 호출용
        protected void NotifyBufferChanged() => OnBufferChanged?.Invoke();

        // MachineUI에서 직접 InputBuffer.Consume 후 호출하는 public 래퍼
        public void PublicNotifyBufferChanged() => OnBufferChanged?.Invoke();
    }
}
