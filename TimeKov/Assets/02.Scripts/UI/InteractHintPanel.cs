using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상호작용 힌트(F 알약) 표시 공용 헬퍼.
//
// ★디자인을 새로 그리지 않는다. 씬에 이미 있는 알약 패널을 그대로 켜고 끈다.
//   패널 구조는 공장 설비 / 전리품 픽업과 같은 것을 쓴다:
//     <패널> > PanelRoot > FPrompt(key-box-F-4x.png) + RowContainer > FacilitySelectRow > Icon / Name
//   EmergencyConsole, VattalusDoorBridge 가 쓰던 방식과 같다(그 둘의 중복 코드를 여기로 모은 것).
public static class InteractHintPanel
{
    // 패널 안 아이콘 경로. 프리팹 구조가 바뀌면 여기만 고치면 된다.
    private const string IconPath = "PanelRoot/RowContainer/FacilitySelectRow/Icon";

    /// <summary>
    /// 시작 시 1회. 패널이 프리팹에서 켜진 채로 저장돼 있으므로 꺼둔다.
    /// 잘못된 패널이 연결됐으면 끄지 않고 에러만 남긴다(다른 시스템을 죽이지 않기 위해).
    /// </summary>
    public static void Prime(GameObject hintUI, Object owner)
    {
        if (hintUI == null) return;
        if (!Validate(hintUI, owner, log: true)) return;
        hintUI.SetActive(false);
    }

    /// <summary>
    /// 씬의 공용 F 알약 패널(FacilityUnlockSelectPanel)을 런타임에 찾는다. 비활성 상태여도 찾는다.
    /// hintUI 를 인스펙터에서 안 넣은 상호작용물이 이걸로 자동 연결할 수 있다(수동 드래그 생략).
    /// ★공장이 쓰는 FacilitySelectPanel(런타임에 행을 만드는 패널)을 잘못 잡지 않도록,
    ///   이름이 정확히 "FacilityUnlockSelectPanel" 인 조상만 인정한다.
    /// </summary>
    public static GameObject FindSharedPanel()
    {
        // FindObjectsOfTypeAll: 비활성 GameObject 안의 컴포넌트도 찾는다(공용 패널은 Prime 후 꺼져 있음).
        var rows = Resources.FindObjectsOfTypeAll<FacilitySelectRow>();
        foreach (var row in rows)
        {
            if (row == null) continue;
            if (!row.gameObject.scene.IsValid()) continue;   // 프리팹 에셋 제외(씬 인스턴스만)
            for (var t = row.transform; t != null; t = t.parent)
                if (t.name == "FacilityUnlockSelectPanel") return t.gameObject;
        }
        return null;
    }

    /// <summary>힌트 패널을 켜고 끈다. 켤 때 이름/아이콘을 갈아끼운다.</summary>
    public static void Show(GameObject hintUI, bool show, string label, Sprite icon = null)
    {
        if (hintUI == null) return;
        // 매 프레임은 아니지만 자주 불리므로 여기선 로그를 남기지 않는다(Prime 에서 이미 한 번 알림).
        if (!Validate(hintUI, owner: null, log: false)) return;
        if (show) Apply(hintUI, label, icon);
        hintUI.SetActive(show);
    }

    // ★쓸 수 있는 패널인지 검사한다.
    //   기준: 안에 FacilitySelectRow(알약 한 줄)가 실물로 들어 있어야 한다.
    //   공장이 쓰는 FacilitySelectPanel 은 행을 런타임에 만들어 넣으므로 실물이 없다.
    //   그걸 여기에 연결하면 SetActive(false) 로 공장 UI 를 통째로 죽여버린다(실제로 그랬다).
    private static bool Validate(GameObject hintUI, Object owner, bool log)
    {
        if (hintUI.GetComponentInChildren<FacilitySelectRow>(true) != null) return true;
        if (!log) return false;

        Debug.LogError(
            $"[InteractHintPanel] Hint UI 에 연결된 '{hintUI.name}' 은 쓸 수 없다. " +
            "안에 FacilitySelectRow 가 실물로 들어 있는 패널이어야 한다.\n" +
            "Canvas > Notifications > FacilityUnlockSelectPanel 을 넣어라. " +
            "(이름이 비슷한 FacilitySelectPanel 은 공장이 쓰는 패널이라 연결하면 공장 UI 가 안 뜬다)",
            owner != null ? owner : hintUI);
        return false;
    }

    private static void Apply(GameObject hintUI, string label, Sprite icon)
    {
        var tmp = hintUI.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null && !string.IsNullOrEmpty(label)) tmp.text = label;

        // ★아이콘은 지정했을 때만 건드린다.
        //   이 패널은 설비 해금 힌트와 공유하므로, null 이라고 꺼버리면
        //   다음에 해금 힌트가 떴을 때 아이콘이 사라진 채로 나온다.
        if (icon == null) return;
        var iconImg = hintUI.transform.Find(IconPath)?.GetComponent<Image>();
        if (iconImg == null) return;
        iconImg.sprite  = icon;
        iconImg.enabled = true;
    }
}

// 가까이 갔을 때 켜는 외곽선 묶음. MonoBehaviour 가 아니라 단말이 필드로 하나 들고 쓴다.
//
// 조각이 여러 개인 물체(우주선 펜스 등)에 Outline 을 손으로 다 붙이면 빠뜨리기 쉬워서
// 처음 필요해질 때 런타임에 만들어 붙인다. 이미 붙어 있으면 그걸 그대로 쓴다(수동 설정 우선).
//
// ★대상은 여러 개를 받는다. 코어처럼 "강조할 원통" 과 "단말" 이 부모자식이 아니라
//   씬 여기저기 흩어져 있는 경우가 있어서, 한 칸으로는 못 잡는다.
public class InteractHighlight
{
    private readonly Transform[] _roots;
    private Outline[] _outlines;
    private bool _ready;

    /// <param name="targets">강조할 대상들. 비어 있으면 fallback 하나만 쓴다.</param>
    /// <param name="fallback">targets 가 비었을 때 쓸 기본 대상(보통 단말 자신).</param>
    public InteractHighlight(Transform[] targets, Transform fallback)
    {
        var list = new List<Transform>();
        if (targets != null)
            foreach (var t in targets)
                if (t != null) list.Add(t);

        if (list.Count == 0 && fallback != null) list.Add(fallback);
        _roots = list.ToArray();
    }

    public void Set(bool on)
    {
        Ensure();
        if (_outlines == null) return;
        foreach (var o in _outlines)
            if (o != null) o.enabled = on;
    }

    private void Ensure()
    {
        if (_ready) return;
        _ready = true;

        var all = new List<Outline>();
        foreach (var root in _roots)
        {
            if (root == null) continue;

            var existing = root.GetComponentsInChildren<Outline>(true);
            if (existing.Length > 0)
            {
                all.AddRange(existing);
                continue;
            }

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (r.GetComponent<Outline>() != null) continue;
                all.Add(r.gameObject.AddComponent<Outline>());
            }
        }

        _outlines = all.ToArray();
        foreach (var o in _outlines) InteractOutline.Apply(o);   // 색/두께 통일
        Set(false);
    }
}
