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
        // 설비 이름(UI 표시용) - Start 에서 시트 facilityName 으로 세팅한다.
        // 시트가 원본이라 인스펙터 노출/직렬화 안 함(ProcessingMachine 과 같은 방식).
        // 프리팹에 "창고 추출기" 가 박혀 있어 시트의 이름과 두 개로 갈라져 있던 것을 시트 하나로 합쳤다.
        // ★ProcessingMachine 과 같은 이유로 늦게라도 채운다 - Start 시점에 DataBoot 이 아직이면
        //   폴백 이름이 화면에 캐시돼 남는다.
        private string machineName;
        public override string MachineName
        {
            get
            {
                if (string.IsNullOrEmpty(machineName)) LoadNameFromSheet();
                return !string.IsNullOrEmpty(machineName) ? machineName : base.MachineName;
            }
        }

        [Header("추출 설정")]
        [Tooltip("아이템 1개를 추출하는 주기 (초)")]
        public float extractInterval = 3f;

        // ── 상태 ────────────────────────────────────────────────────────

        private int   _selectedItemId = -1;
        private float _timer;

        public int   SelectedItemId  => _selectedItemId;
        public float ExtractInterval => extractInterval;
        public float TimerRemaining  => extractInterval - _timer;

        /// <summary>출력 벨트가 하나라도 연결돼 있는지. 연결이 없으면 추출하지 않는다.</summary>
        public bool HasOutputBelt
        {
            get
            {
                for (int i = 0; i < outputBelts.Count; i++)
                    if (outputBelts[i] != null) return true;
                return false;
            }
        }

        /// <summary>선택 아이템이 바뀔 때 발생.</summary>
        public event Action OnSelectionChanged;

        // ── 초기화 ──────────────────────────────────────────────────────

        protected override void Start()
        {
            base.Start();   // 월드 표시(FacilityWorldDisplay) 자동 부착

            // 이름은 시트가 원본. 데이터가 이미 로드됐으면 즉시, 아직이면 로드 완료 시점에 읽는다.
            if (DataBoot.IsLoaded) LoadNameFromSheet();
            else DataBoot.OnDataLoaded += OnDataLoaded;
        }

        private void OnDestroy()
        {
            DataBoot.OnDataLoaded -= OnDataLoaded;
        }

        private void OnDataLoaded()
        {
            DataBoot.OnDataLoaded -= OnDataLoaded;
            LoadNameFromSheet();
        }

        private void LoadNameFromSheet()
        {
            int fid = FacilityId;
            if (fid <= 0) return;

            var facility = GameDataUtility.GetFacility(fid);
            if (facility != null && !string.IsNullOrEmpty(facility.facilityName))
                machineName = facility.facilityName;
        }

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

            // 출력 벨트가 연결돼 있지 않으면 추출하지 않는다 (타이머도 멈춤)
            if (!HasOutputBelt) return;

            // OutputBuffer에 아이템이 남아있으면 대기 (벨트가 처리할 때까지)
            if (OutputBuffer.Stock.Count > 0) return;

            // 창고에 선택 아이템 재고가 없으면 추출 대기 - 타이머/게이지 정지(빈 채로).
            // (이거 없으면 재고 0이라 TryExtract 가 매번 헛돌아도 게이지만 계속 차오르는 버그)
            var storage = InventoryManager.StorageInstance;
            if (storage == null || storage.GetTotalItemCount(_selectedItemId) <= 0)
            {
                _timer = 0f;
                return;
            }

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
