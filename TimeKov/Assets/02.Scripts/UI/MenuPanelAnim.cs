// =====================================================================
// MenuPanelAnim.cs
// 메인메뉴 쪽 창들이 열리고 닫힐 때 쓰는 공통 연출.
//
// [설정창과 같은 것을 쓴다]
//   설정창(SettingsUIBuilder.PlayOpenAnim)이 쓰는 값을 그대로 가져왔다 —
//   알파 0→1 + 배율 1.04→1, 0.22초, OutQuad. 트윈 러너도 같은 UITween 을 쓴다.
//   ★배율이 1 '이상'에서 시작하는 게 핵심이다. 1 미만에서 커지면 줄어든 동안
//     창 가장자리 밖으로 뒤 화면이 비쳐서 지저분하다.
//
//   닫기는 설정창에 아직 연출이 없어(즉시 꺼짐) 그 역방향으로 맞췄다.
//   여는 연출만 있고 닫을 때 툭 사라지면 그게 더 어색하다.
//
// [왜 unscaled 인가]
//   메인메뉴는 timeScale 이 0 일 수 있다(타이틀에서 멈춰 있음). 스케일드 타임으로 돌면
//   연출이 영원히 끝나지 않는다.
// =====================================================================

using System;
using System.Collections;
using GameSettingsUI;
using UnityEngine;

public static class MenuPanelAnim
{
    /// 설정창 openDuration 과 같은 값.
    public const float Duration = 0.22f;
    /// 설정창 openStartScale 과 같은 값.
    public const float StartScale = 1.04f;
    /// 닫기는 열기보다 조금 빠르게 — 기다리게 만들면 답답하다.
    const float CloseRatio = 0.7f;

    /// <summary>열려 있고 '닫히는 중이 아닌가'. 닫기 연출이 도는 동안에도 오브젝트는 아직
    /// 켜져 있어서 activeSelf 만으로는 구분이 안 된다 — 그 사이 ESC/Enter 가 다시 먹히면
    /// 이미 닫은 창을 또 닫으려 든다. Close 가 꺼두는 interactable 로 판별한다.</summary>
    public static bool IsOpen(GameObject panel)
    {
        if (panel == null || !panel.activeSelf) return false;
        var cg = panel.GetComponent<CanvasGroup>();
        return cg == null || cg.interactable;
    }

    public static void Open(GameObject panel)
    {
        if (panel == null) return;
        panel.SetActive(true);

        var cg = Group(panel);
        var rt = panel.transform as RectTransform;

        cg.blocksRaycasts = true;
        cg.interactable = true;
        cg.alpha = 0f;
        UITween.Fade(cg, 1f, Duration, Ease.OutQuad);

        if (rt != null)
        {
            rt.localScale = Vector3.one * StartScale;
            UITween.Scale(rt, 1f, Duration, Ease.OutQuad);
        }
    }

    /// <param name="host">대기 코루틴을 돌릴 컴포넌트. 창 자신이어도 된다 —
    /// 실제로 끄는 건 연출이 끝난 뒤라 그때까지는 살아 있다.</param>
    public static void Close(MonoBehaviour host, GameObject panel, Action done = null)
    {
        if (panel == null) return;
        if (!panel.activeSelf) { done?.Invoke(); return; }

        var cg = Group(panel);
        var rt = panel.transform as RectTransform;

        // 사라지는 동안 클릭이 통과하지 않게. 연타로 두 번 처리되는 것도 여기서 막힌다.
        cg.blocksRaycasts = false;
        cg.interactable = false;

        float dur = Duration * CloseRatio;
        UITween.Fade(cg, 0f, dur, Ease.OutQuad);
        if (rt != null) UITween.Scale(rt, StartScale, dur, Ease.OutQuad);

        if (host != null && host.isActiveAndEnabled)
            host.StartCoroutine(CloseAfter(dur, panel, done));
        else
            Finish(panel, done);   // 코루틴을 돌릴 데가 없으면 연출 없이 끝낸다
    }

    /// <summary>연출 없이 즉시 닫는다. 이 창을 품고 있는 더 큰 패널이 같이 닫히는 경우처럼,
    /// 연출이 끝나기 전에 코루틴을 돌리던 오브젝트가 꺼져 버릴 수 있는 자리에서 쓴다.
    /// (그냥 두면 코루틴이 죽어 창이 '켜진 채 투명한' 상태로 남는다)</summary>
    public static void CloseInstant(GameObject panel)
    {
        if (panel == null || !panel.activeSelf) return;
        Finish(panel, null);
    }

    static IEnumerator CloseAfter(float dur, GameObject panel, Action done)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            if (panel == null) yield break;
            yield return null;
        }
        Finish(panel, done);
    }

    // 다음에 열 때를 위해 원래 값으로 되돌린다. Open 을 거치지 않고 그냥 SetActive(true)
    // 하는 경로가 생겨도 투명하거나 커진 채로 뜨지 않게.
    static void Finish(GameObject panel, Action done)
    {
        if (panel == null) { done?.Invoke(); return; }

        var cg = Group(panel);
        var rt = panel.transform as RectTransform;

        UITween.Stop(cg, "fade");
        if (rt != null) UITween.Stop(rt, "scl");

        panel.SetActive(false);
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
        cg.interactable = true;
        if (rt != null) rt.localScale = Vector3.one;

        done?.Invoke();
    }

    static CanvasGroup Group(GameObject go)
    {
        // ★?? 를 쓰지 않는다 — 유니티 오브젝트는 파괴돼도 ?? 가 null 로 잡지 못한다.
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }
}
