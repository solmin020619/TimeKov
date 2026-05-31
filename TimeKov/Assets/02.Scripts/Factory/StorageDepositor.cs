using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TIMEKOV.Factory
{
    /// <summary>
    /// 설비 → 창고 적재기.
    /// 벨트로 들어온 아이템을 버퍼에 쌓았다가 depositInterval(초)마다 창고로 일괄 전송한다.
    /// 연료가 있을 때만 카운트다운 진행 (연료 소진 시 일시정지).
    /// </summary>
    public class StorageDepositor : MachineBase
    {
        [Header("설비 이름 (UI 표시용)")]
        public string machineName = "창고 적재기";
        public override string MachineName => !string.IsNullOrEmpty(machineName) ? machineName : base.MachineName;

        [Header("적재 설정")]
        [Tooltip("창고로 전송하는 주기 (초)")]
        public float depositInterval = 5f;
        [Tooltip("InputBuffer 최대 보관 아이템 수")]
        public int maxBuffer = 100;

        // ── 상태 ─────────────────────────────────────────────────────────

        private float _depositTimer;
        public float DepositInterval   => depositInterval;
        public float NextDepositTime   => _depositTimer;

        // ── 초기화 ────────────────────────────────────────────────────────

        private void Start()
        {
            _depositTimer = depositInterval;
            StartCoroutine(DepositRoutine());
        }

        private void OnDisable() => StopAllCoroutines();

        // ── 수신 제어 ────────────────────────────────────────────────────

        /// <summary>버퍼 한도 내에서 모든 아이템 수락.</summary>
        public override bool CanReceive(int itemId)
        {
            int total = 0;
            foreach (var kv in InputBuffer.Stock)
                total += kv.Value;
            return total < maxBuffer;
        }

        // ── 적재 루틴 ────────────────────────────────────────────────────

        private IEnumerator DepositRoutine()
        {
            while (true)
            {
                _depositTimer = depositInterval;

                while (_depositTimer > 0f)
                {
                    // 버퍼에 아이템이 있을 때만 연료 소모 & 연료 체크
                    if (InputBuffer.Stock.Count > 0)
                    {
                        if (!HasFuel)
                        {
                            SetStatus(MachineStatus.NoFuel);
                            while (!HasFuel) yield return null;
                        }

                        ConsumeFuelDelta(Time.deltaTime);
                        SetStatus(MachineStatus.Processing);
                    }
                    else
                    {
                        SetStatus(MachineStatus.Idle);
                    }

                    _depositTimer -= Time.deltaTime;
                    yield return null;
                }

                DepositToStorage();
            }
        }

        private void DepositToStorage()
        {
            var storage = InventoryManager.StorageInstance;
            if (storage == null || InputBuffer.Stock.Count == 0)
            {
                SetStatus(MachineStatus.Idle);
                return;
            }

            // 버퍼 복사 후 전송 (foreach 중 수정 방지)
            var snapshot = new Dictionary<int, int>(InputBuffer.Stock);
            foreach (var kv in snapshot)
            {
                if (kv.Value <= 0) continue;
                if (!InputBuffer.Consume(kv.Key, kv.Value)) continue;

                storage.AddItem(kv.Key, kv.Value);
                GameEvents.RaiseFacilityProcessComplete(FacilityId, kv.Key, kv.Value);
            }

            storage.ForceRefreshUI();
            SetStatus(MachineStatus.Idle);
            NotifyBufferChanged();
        }
    }
}
