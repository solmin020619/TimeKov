using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 보스 포효 디버프: 화면 가장자리 어둠(ScreenVignette 재사용) + 플레이어 시간 드레인 가속.
// 런타임 지연 싱글톤(씬 세팅/프리팹 불필요). WyvernBossController가 BossRoarDebuff.Trigger 호출.
public class BossRoarDebuff : MonoBehaviour
{
    private static BossRoarDebuff _instance;
    private static bool _quitting;

    private CanvasGroup _cg;
    private Coroutine _co;

    // duration: 디버프 지속(초) / drainMult: 시간 드레인 배수
    public static void Trigger(float duration, float drainMult)
    {
        if (_quitting) return;
        var inst = Instance;
        if (inst != null) inst.Run(duration, drainMult);
    }

    private static BossRoarDebuff Instance
    {
        get
        {
            if (_instance == null && !_quitting)
            {
                var go = new GameObject("BossRoarDebuff");
                _instance = go.AddComponent<BossRoarDebuff>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        BuildVignette();
    }

    private void OnApplicationQuit() => _quitting = true;
    private void OnDestroy() { if (_instance == this) _instance = null; }

    private void BuildVignette()
    {
        var canvasGo = new GameObject("RoarVignetteCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;   // 월드 위, 비네트는 가장자리만 어둡혀 HUD 중앙은 영향 적음

        var imgGo = new GameObject("Vignette", typeof(RectTransform));
        imgGo.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        imgGo.AddComponent<RawImage>();                 // ScreenVignette RequireComponent
        var vig = imgGo.AddComponent<ScreenVignette>(); // 가장자리 어둠 텍스처 1회 생성
        vig.vignetteStrength = 0.82f;
        vig.vignetteFalloff = 0.5f;

        _cg = imgGo.AddComponent<CanvasGroup>();
        _cg.alpha = 0f; _cg.blocksRaycasts = false; _cg.interactable = false;
    }

    private void Run(float duration, float drainMult)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Routine(duration, drainMult));
    }

    private IEnumerator Routine(float duration, float drainMult)
    {
        var stat = FindPlayerStat();
        if (stat != null) stat.HpDrainMultiplier = drainMult;

        yield return Fade(0f, 1f, 0.4f);
        float hold = Mathf.Max(0f, duration - 0.4f - 0.8f);
        yield return new WaitForSeconds(hold);
        yield return Fade(1f, 0f, 0.8f);

        if (stat != null) stat.HpDrainMultiplier = 1f;
        _co = null;
    }

    private IEnumerator Fade(float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur && _cg != null)
        {
            t += Time.deltaTime;
            _cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            yield return null;
        }
        if (_cg != null) _cg.alpha = to;
    }

    private static PlayerStatComponent FindPlayerStat()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        return p != null ? p.GetComponent<PlayerStatComponent>() : null;
    }
}
