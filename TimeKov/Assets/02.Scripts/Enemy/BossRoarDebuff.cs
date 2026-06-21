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

    const float DarkAlpha = 0.5f;   // 전체 화면 검정 오버레이 세기(중앙까지 어둡게). 더 어둡게=올려라(0~0.8).

    private void BuildVignette()
    {
        var canvasGo = new GameObject("RoarVignetteCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;   // 월드 위(HUD 아래)

        // 전체를 한 그룹으로 묶어 알파 페이드
        var groupGo = new GameObject("DarkGroup", typeof(RectTransform));
        groupGo.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)groupGo.transform);
        _cg = groupGo.AddComponent<CanvasGroup>();
        _cg.alpha = 0f; _cg.blocksRaycasts = false; _cg.interactable = false;

        // 1) 전체 화면 검정 오버레이 = 중앙까지 확 어둡게(예전엔 가장자리 비네트만이라 미미했음)
        var blackGo = new GameObject("Dark", typeof(RectTransform));
        blackGo.transform.SetParent(groupGo.transform, false);
        Stretch((RectTransform)blackGo.transform);
        var blackImg = blackGo.AddComponent<Image>();
        blackImg.color = new Color(0f, 0f, 0f, DarkAlpha);
        blackImg.raycastTarget = false;

        // 2) 가장자리 비네트(분위기 강화)
        var vigGo = new GameObject("Vignette", typeof(RectTransform));
        vigGo.transform.SetParent(groupGo.transform, false);
        Stretch((RectTransform)vigGo.transform);
        vigGo.AddComponent<RawImage>();                 // ScreenVignette RequireComponent
        var vig = vigGo.AddComponent<ScreenVignette>();
        vig.vignetteStrength = 0.95f;                   // 0.82 -> 0.95(가장자리 더 진하게)
        vig.vignetteFalloff = 0.55f;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
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
