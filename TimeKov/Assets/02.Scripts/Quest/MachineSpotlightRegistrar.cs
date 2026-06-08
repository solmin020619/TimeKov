using System.Collections.Generic;
using UnityEngine;
using TIMEKOV.Factory;

// 설비 UI(머신)가 열릴 때 연료/재료/출력(받기) 슬롯을 튜토리얼 스포트라이트 타깃으로 자동 등록.
// 씬/프리팹 수동 부착 불필요, 공장 코드 비침습. GameEvents.OnFacilityInteract 훅 + 패널 닫힘 폴링.
public class MachineSpotlightRegistrar : MonoBehaviour
{
    const string FuelId   = "fuel_slot";
    const string InputId  = "machine_input";
    const string OutputId = "machine_output";

    static MachineSpotlightRegistrar _i;

    MachineUI _ui;
    RectTransform _fuel, _output;
    readonly List<RectTransform> _inputs = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_i != null) return;
        var go = new GameObject("[MachineSpotlightRegistrar]");
        DontDestroyOnLoad(go);
        _i = go.AddComponent<MachineSpotlightRegistrar>();
    }

    void OnEnable()  { GameEvents.OnFacilityInteract += OnFacilityOpened; }
    void OnDisable() { GameEvents.OnFacilityInteract -= OnFacilityOpened; }
    void OnDestroy() { GameEvents.OnFacilityInteract -= OnFacilityOpened; if (_i == this) _i = null; }

    // 설비 UI 열림 통지 — 이 시점엔 OpenFor가 끝나 슬롯이 빌드+활성 상태.
    void OnFacilityOpened(int facilityId)
    {
        _ui = FindAnyObjectByType<MachineUI>(FindObjectsInactive.Include);
        if (_ui == null) return;

        _fuel = _ui.fuelDropSlot != null ? _ui.fuelDropSlot.GetComponent<RectTransform>() : null;
        // 출력 슬롯은 결과물 없으면 비활성화됨 → 항상 켜져있는 "모두 받기" 버튼을 타깃으로.
        _output = _ui.takeOutputBtn != null ? _ui.takeOutputBtn.GetComponent<RectTransform>() : null;

        if (_fuel != null)   TutorialOverlay.RegisterTarget(FuelId, _fuel);
        if (_output != null) TutorialOverlay.RegisterTarget(OutputId, _output);

        // machine_input = 재료 슬롯 전부 (두 칸 다 강조 — 합집합 영역).
        _inputs.Clear();
        if (_ui.recipeDropSlots != null)
            foreach (var slot in _ui.recipeDropSlots)
            {
                if (slot == null) continue;
                var rt = slot.GetComponent<RectTransform>();
                if (rt == null) continue;
                _inputs.Add(rt);
                TutorialOverlay.RegisterTarget(InputId, rt);
            }
    }

    void Update()
    {
        // 패널이 닫히면 해제 (OnFacilityClose 이벤트가 없어 상태 폴링).
        if (_ui == null) return;
        if (_ui.uiPanel != null && _ui.uiPanel.activeInHierarchy) return;
        UnregisterAll();
    }

    void UnregisterAll()
    {
        if (_fuel != null)   TutorialOverlay.UnregisterTarget(FuelId, _fuel);
        if (_output != null) TutorialOverlay.UnregisterTarget(OutputId, _output);
        foreach (var rt in _inputs) TutorialOverlay.UnregisterTarget(InputId, rt);
        _inputs.Clear();
        _fuel = _output = null;
        _ui = null;
    }
}
