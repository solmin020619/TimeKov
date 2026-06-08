using UnityEngine;

/// <summary>
/// 이 UI 요소를 튜토리얼 스포트라이트 타깃으로 등록.
/// id 로 ContinueObjective.spotlightTargetId 와 매칭된다.
/// 예) 머신 재료슬롯에 붙이고 id="machine_input" → 그 슬롯을 강조.
/// 활성/비활성에 맞춰 자동 등록/해제 (UI 열릴 때만 강조 대상이 됨).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TutorialHighlightTarget : MonoBehaviour
{
    [Tooltip("스포트라이트가 찾는 고유 id (예: machine_input, build_quickslot, core_upgrade_btn, fuel_slot)")]
    [SerializeField] private string id;

    private RectTransform _rect;

    private void Awake() => _rect = (RectTransform)transform;
    private void OnEnable() => TutorialOverlay.RegisterTarget(id, _rect);
    private void OnDisable() => TutorialOverlay.UnregisterTarget(id, _rect);
}
