using System.Collections.Generic;
using UnityEngine;

// 도감 몬스터 프리뷰 설정.
// CodexUI는 런타임 생성이라 인스펙터가 없으므로, 이 SO를
// Resources/Codex/CodexPreviewConfig 에 두면 도감이 자동 로드한다.
// 종욱이 8종 엔트리에 프리팹 연결 + 흉상 프레이밍(오프셋)만 잡으면 됨.
// state는 임시값(테스트용) - 나중에 실제 해금 데이터로 대체.
[CreateAssetMenu(fileName = "CodexPreviewConfig", menuName = "TIMEKOV/Codex Preview Config")]
public class CodexPreviewConfig : ScriptableObject
{
    public enum RevealState { Hidden, Silhouette, Revealed }

    [System.Serializable]
    public class MonsterEntry
    {
        public string displayName = "몬스터";
        public GameObject prefab;                            // 05.Prefabs/Enemy/Enemy_*
        public RevealState state = RevealState.Revealed;     // 임시 - 나중에 실제 해금 데이터로 대체

        [Header("프레이밍 (흉상)")]
        [Tooltip("좌우 회전(도). 0 = 정면")]
        public float yaw = 20f;
        [Tooltip("위아래 기울기(도). 양수 = 살짝 내려다봄")]
        public float pitch = 8f;
        [Tooltip("바운드에서 겨냥 높이. 1 = 머리/위쪽, 0.5 = 중앙. 흉상은 0.7 근처")]
        [Range(0f, 1f)] public float aimHeight = 0.72f;
        [Tooltip("1 = 전체가 칸에 꽉 참. 작을수록 줌인(흉상으로 당김)")]
        public float fillScale = 0.7f;
        [Tooltip("프레임 내 수평 위치(월드 단위). + = 왼쪽으로, - = 오른쪽으로. 모델이 한쪽 치우치면 보정")]
        public float xOffset = 0f;

        [Tooltip("프리뷰에서 강제로 재생할 애니 상태 이름(layer 0). 비워두면 기본 상태 재생.\n" +
                 "화염보스처럼 기본 상태가 '등장/상승' 클립이라 프리뷰가 떠오르는 경우 'Idle' 지정.")]
        public string previewAnimState = "";
    }

    public List<MonsterEntry> monsters = new List<MonsterEntry>();
}
