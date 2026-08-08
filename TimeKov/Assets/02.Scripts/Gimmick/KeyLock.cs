using UnityEngine;

// ── 열쇠 자물쇠 (열쇠 아이템으로 잠금 해제) ──────────────────────────────────────
// 정예가 떨군 '열쇠' 아이템을 소모해 문/상자를 여는 자물쇠. GimmickTrigger 라 targets 를 연다.
//   • 플레이어가 F 로 조작 → 가방에 열쇠(keyItemId)가 있으면 소모하고 잠금 해제(→ targets 개방).
//   • 열쇠가 없으면 거부(토스트 안내). 한 번 열리면 계속 열림(latch).
//
//   보통 EliteKeyDrop(정예 처치 시 열쇠 100% 드랍) 과 짝으로 쓴다. keyItemId 를 서로 맞춰라.
//   WarpPoint/GimmickSwitch 와 같은 방식으로 F 알약 + 근접 외곽선을 쓴다.
[RequireComponent(typeof(Collider))]
public class KeyLock : GimmickTrigger, IInteractable, IInteractHint
{
    [Header("열쇠")]
    [Tooltip("잠금을 여는 데 필요한 열쇠 아이템 ID. EliteKeyDrop 의 Key Item Id 와 같아야 한다.")]
    [SerializeField] private int keyItemId;
    [Tooltip("필요한 열쇠 개수. 보통 1.")]
    [Min(1)] [SerializeField] private int keysRequired = 1;
    [Tooltip("체크: 열 때 열쇠를 소모한다. 해제하면 '보유만 확인'하고 소모 안 함(마스터키식).")]
    [SerializeField] private bool consumeKey = true;

    [Header("상태 비주얼 (선택)")]
    [Tooltip("잠겨 있을 때만 켤 오브젝트(자물쇠 등).")]
    [SerializeField] private GameObject lockedVisual;
    [Tooltip("열렸을 때만 켤 오브젝트(풀린 자물쇠 등).")]
    [SerializeField] private GameObject unlockedVisual;

    [Header("사운드")]
    [Tooltip("잠금 해제 시 재생. None 이면 기본 스위치음으로 폴백.")]
    [SerializeField] private SfxId unlockSfx = SfxId.None;
    [Tooltip("열쇠가 없어 거부될 때 재생(선택). None 이면 무음.")]
    [SerializeField] private SfxId deniedSfx = SfxId.None;

    [Header("근접 힌트 (F)")]
    [Tooltip("가까이 가면 켤 F 알약 UI. 비우면 씬 공용 패널을 자동으로 찾아 쓴다.")]
    [SerializeField] private GameObject hintUI;
    [Tooltip("알약에 표시할 이름.")]
    [SerializeField] private string hintLabel = "열쇠 사용";
    [Tooltip("알약 왼쪽 아이콘. 비우면 패널 기본 아이콘 사용.")]
    [SerializeField] private Sprite hintIcon;
    [Tooltip("가까이 가면 외곽선을 켤 대상들. 비우면 이 오브젝트를 쓴다.")]
    [SerializeField] private Transform[] outlineTargets;

    private InteractHighlight _highlight;

    private void Start()
    {
        // PlayerInteractComponent 의 OverlapSphere 는 Interactable 레이어만 훑는다.
        int il = LayerMask.NameToLayer("Interactable");
        if (il >= 0) gameObject.layer = il;

        if (hintUI == null) hintUI = InteractHintPanel.FindSharedPanel();
        _highlight = new InteractHighlight(outlineTargets, transform);
        InteractHintPanel.Prime(hintUI, this);

        ApplyVisual();
    }

    // 이미 열렸으면 F 후보에서 빠진다.
    public bool CanInteract => !IsSatisfied;

    public void Interact(Player player)
    {
        if (IsSatisfied) return;

        var inv = InventoryManager.Instance;
        int have = inv != null ? inv.GetTotalItemCount(keyItemId) : 0;
        if (have < keysRequired)
        {
            GameSfx.Play(deniedSfx, transform.position);
            var data = ItemDatabase.GetItem(keyItemId);
            string keyName = data != null ? data.itemName : "열쇠";
            ToastManager.Warning($"{keyName}이(가) 필요합니다");
            return;
        }

        if (consumeKey) inv.TryConsumeItem(keyItemId, keysRequired);

        GameSfx.Play(unlockSfx == SfxId.None ? SfxId.GimmickSwitchOn : unlockSfx, transform.position);
        SetSatisfied(true);   // latch → targets 영구 개방
        ApplyVisual();
        _highlight?.Set(false);
        InteractHintPanel.Show(hintUI, false, Loc.Get(hintLabel), hintIcon);
    }

    private void ApplyVisual()
    {
        if (lockedVisual   != null) lockedVisual.SetActive(!IsSatisfied);
        if (unlockedVisual != null) unlockedVisual.SetActive(IsSatisfied);
    }

    public void ShowHint(bool show)
    {
        if (IsSatisfied) { _highlight?.Set(false); return; }
        InteractHintPanel.Show(hintUI, show, Loc.Get(hintLabel), hintIcon);
        _highlight?.Set(show);
    }
}
