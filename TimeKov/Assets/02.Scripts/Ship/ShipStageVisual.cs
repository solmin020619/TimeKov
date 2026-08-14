using UnityEngine;

// 폐우주선의 단계별 월드 표시.
//   수리 레벨이 오를수록 우주선 모델이 바뀐다 — 유저가 밖에서 수리 진행을 눈으로 본다.
//   ★공사 펜스는 폐지했다(우주선을 가려서 수리되는 게 안 보였다). fenceRoot 를 물려두면 항상 끈다.
//
//   레벨 구간은 모델 개수로 '균등 분배'한다. 단계 수나 모델 수가 바뀌어도 코드를 안 고쳐도 된다.
//     예) 10단계 · 모델 5개 → 2단계마다 교체
//         Lv1-2 → 1번,  Lv3-4 → 2번,  Lv5-6 → 3번,  Lv7-8 → 4번,  Lv9-10 → 5번
//
//   ShipRepairManager.OnChanged 를 구독하므로 수리 즉시 반영된다.
//   세팅: Tools ▸ TIMEKOV ▸ 우주선 단계별 모델 세팅 (프리팹 5개를 자식으로 깔고 자동 연결)
public class ShipStageVisual : MonoBehaviour
{
    [Tooltip("단계별 모델. 낮은 단계 → 높은 단계 순서로 넣는다(Spaceship_1 … _5).\n" +
             "레벨 구간은 개수에 맞춰 자동 분배된다.")]
    [SerializeField] private GameObject[] stageModels;

    [Tooltip("예전 공사 펜스. 물려두면 항상 꺼진다 — 이제 우주선을 가리지 않는다.\n" +
             "펜스 오브젝트를 아예 지웠다면 비워두면 된다.")]
    [SerializeField] private GameObject fenceRoot;

    [Tooltip("외형이 바뀔 때 재생할 연출(페이드 → 고정 카메라로 새 외형 → 복귀).\n" +
             "비우면 같은 오브젝트/씬에서 자동으로 찾는다. 없으면 연출 없이 즉시 교체된다.")]
    [SerializeField] private ShipStageCinematic cinematic;

    /// 지금 보이는 모델의 인덱스(-1 = 아직 결정 전). 디버그/연출 연동용.
    public int CurrentStageIndex { get; private set; } = -1;

    private void OnEnable()  => ShipRepairManager.OnChanged += Refresh;
    private void OnDisable() => ShipRepairManager.OnChanged -= Refresh;

    private void Start() => Refresh();

    private void Refresh()
    {
        if (fenceRoot != null) fenceRoot.SetActive(false);
        if (stageModels == null || stageModels.Length == 0) return;

        var mgr = ShipRepairManager.Instance;
        int level = mgr != null ? mgr.CurrentLevel : 1;
        int max   = mgr != null ? mgr.MaxLevel     : stageModels.Length;

        int next = StageIndexFor(level, max, stageModels.Length);
        if (next == CurrentStageIndex) return;   // 같은 구간 안에서의 수리 — 외형 변화 없음

        // 첫 표시(게임 시작·로드)는 연출 없이 바로 맞춘다. 실제로 '바뀌는' 순간에만 보여준다.
        bool first = CurrentStageIndex < 0;
        var cine = ResolveCinematic();

        if (!first && cine != null && Application.isPlaying)
        {
            var from = stageModels[CurrentStageIndex];
            var to   = stageModels[next];
            cine.Play(from, to, () => ApplyStage(next));
        }
        else
        {
            ApplyStage(next);
        }
    }

    private void ApplyStage(int index)
    {
        CurrentStageIndex = index;
        for (int i = 0; i < stageModels.Length; i++)
            if (stageModels[i] != null) stageModels[i].SetActive(i == index);
    }

    private ShipStageCinematic ResolveCinematic()
    {
        if (cinematic != null) return cinematic;
        cinematic = GetComponent<ShipStageCinematic>();
        if (cinematic == null) cinematic = FindFirstObjectByType<ShipStageCinematic>();
        return cinematic;
    }

    /// 레벨(1~maxLevel)을 모델 개수로 균등 분배한 인덱스.
    ///   10단계·5모델이면 2단계마다 한 번 바뀐다. (에디터 툴에서도 미리보기용으로 쓴다)
    public static int StageIndexFor(int level, int maxLevel, int modelCount)
    {
        if (modelCount <= 1 || maxLevel <= 1) return 0;
        int i = (Mathf.Clamp(level, 1, maxLevel) - 1) * modelCount / maxLevel;
        return Mathf.Clamp(i, 0, modelCount - 1);
    }
}
