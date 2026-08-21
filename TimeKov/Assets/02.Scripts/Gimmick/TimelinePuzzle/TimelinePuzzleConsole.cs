// =====================================================================
// TimelinePuzzleConsole.cs
// 월드에 놓는 '시간선 복원' 장치. 상호작용하면 퍼즐 UI 가 열리고, 풀면 연결된
// GimmickTarget(결계/문/다리 등)이 열린다.
//
// [기존 프레임에 얹었다]
//   GimmickTrigger 를 상속하므로 targets 배선·latch 처리·해제 효과가 그대로 재사용된다.
//   퍼즐이 풀리는 순간 SetSatisfied(true) 만 부르면 나머지는 프레임이 알아서 한다.
//
// [판 만들기]
//   시작·끝점은 인스펙터에서 칸 좌표로 지정한다. 통로마다 다른 판을 두면 난이도 곡선이 생긴다.
//   ★행+열이 짝수인 칸끼리만 이어진다. 5×5 는 그 칸이 13개인데, 25칸을 한 번씩 밟는
//     경로는 색을 번갈아 밟으므로 시작과 끝이 모두 그 13칸 안에 있어야 한다.
//     (홀수 칸을 지정하면 아무리 해도 안 풀리는 판이 된다 — OnValidate 가 경고한다)
//   전수 조사 결과 그 13칸 중 어떤 두 칸을 골라도 전부 풀린다. 즉 이 검사만으로 충분하다.
//   해의 개수로 본 난이도:
//     104 (0,0)-(4,4)   지그재그만으로 풀림 · 가장 쉬움
//      86 (0,0)-(0,4)
//      58 (0,0)-(2,4)
//      34 (0,0)-(2,0)   기본값 · 판 전체를 돌아 되돌아와야 한다
//      28 (0,2)-(2,0)   가장 어려움
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

public class TimelinePuzzleConsole : GimmickTrigger, IInteractable, IInteractHint, ISaveable
{
    public const int Size = 5;

    [Header("퍼즐 판")]
    [Tooltip("시작 칸 (행, 열). 0부터. 행+열이 짝수여야 한다.")]
    [SerializeField] Vector2Int startCell = new Vector2Int(0, 0);

    [Tooltip("도착 칸 (행, 열). 행+열이 짝수여야 한다.")]
    [SerializeField] Vector2Int endCell = new Vector2Int(2, 0);

    [Tooltip("지나갈 수 없는 칸. ★지금은 비워 둔다 — 나중에 어려운 판을 만들 때 쓰려고 미리 열어둔 자리다.\n" +
             "칸을 막으면 위의 '짝수 칸' 규칙만으로는 풀림을 보장할 수 없으니, 넣을 때 반드시 직접 풀어 볼 것.")]
    [SerializeField] List<Vector2Int> blockedCells = new();

    [Header("연결")]
    [Tooltip("퍼즐 UI. 비워두면 씬에서 이름(TimelinePuzzlePanel)으로 찾는다.")]
    [SerializeField] TimelinePuzzleUI panel;

    [Header("근접 힌트 / 외곽선")]
    [Tooltip("가까이 가면 켤 [F] 알약 UI.\n비우면 씬의 공용 패널(FacilityUnlockSelectPanel)을 실행 중에 자동으로 찾는다.")]
    [SerializeField] GameObject hintUI;

    // 로컬라이징 키이기도 하다(Loc.Get 은 한국어 원문을 키로 쓴다) → 한 곳에만 적어 둔다.
    const string DefaultHintLabel = "복원하기";

    [Tooltip("알약에 표시할 동작 이름.")]
    [SerializeField] string hintLabel = DefaultHintLabel;

    [Tooltip("알약 왼쪽 아이콘. 비우면 패널에 원래 박혀 있는 아이콘을 그대로 쓴다.")]
    [SerializeField] Sprite hintIcon;

    [Tooltip("외곽선을 켤 대상들. 비우면 이 오브젝트 이하 전체.\n" +
             "부모자식이 아니어도 된다 — 씬 어디 있는 오브젝트든 여러 개 넣을 수 있다.")]
    [SerializeField] Transform[] outlineTargets;

    [Header("저장")]
    [Tooltip("이 퍼즐을 구분하는 고유 id. 비워두면 오브젝트 이름을 쓴다.\n" +
             "★한 번 정하면 바꾸지 말 것 — 바꾸면 이미 푼 플레이어가 다시 풀어야 한다.")]
    [SerializeField] string puzzleId = "";

    public Vector2Int StartCell => startCell;
    public Vector2Int EndCell => endCell;
    public IReadOnlyList<Vector2Int> BlockedCells => blockedCells;

