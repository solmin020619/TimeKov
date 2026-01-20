using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TimeWarningUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image vignetteRed;

    [Header("Thresholds")]
    [SerializeField] private float warn20 = 20f;
    [SerializeField] private float warn10 = 10f;

    [Header("Blink")]
    [SerializeField] private float blinkSpeed10 = 0.08f; // 10초 이하 점멸 속도
    [SerializeField] private float steadyAlpha20 = 0.20f; // 20초 이하 기본 붉기
    [SerializeField] private float dangerAlpha = 0.35f;    // 급증 지역 붉기

    private Coroutine blinkCo;
    private bool inDecayZone;

    //  타이머 시스템에서 매 프레임/주기적으로 호출해주면 됨
    public void SetTimeRemaining(float t)
    {
        if (vignetteRed == null) return;

        if (t <= 0f)
        {
            StopBlink();
            SetAlpha(0f);
            return;
        }

        if (t <= warn10)
        {
            // 10초 이하: 빠른 점멸
            if (blinkCo == null)
                blinkCo = StartCoroutine(Blink(blinkSpeed10));
        }
        else if (t <= warn20)
        {
            // 20초 이하: 붉게 유지
            StopBlink();
            SetAlpha(inDecayZone ? dangerAlpha : steadyAlpha20);
        }
        else
        {
            // 정상
            StopBlink();
            SetAlpha(inDecayZone ? dangerAlpha : 0f);
        }
    }

    //  TimeDecay 급증 지역 진입/이탈 시 호출
    public void SetInDecayZone(bool active)
    {
        inDecayZone = active;
    }

    private IEnumerator Blink(float speed)
    {
        while (true)
        {
            // on
            SetAlpha(inDecayZone ? dangerAlpha : 0.28f);
            yield return new WaitForSecondsRealtime(speed);
            // off
            SetAlpha(0f);
            yield return new WaitForSecondsRealtime(speed);
        }
    }

    private void StopBlink()
    {
        if (blinkCo != null) StopCoroutine(blinkCo);
        blinkCo = null;
    }

    private void SetAlpha(float a)
    {
        var c = vignetteRed.color;
        c.a = a;
        vignetteRed.color = c;
    }
}
