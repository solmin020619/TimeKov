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
    private Image _blackImg;          // 화면 전체 어둠(알파=세기) - 발동 시 보스 인스펙터 값으로 갱신
    private ScreenVignette _vignette; // 가장자리 비네트 - 발동 시 보스 인스펙터 값으로 갱신

    // duration: 디버프 지속(초) / drainMult: 시간 드레인 배수 / darkAlpha: 화면 어둠 세기(0~0.8) / vStrength,vFalloff: 가장자리 비네트
    // 세기 값은 WyvernBossController 인스펙터에서 넘어온다(코드 상수 아님 - 인스펙터서 조절).
    public static void Trigger(float duration, float drainMult, float darkAlpha, float vStrength, float vFalloff)
    {
        if (_quitting) return;
        var inst = Instance;
        if (inst != null) inst.Run(duration, drainMult, darkAlpha, vStrength, vFalloff);
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

    const float DarkAlpha = 0.5f;   // 빌드 시 초기값일 뿐 - 실제 세기는 Trigger가 보스(WyvernBossController) 인스펙터 값으로 매 발동 덮어씀.

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
        _blackImg = blackImg;

        // 2) 가장자리 비네트(분위기 강화)
        var vigGo = new GameObject("Vignette", typeof(RectTransform));
        vigGo.transform.SetParent(groupGo.transform, false);
        Stretch((RectTransform)vigGo.transform);
        vigGo.AddComponent<RawImage>();                 // ScreenVignette RequireComponent
        var vig = vigGo.AddComponent<ScreenVignette>();
        vig.vignetteStrength = 0.95f;                   // 빌드 초기값(Trigger가 인스펙터 값으로 덮어씀)
        vig.vignetteFalloff = 0.55f;
        _vignette = vig;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private void Run(float duration, float drainMult, float darkAlpha, float vStrength, float vFalloff)
    {
        // 매 발동 시 보스 인스펙터 값 적용 -> 인스펙터에서 어둠/비네트 세기 조절 가능.
        if (_blackImg != null) _blackImg.color = new Color(0f, 0f, 0f, Mathf.Clamp01(darkAlpha));
        if (_vignette != null)
        {
            _vignette.vignetteStrength = Mathf.Clamp01(vStrength);
            _vignette.vignetteFalloff  = Mathf.Clamp(vFalloff, 0.05f, 0.8f);
        }
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
