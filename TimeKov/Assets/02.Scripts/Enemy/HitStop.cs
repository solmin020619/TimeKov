using System.Collections;
using UnityEngine;

// 타격 순간 아주 짧게 시간을 늦춰 "퍽" 하고 걸리는 느낌을 만든다.
// 타격감에 기여도가 가장 큰 요소인데 비용은 거의 없다.
//
// [주의] 전역 timeScale 을 건드리므로 규칙이 있다.
//  - 겹치면 게임이 멈춘 것처럼 보인다 -> 이미 진행 중이면 남은 시간만 늘린다(중첩 금지).
//  - 완전 정지(0)는 쓰지 않는다. 이 프로젝트에 timeScale 0 에서 NaN 나는 코드가 있었다.
//  - 복구는 unscaled 로 센다. scaled 로 세면 느려진 시간 때문에 영원히 안 끝난다.
//  - 일시정지(메뉴) 중에는 아예 발동하지 않는다. 풀리면서 timeScale 을 덮어쓰면 안 된다.
public class HitStop : MonoBehaviour
{
    private static HitStop _inst;
    private float _remain;
    private bool _running;

    public static void Play(float duration, float scale = 0.08f)
    {
        if (duration <= 0f) return;

        // 메뉴 등으로 이미 멈춰 있으면 건드리지 않는다
        if (Time.timeScale <= 0.001f) return;

        if (_inst == null)
        {
            var go = new GameObject("[HitStop]");
            _inst = go.AddComponent<HitStop>();
            DontDestroyOnLoad(go);
        }
        _inst.Begin(duration, scale);
    }

    private void Begin(float duration, float scale)
    {
        // 중첩 금지: 길이만 늘리고 코루틴은 하나만 돈다
        _remain = Mathf.Max(_remain, duration);
        if (_running) return;

        StartCoroutine(Run(Mathf.Clamp(scale, 0.01f, 0.9f)));
    }

    private IEnumerator Run(float scale)
    {
        _running = true;
        float prev = Time.timeScale;
        Time.timeScale = scale;

        while (_remain > 0f)
        {
            _remain -= Time.unscaledDeltaTime;
            yield return null;
        }

        // 도중에 메뉴 등이 timeScale 을 0 으로 만들었으면 덮어쓰지 않는다
        if (Time.timeScale > 0.001f) Time.timeScale = prev <= 0.001f ? 1f : prev;
        _running = false;
    }

    private void OnDestroy()
    {
        if (_inst == this) _inst = null;
    }
}
