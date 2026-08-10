using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// ── 도전 구역 구조(救助) 연출 ─────────────────────────────────────────────────
// 시간 급속감소 구역 안에서 시간이 다 닳아도 '진짜로' 죽지 않는다.
//   검은 화면으로 페이드 아웃 → 입구로 되돌리고 시간 일부 회복 → 페이드 인.
//   진짜 사망이 아니므로 아이템 드롭·게임오버가 일어나지 않는다(TimeHazardZone 이 사망을 가로챈다).
//
// 씬 세팅/프리팹 불필요 — 필요할 때 런타임에 스스로 만들어진다(지연 싱글톤).
//   페이드 오버레이는 WarpManager 와 같은 방식(코드로 만든 Overlay 캔버스 + CanvasGroup).
[SingleInstance]
public class TimeHazardRescue : MonoBehaviour
{
    private static TimeHazardRescue _instance;
    private static bool _quitting;

    private CanvasGroup _cg;
    private bool _busy;

    /// 지금 구조 연출이 진행 중인가(중복 발동 방지용 — 구역이 물어본다).
    public static bool IsBusy => _instance != null && _instance._busy;

    /// 구조 시작. player 를 entrance 위치로 되돌린다.
    ///   ★HP 회복은 호출한 쪽이 '이 호출 전에' 이미 끝내야 한다(0 이면 그 사이 진짜 사망 처리가 돈다).
    public static void Run(Transform player, Vector3 entrance, float fadeTime, float blackHold)
    {
        if (_quitting || player == null) return;
        var inst = Instance;
        if (inst == null || inst._busy) return;
        inst.StartCoroutine(inst.Routine(player, entrance, fadeTime, blackHold));
    }

    private static TimeHazardRescue Instance
    {
        get
        {
            if (_instance == null && !_quitting)
            {
                var go = new GameObject("TimeHazardRescue");
                _instance = go.AddComponent<TimeHazardRescue>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (UIDuplicateGuard.Report(_instance, this)) { Destroy(gameObject); return; }
        _instance = this;
        BuildOverlay();
    }

    private void OnApplicationQuit() => _quitting = true;
    private void OnDestroy() { if (_instance == this) _instance = null; }

    private void BuildOverlay()
    {
        var canvasGo = new GameObject("TimeHazardRescueFade");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;   // 워프 페이드와 동일 — 대부분의 UI 위를 덮는다

        var imgGo = new GameObject("Black", typeof(RectTransform));
        imgGo.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var img = imgGo.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;

        _cg = imgGo.AddComponent<CanvasGroup>();
        _cg.alpha = 0f; _cg.blocksRaycasts = false; _cg.interactable = false;
    }

    private IEnumerator Routine(Transform player, Vector3 entrance, float fadeTime, float blackHold)
    {
        _busy = true;
        PlayerInputComponent.IsBlocked = true;   // 연출 중 조작 차단(워프와 동일)

        if (_cg != null) _cg.blocksRaycasts = true;
        yield return CoreUtilities.FadeUnscaled(_cg, 0f, 1f, fadeTime);   // 검게
        yield return new WaitForSecondsRealtime(blackHold);

        // 검은 화면 중에 되돌린다 — 순간이동이 안 보이게.
        Teleport(player, entrance);

        yield return CoreUtilities.FadeUnscaled(_cg, 1f, 0f, fadeTime);   // 밝게
        if (_cg != null) _cg.blocksRaycasts = false;

        PlayerInputComponent.IsBlocked = false;
        _busy = false;
    }

    // WarpManager.TeleportPlayer 와 동일한 방식 — Rigidbody 위치를 직접 옮기고 속도를 지운다.
    //   (속도를 안 지우면 떨어지던 가속도가 남아 도착하자마자 튄다)
    private static void Teleport(Transform player, Vector3 dest)
    {
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = dest;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        player.position = dest;
    }
}
