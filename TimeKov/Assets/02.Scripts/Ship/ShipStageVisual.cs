using UnityEngine;

// 폐우주선의 단계별 월드 표시. Lv.1~(N-1) = 파손(공사 펜스 + 파손 모델/임시 큐브),
// Lv.N(최종) = 펜스 제거 + 완성 우주선 모델 노출.
// ShipRepairManager.OnChanged 를 구독해 레벨에 따라 오브젝트 on/off 한다.
// (중간 단계별 모델 변화/작업등/용접 이펙트는 기획상 필수 제외 — 여기선 최종 노출만 처리.)
public class ShipStageVisual : MonoBehaviour
{
    [Tooltip("Lv.1~(N-1) 동안 켜둘 공사 펜스 루트.")]
    [SerializeField] private GameObject fenceRoot;
    [Tooltip("Lv.1~(N-1) 동안 켜둘 파손 우주선(임시 큐브 등).")]
    [SerializeField] private GameObject wreckModel;
    [Tooltip("Lv.N 최종 수리 완료 시 켤 완성 우주선 모델.")]
    [SerializeField] private GameObject finishedModel;

    private void OnEnable()  => ShipRepairManager.OnChanged += Refresh;
    private void OnDisable() => ShipRepairManager.OnChanged -= Refresh;

    private void Start() => Refresh();

    private void Refresh()
    {
        var mgr = ShipRepairManager.Instance;
        bool finished = mgr != null && mgr.IsFullyRepaired;

        if (fenceRoot     != null) fenceRoot.SetActive(!finished);
        if (wreckModel    != null) wreckModel.SetActive(!finished);
        if (finishedModel != null) finishedModel.SetActive(finished);
    }
}
