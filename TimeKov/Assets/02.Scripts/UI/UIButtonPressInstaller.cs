// =====================================================================
// UIButtonPressInstaller.cs
// 프로젝트의 모든 Button 에 눌림 연출(UIButtonPressEffect)을 자동으로 붙인다.
//
// [왜 자동인가]
//   버튼은 씬에 구운 것, 프리팹 안에 있는 것, 코드가 런타임에 만드는 것이 섞여 있다.
//   손으로 붙이면 새 버튼을 만들 때마다 빼먹고, 빼먹은 버튼만 반응이 없어서
//   오히려 "가끔 안 눌린다"는 인상을 준다. 그래서 전역에서 한 번에 처리한다.
//
// [어떻게 찾나]
//   UGUI 의 Selectable 은 켜질 때 자기를 static 목록에 등록한다. 그 목록만 훑으면
//   출신(씬/프리팹/런타임 생성)과 무관하게 지금 화면에 살아 있는 버튼을 다 볼 수 있다.
//   꺼진 패널의 버튼은 목록에 없다가 패널이 열리면 들어오므로, 주기적으로 훑는다.
//
// [비용]
//   0.15 초에 한 번, 살아 있는 Selectable 수(보통 수십~200개)만큼 TryGetComponent.
//   매 프레임 도는 일이 아니라 프레임에 잡히지 않는다.
//
// [끄는 법] UIButtonPressInstaller.Enabled = false  (버튼 하나만 빼려면 UIButtonPressEffectIgnore)
// =====================================================================

using UnityEngine;
using UnityEngine.UI;

public static class UIButtonPressInstaller
{
    /// <summary>전체 끄기. 연출이 의심스러우면 이것부터 false 로 두고 원인을 좁힌다.</summary>
    public static bool Enabled = true;

    // 훑는 주기(초). 짧게 잡을 이유가 없다 - 패널이 열리고 사람이 버튼을 누르기까지는
    // 아무리 빨라도 이보다 오래 걸린다.
    private const float SweepInterval = 0.15f;

    private static Selectable[] _buf = new Selectable[128];
    private static float _next;
    private static bool _hooked;

    // 도메인 리로드를 꺼두면 static 이 살아남아 아래 Boot 이 또 돌면서 중복 구독된다.
    // 플레이 시작마다 확실히 한 번만 물리도록 먼저 떼어낸다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Canvas.willRenderCanvases -= Sweep;
        _hooked = false;
        _next = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Enabled || _hooked) return;
        _hooked = true;
        // UGUI 가 캔버스를 갱신하기 직전에 매 프레임 호출된다. 씬에 아무것도 안 놓아도 되고
        // 씬이 바뀌어도 유지된다(펌프용 오브젝트를 따로 만들 필요가 없다).
        Canvas.willRenderCanvases += Sweep;
    }

    private static void Sweep()
    {
        if (!Enabled) return;

        float now = Time.unscaledTime;
        if (now < _next) return;
        _next = now + SweepInterval;

        int count = Selectable.allSelectableCount;
        if (count <= 0) return;
        if (_buf.Length < count) _buf = new Selectable[Mathf.NextPowerOfTwo(count)];

        count = Selectable.AllSelectablesNoAlloc(_buf);
        for (int i = 0; i < count; i++)
        {
            var s = _buf[i];
            _buf[i] = null;              // 파괴된 오브젝트를 붙잡고 있지 않게 비운다
            if (s == null) continue;
            if (!(s is Button)) continue;

            // Animation 전환은 Animator 가 이 오브젝트를 통째로 굴린다. 우리가 스케일을
            // 같이 쓰면 서로 덮어써서 떨린다.
            if (s.transition == Selectable.Transition.Animation) continue;

            var go = s.gameObject;
            if (go.TryGetComponent<UIButtonPressEffect>(out _)) continue;
            if (go.TryGetComponent<UIButtonPressEffectIgnore>(out _)) continue;

            // 같은 transform 의 localScale 을 직접 쓰는 것들. 붙이면 서로 값을 밟는다.
            //   UIPulseScale    - 매 프레임 맥동으로 덮어쓴다
            //   MenuItemHoverFx - DOTween 으로 호버 확대를 굴린다
            if (go.TryGetComponent<UIPulseScale>(out _)) continue;
            if (go.TryGetComponent<MenuItemHoverFx>(out _)) continue;

            go.AddComponent<UIButtonPressEffect>();
        }
    }
}
