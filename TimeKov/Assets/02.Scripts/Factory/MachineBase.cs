using System;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public enum MachineStatus { Disconnected, Idle, Processing, OutputReady }

    public abstract class MachineBase : MonoBehaviour
    {
        public readonly ItemBuffer InputBuffer = new();
        public readonly ItemBuffer OutputBuffer = new();

        public MachineStatus Status { get; protected set; } = MachineStatus.Idle;

        public event Action OnBufferChanged;
        public event Action<MachineStatus> OnStatusChanged;

        [HideInInspector] public BeltSegment outputBelt;
        [HideInInspector] public BeltSegment inputBelt;

        [Header("입출구 포트")]
        public Transform outputPort;
        public Transform inputPort;

        int _facilityIdCache = -1;
        protected int FacilityId
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
            return true;
        }

        protected void Dispatch(int itemId, int amount)
        {
            if (outputBelt != null && outputBelt.IsReady && outputBelt.targetM != this)
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

        protected void NotifyBufferChanged() => OnBufferChanged?.Invoke();
        public void PublicNotifyBufferChanged() => OnBufferChanged?.Invoke();
    }
}