using System;
using UnityEngine;

namespace TIMEKOV.Factory
{
    /// <summary>
    /// 창고 → 설비 추출기.
    /// 선택한 아이템 1개를 extractInterval(초)마다 창고에서 꺼내 벨트로 전송한다.
    /// 연료 불필요.
    /// </summary>
    public class StorageExtractor : MachineBase
    {
        [Header("설비 이름 (UI 표시용)")]
        public string machineName = "창고 추출기";
        public override string MachineName => !string.IsNullOrEmpty(machineName) ? machineName : base.MachineName;

        [Header("추출 설정")]
        [Tooltip("아이템 1개를 추출하는 주기 (초)")]
        public float extractInterval = 3f;

        // ── 상태 ────────────────────────────────────────────────────────

        private int   _selectedItemId = -1;
        private float _timer;

        public int   SelectedItemId  => _selectedItemId;
        public float ExtractInterval => extractInterval;
        public float TimerRemaining  => extractInterval - _timer;

        /// <summary>선택 아이템이 바뀔 때 발생.</summary>
        public event Action OnSelectionChanged;

        // ── 공개 메서드 ──────────────────────────────────────────────────

        /// <summary>추출할 아이템 ID를 설정한다.</summary>
        public void SetTargetItem(int itemId)
        {
            _selectedItemId = itemId;
            _timer = 0f; // 선택 바꾸면 타이머 리셋
            OnSelectionChanged?.Invoke();
        }

        // StorageExtractor는 벨트에서 아이템을 받지 않는다
        public override bool CanReceive(int itemId) => false;

        // ── 매 프레임 ────────────────────────────────────────────────────

        private void Update()
        {
            if (_selectedItemId <= 0) return;

            // OutputBuffer에 아이템이 남아있으면 대기 (벨트가 처리할 때까지)
            if (OutputBuffer.Stock.Count > 0) return;

            _timer += Time.deltaTime;
            if (_timer >= extractInterval)
            {
                _timer = 0f;
                TryExtract();
            }
        }

        private void TryExtract()
        {
            var storage = InventoryManager.StorageInstance;
            if (storage == null) return;
            if (storage.GetTotalItemCount(_selectedItemId) <= 0) return;

            if (!storage.TryConsumeItem(_selectedItemId, 1)) return;

            storage.ForceRefreshUI();
            Dispatch(_selectedItemId, 1);
            NotifyBufferChanged();
        }
    }
}