    string Id => string.IsNullOrEmpty(puzzleId) ? name : puzzleId;

    // 이미 풀었으면 다시 열리지 않는다.
    public bool CanInteract => !IsSatisfied;

    // ★base.Awake() 를 반드시 부른다 — 부모(GimmickTrigger)가 거기서 '이미 푼 조건'을 복원한다.
    //   override 를 빼고 그냥 Awake 를 정의하면 부모 것이 가려져 조용히 복원이 안 된다.
    protected override void Awake()
    {
        base.Awake();
        SaveSlotManager.Instance?.Register(this);

        // 세이브 복원은 "끌어오기" 패턴 — 각 시스템이 자기 Awake 에서 직접 읽는다(ISaveable 주석 참고).
        //   instant:true — 지금 푼 게 아니라 '이미 풀려 있던' 상태를 되돌리는 것이므로
        //   결계가 사라지는 연출과 소멸음 없이 처음부터 없던 것처럼 만든다.
        var data = SaveSlotManager.Instance?.Data;
        if (data != null && data.solvedPuzzleIds.Contains(Id)) SetSatisfied(true, instant: true);
    }

    InteractHighlight _highlight;

    void Start()
    {
        _highlight = new InteractHighlight(outlineTargets, transform);

        // 인스펙터에서 안 넣었으면 씬의 공용 알약 패널을 찾아 쓴다(수동 드래그 생략).
        if (hintUI == null) hintUI = InteractHintPanel.FindSharedPanel();
        InteractHintPanel.Prime(hintUI, this);
    }

    void OnDestroy() => SaveSlotManager.Instance?.Unregister(this);

    public void Interact(Player player)
    {
        if (!CanInteract) return;

        EnsurePanel();
        if (panel == null)
        {
            Debug.LogError("[TimelinePuzzleConsole] 퍼즐 UI(TimelinePuzzlePanel)를 찾지 못했습니다.", this);
            return;
        }
        panel.Open(this, OnSolved);
    }

    /// <summary>다가오면 [F] 알약 + 외곽선을 켠다. 다른 상호작용물(코어 단말 등)과 같은 경로를 쓴다 —
    /// 알약의 글자는 씬에 실물로 있는 공용 패널의 것을 갈아끼우는 방식이라 런타임 생성이 아니다.</summary>
    public void ShowHint(bool show)
    {
        bool on = show && CanInteract;
        // 비어 있으면 기본값으로 — 알약에 글자가 없는 채로 뜨는 것보다 낫다.
        string label = string.IsNullOrEmpty(hintLabel) ? DefaultHintLabel : hintLabel;
        InteractHintPanel.Show(hintUI, on, Loc.Get(label), hintIcon);
        _highlight?.Set(on);
    }

    void OnSolved()
    {
        SetSatisfied(true);      // 연결된 GimmickTarget 들이 열린다
        ShowHint(false);         // 더 이상 상호작용 대상이 아니다

        var data = SaveSlotManager.Instance?.Data;
        if (data != null && !data.solvedPuzzleIds.Contains(Id)) data.solvedPuzzleIds.Add(Id);
        SaveSlotManager.Instance?.SaveActive();   // 다시 풀지 않도록 즉시 기록
    }

    public void Capture(GameSaveData data)
    {
        if (data == null) return;
        if (IsSatisfied && !data.solvedPuzzleIds.Contains(Id)) data.solvedPuzzleIds.Add(Id);
    }

    void EnsurePanel()
    {
        if (panel != null) return;
        panel = Object.FindFirstObjectByType<TimelinePuzzleUI>(FindObjectsInactive.Include);
    }

#if UNITY_EDITOR
    // 안 풀리는 판이 씬에 배치되는 것을 막는다. 이 경고는 무시하면 안 된다 —
    // 홀수 칸을 시작이나 끝으로 잡으면 플레이어가 아무리 시도해도 풀 수 없다.
    void OnValidate()
    {
        startCell = Clamp(startCell);
        endCell = Clamp(endCell);

        if (startCell == endCell)
            Debug.LogWarning($"[{name}] 시작 칸과 도착 칸이 같습니다.", this);
        else if (!IsEvenCell(startCell) || !IsEvenCell(endCell))
            Debug.LogWarning($"[{name}] 행+열이 짝수인 칸만 시작·도착으로 쓸 수 있습니다. " +
                             "지금 설정으로는 절대 풀 수 없는 판이 됩니다.", this);
    }

    static Vector2Int Clamp(Vector2Int v) =>
        new Vector2Int(Mathf.Clamp(v.x, 0, Size - 1), Mathf.Clamp(v.y, 0, Size - 1));
#endif

    public static bool IsEvenCell(Vector2Int c) => ((c.x + c.y) & 1) == 0;
}
