// =====================================================================
// ScreenshotMode.cs
// 트레일러/스크린샷 촬영용 - F8 로 화면 HUD 를 숨기고 다시 되돌린다.
//
// ★정식 빌드에도 들어간다(개발자키가 아니다). 트레일러는 빌드로 찍으므로
//   DevHotkeys(에디터/Dev빌드 전용) 에 두면 정작 촬영할 때 안 먹는다.
//   그래서 이 파일은 #if 가드 없이 항상 컴파일되고, F8 입력도 스스로 받는다.
//
// [무엇이 사라지나 - 평소 자동으로 사라지는 것과 똑같다]
//   기지에서 가만히 있으면 HUD 가 스르르 사라지는 동작이 원래 있다(HudAutoFader / SkillBarUI).
//   F8 은 그 상태를 '강제로' 만드는 것뿐이다. 즉 우측 상단 시간/체력바와 우측 하단 스킬바가
//   평소와 같은 방식으로 사라진다.
//
//   ★예전엔 씬의 모든 Canvas 를 꺼버렸다. 그러면 몬스터 머리 위 체력바, 피해 숫자,
//     설비 위 표시까지 같이 사라져서 트레일러에 담아야 할 그림이 날아갔다. 그래서 폐기했다.
//
// [끄는 방식 - 상태를 건드리지 않는다]
//   HUD 쪽 표시 판정에 플래그 하나만 얹는다. 오브젝트를 SetActive 로 껐다 켜면
//   OnEnable/OnDisable 이 줄줄이 돌아 초기화가 어긋난다(이미 겪은 사고다).
//   플래그 방식이라 일시정지/입력/커서 전부 그대로고, 촬영 중 아무 때나 켜고 꺼도 안전하다.
//
// [IMGUI 오버레이는 따로 꺼야 한다]
//   FpsCounter / PerfHud 는 OnGUI 로 직접 그려서 캔버스/알파와 무관하다.
//   그냥 두면 FPS 숫자가 트레일러에 박히므로 같이 끈다. 내가 끈 것만 되돌린다.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

public class ScreenshotMode : MonoBehaviour
{
    /// <summary>UI 를 숨겼다 되돌리는 키. 촬영용이라 정식 빌드에서도 동작한다.</summary>
    public static KeyCode ToggleKey = KeyCode.F8;

    /// <summary>
    /// 지금 촬영 모드인가. HUD 표시 판정(HudAutoFader / SkillBarUI)이 이 값을 본다.
    /// 새로 만든 HUD 를 촬영 때 숨기고 싶으면 그쪽 표시 조건에 이 플래그를 한 줄 얹으면 된다.
    /// </summary>
    public static bool Active { get; private set; }

    private static readonly List<MonoBehaviour> _hiddenOverlays = new(); // 내가 끈 IMGUI 오버레이만

    // 씬에 붙일 필요 없음: 게임 시작 시 자동 생성되고 씬 전환에도 유지된다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        var go = new GameObject("[ScreenshotMode]") { hideFlags = HideFlags.HideAndDontSave };
        go.AddComponent<ScreenshotMode>();
        DontDestroyOnLoad(go);
    }

    /// <summary>HUD 숨김 토글. 게임 상태(일시정지/입력/커서)는 건드리지 않는다.</summary>
    public static void Toggle()
    {
        if (Active) Show();
        else Hide();
    }

    private static void Hide()
    {
        Active = true;

        // OnGUI 로 직접 그리는 것들. 알파 페이드가 안 통해서 컴포넌트를 꺼야 한다.
        HideOverlay(FindAnyObjectByType<FpsCounter>());
        HideOverlay(FindAnyObjectByType<PerfHud>());

        Debug.Log($"[촬영 모드] HUD 를 숨겼다. {ToggleKey} 를 다시 누르면 되돌린다.");
    }

    private static void Show()
    {
        Active = false;

        // 원래 꺼져 있던 것을 켜버리면 안 되므로 '내가 끈 것'만 되돌린다.
        foreach (var m in _hiddenOverlays)
        {
            if (m == null) continue;   // 그 사이 파괴된 것은 건너뛴다
            m.enabled = true;
        }
        _hiddenOverlays.Clear();

        Debug.Log("[촬영 모드] HUD 를 되돌렸다.");
    }

    private static void HideOverlay(MonoBehaviour m)
    {
        if (m == null || !m.enabled) return;
        m.enabled = false;
        _hiddenOverlays.Add(m);
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey)) Toggle();
    }
}
