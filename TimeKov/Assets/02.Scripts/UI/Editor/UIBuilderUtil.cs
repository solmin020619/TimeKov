using UnityEditor;
using UnityEngine;

// [08-02] UI 빌더 공용 헬퍼.
//
// 겪은 사고: 빌더들이 FindFirstObjectByType<Canvas>() 로 캔버스를 찾았는데,
//   우리가 만든 UI(CastGauge 등)가 정렬 격리용으로 Canvas 컴포넌트를 갖고 있어서 그게 먼저 잡혔다.
//   결과: Canvas/InventoryCanvas/CastGauge/ChestPrompt 처럼 UI 안에 UI 가 파묻히고,
//         부모의 CanvasGroup 알파가 0 이라 자식까지 통째로 안 보였다.
//   -> 캔버스 탐색은 반드시 '루트 캔버스'로 한다.
public static class UIBuilderUtil
{
    /// <summary>씬의 메인(루트) 캔버스. 중첩 Canvas 는 제외한다.</summary>
    public static Canvas FindMainCanvas()
    {
        var all = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Canvas best = null;
        foreach (var c in all)
        {
            if (!c.isRootCanvas) continue;               // 중첩 캔버스(우리 UI가 붙인 것) 제외
            if (c.renderMode != RenderMode.ScreenSpaceOverlay && c.renderMode != RenderMode.ScreenSpaceCamera) continue;
            if (best == null) { best = c; continue; }
            // 이름이 정확히 "Canvas" 인 쪽을 우선(메인 캔버스 관례)
            if (c.name == "Canvas" && best.name != "Canvas") best = c;
        }
        return best;
    }

    /// <summary>캔버스 아래 그룹 컨테이너를 찾고, 없으면 만든다(HUD/Overlays/Panels/Modals 정리용).</summary>
    public static Transform EnsureGroup(Canvas canvas, string groupName)
    {
        var t = canvas.transform.Find(groupName);
        if (t != null) return t;

        var go = new GameObject(groupName, typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Undo.RegisterCreatedObjectUndo(go, "Create UI Group");
        return go.transform;
    }

    /// <summary>방금 만든 계층 안의 절차 스프라이트를 지금 한 번 채운다(에디터 미리보기용).
    /// 이 참조는 유니티 재시작 때 사라지지만, 그때는 RuntimeGeneratedSprite 가 실행 시 스스로 다시 채운다.
    /// 안 부르면 에디터에서 흰 사각형만 보여 레이아웃 조정이 어렵다.</summary>
    public static void ApplyGeneratedSprites(GameObject root)
    {
        if (root == null) return;
        foreach (var gen in root.GetComponentsInChildren<RuntimeGeneratedSprite>(true))
            gen.Apply();
    }

    /// <summary>방금 만든 계층에서 UISpriteFactory 가 만든 스프라이트를 쓰는 Image 를 전부 찾아
    /// 재생성용 키를 심어둔다(RuntimeGeneratedSprite.sourceKey).
    ///
    /// 왜: 팩토리 스프라이트는 에셋이 아니라 메모리 생성물이라 유니티를 껐다 켜면 사라진다.
    ///   호출부가 수십 군데인 화면은 하나씩 컴포넌트를 붙일 수 없으므로, 빌드가 끝난 뒤 한 번에 훑는다.
    /// 이미 RuntimeGeneratedSprite 가 붙어 있으면(모양을 손으로 지정한 경우) 건드리지 않는다.</summary>
    public static int AttachGeneratedSpriteKeys(GameObject root)
    {
        if (root == null) return 0;
        int n = 0;
        foreach (var img in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            if (img.sprite == null) continue;
            if (img.GetComponent<RuntimeGeneratedSprite>() != null) continue;
            if (!UISpriteFactory.TryGetKey(img.sprite, out string key)) continue;   // 진짜 에셋 스프라이트는 건너뜀
            img.gameObject.AddComponent<RuntimeGeneratedSprite>().sourceKey = key;
            n++;
        }
        return n;
    }

    /// <summary>씬 어디에 있든(비활성 포함) 해당 UI 컴포넌트를 가진 오브젝트를 지운다.
    /// 부모가 잘못 잡혀 엉뚱한 곳에 만들어진 이전 결과물까지 정리하기 위함.</summary>
    public static int RemoveExisting<T>() where T : Component
    {
        var found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int n = 0;
        foreach (var c in found)
        {
            if (c == null) continue;
            Undo.DestroyObjectImmediate(c.gameObject);
            n++;
        }
        return n;
    }
}
