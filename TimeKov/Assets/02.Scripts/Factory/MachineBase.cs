// =====================================================================
// MachineBase.cs
// 모든 공장 설비가 상속하는 기반 클래스.
//
// 역할:
//   - 입력 버퍼(inputBuffer) / 출력 버퍼(outputBuffer) 보유
//   - 컨베이어 벨트 연결 감지 → 상태(MachineStatus) 관리
//   - 아이템 수신(Receive) / 배출(Dispatch) 공통 로직
//   - UI 갱신은 OnStatusChanged 이벤트로 외부에 위임
//
// 각 설비는 ProcessingMachine(가공형) 또는 이 클래스를 직접 상속.
// =====================================================================

using System;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public enum MachineStatus
    {
        Disconnected,  // 🔴 벨트 끊김
        Idle,          // ⚪ 연결됨, 재료 대기
        Processing,    // 🟢 가공 중
        OutputFull,    // 🟡 출력 버퍼 꽉참
    }

    public abstract class MachineBase : MonoBehaviour
    {
        // ---------------------------------------------------------------
        // Inspector
        // ---------------------------------------------------------------
        [Header("버퍼 용량")]
        public int inputCapacity  = 100;
        public int outputCapacity = 100;

        // ---------------------------------------------------------------
        // 버퍼
        // ---------------------------------------------------------------
        protected readonly ItemBuffer inputBuffer  = new();
        protected readonly ItemBuffer outputBuffer = new();

        // ---------------------------------------------------------------
        // 상태
        // ---------------------------------------------------------------
        public MachineStatus Status { get; private set; } = MachineStatus.Disconnected;

        // 입출력 벨트 연결 수 (연결된 벨트가 1개 이상이어야 동작)
        protected int inputConnections  = 0;
        protected int outputConnections = 0;

        // ---------------------------------------------------------------
        // 이벤트 (UI가 구독)
        // ---------------------------------------------------------------
        public event Action<MachineStatus> OnStatusChanged;

        // ---------------------------------------------------------------
        // 벨트 연결 콜백 (ConveyorBelt.Start()에서 호출)
        // ---------------------------------------------------------------
        public void NotifyInputConnected(ConveyorBelt belt)
        {
            inputConnections++;
            RefreshStatus();
        }

        public void NotifyOutputConnected(ConveyorBelt belt)
        {
            outputConnections++;
            RefreshStatus();
        }

        // ---------------------------------------------------------------
        // 아이템 수신 (컨베이어 벨트가 호출)
        // ---------------------------------------------------------------
        public virtual void Receive(string itemId, int amount)
        {
            inputBuffer.Add(itemId, amount);
            OnReceived(itemId, amount);
        }

        // 수신 후 각 설비가 추가 처리를 원할 때 오버라이드
        protected virtual void OnReceived(string itemId, int amount) { }

        // ---------------------------------------------------------------
        // 출력 버퍼 → 연결된 벨트로 배출
        // ---------------------------------------------------------------
        protected void Dispatch(ConveyorBelt belt, string itemId, int amount)
        {
            if (belt == null || !belt.IsConnected)
            {
                outputBuffer.Add(itemId, amount); // 벨트 없으면 버퍼 대기
                SetStatus(MachineStatus.OutputFull);
                return;
            }
            belt.TryTransport(itemId, amount);
        }

        // ---------------------------------------------------------------
        // 상태 관리
        // ---------------------------------------------------------------
        protected void SetStatus(MachineStatus s)
        {
            if (Status == s) return;
            Status = s;
            OnStatusChanged?.Invoke(s);
        }

        protected void RefreshStatus()
        {
            if (inputConnections == 0 && outputConnections == 0)
                SetStatus(MachineStatus.Disconnected);
            else if (Status == MachineStatus.Disconnected)
                SetStatus(MachineStatus.Idle);
        }
    }
}
