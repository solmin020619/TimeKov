// =====================================================================
// ScreenshotMode.cs
// 트레일러/스크린샷 촬영용 - F8 로 화면의 UI 를 한 번에 숨기고 다시 되돌린다.
//
// ★정식 빌드에도 들어간다(개발자키가 아니다). 트레일러는 빌드로 찍으므로
//   DevHotkeys(에디터/Dev빌드 전용) 에 두면 정작 촬영할 때 안 먹는다.
//   그래서 이 파일은 #if 가드 없이 항상 컴파일되고, F8 입력도 스스로 받는다.
//
// [왜 SetActive 가 아니라 Canvas 컴포넌트를 끄나 - 중요]
//   UI 오브젝트를 SetActive(false) 로 끄면 OnDisable/OnEnable 이 줄줄이 돌아간다.
//   이 프로젝트 UI 는 Awake/OnEnable 에서 초기화하고 싱글톤 가드까지 도는 것이 많아서,
//   껐다 켜는 것만으로 상태가 어긋날 수 있다(라벨이 빈 값으로 덮이는 사고를 이미 겪었다).
//   Canvas 컴포넌트만 끄면 '그리기'만 멈추고 오브젝트/스크립트는 그대로다 = 부작용 0.
//
//   ★그래서 이 기능은 게임 상태를 전혀 건드리지 않는다. 일시정지/입력/커서 모두 그대로다.
//   촬영 중 아무 때나 켜고 꺼도 안전하다.
//
// [IMGUI 오버레이는 따로 꺼야 한다]
//   FpsCounter / PerfHud 는 OnGUI 로 직접 그려서 Canvas 와 무관하다.
//   캔버스만 끄면 FPS 숫자가 화면에 그대로 남아 트레일러에 박힌다. 그래서 같이 끈다.
//
// [끈 것만 정확히 되돌린다]
//   원래 꺼져 있던 캔버스(닫힌 패널 등)를 켜버리면 안 보여야 할 UI 가 나타난다.
//   그래서 '내가 끈 것'만 목록에 담고, 복구 때 그것만 되돌린다.
//
// [촬영 중 새로 뜬 UI]
//   토스트/팝업은 촬영 중에도 새로 생긴다. 매 프레임 전체 탐색은 비싸니
//   ReScanInterval 마다 한 번씩만 훑어서 새로 생긴 것을 끈다.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

public class ScreenshotMode : MonoBehaviour
{
    /// <summary>UI 를 숨겼다 되돌리는 키. 촬영용이라 정식 빌드에서도 동작한다.</summary>
    public static KeyCode ToggleKey = KeyCode.F8;

    /// <summary>지금 UI 가 숨겨진 상태인가.</summary>
    public static bool Active { get; private set; }

    private const float ReScanInterval = 0.4f;   // 촬영 중 새로 뜬 UI 를 잡는 주기(초)

    private static readonly List<Canvas> _hiddenCanvases = new();        // 내가 끈 캔버스만
    private static readonly List<MonoBehaviour> _hiddenOverlays = new(); // 내가 끈 IMGUI 오버레이만
    private float _nextScan;

    // 씬에 붙일 필요 없음: 게임 시작 시 자동 생성되고 씬 전환에도 유지된다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        var go = new GameObject("[ScreenshotMode]") { hideFlags = HideFlags.HideAndDontSave };
        go.AddComponent<ScreenshotMode>();
        DontDestroyOnLoad(go);
    }

    /// <summary>UI 숨김 토글. 게임 상태(일시정지/입력/커서)는 건드리지 않는다.</summary>
    public static void Toggle()
    {
        if (Active) Show();
        else Hide();
    }

    private static void Hide()
    {
        Active = true;
        _hiddenCanvases.Clear();
        _hiddenOverlays.Clear();
        HideNewTargets();
        Debug.Log($"[촬영 모드] UI 를 숨겼다(캔버스 {_hiddenCanvases.Count}개). {ToggleKey} 를 다시 누르면 되돌린다.");
    }

    private static void Show()
    {
        Active = false;
        int n = 0;
        foreach (var c in _hiddenCanvases)
        {
            if (c == null) continue;   // 그 사이 파괴된 것은 건너뛴다
            c.enabled = true;
            n++;
        }
        foreach (var m in _hiddenOverlays)
        {
            if (m == null) continue;
            m.enabled = true;
        }
        _hiddenCanvases.Clear();
        _hiddenOverlays.Clear();
        Debug.Log($"[촬영 모드] UI 를 되돌렸다(캔버스 {n}개).");
    }

    // 지금 켜져 있는 것만 끄고 목록에 담는다. 이미 꺼져 있던 것은 손대지 않는다.
    private static void HideNewTargets()
    {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (c == null || !c.enabled) continue;
            c.enabled = false;
            _hiddenCanvases.Add(c);
        }

        // OnGUI 로 직접 그리는 것들(Canvas 와 무관해서 위 루프가 못 잡는다).
        HideOverlay(FindAnyObjectByType<FpsCounter>());
        HideOverlay(FindAnyObjectByType<PerfHud>());
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

        if (!Active) return;
        // 촬영 중 새로 뜬 토스트/팝업 잡기. 매 프레임 전체 탐색은 비싸서 주기적으로만.
        if (Time.unscaledTime < _nextScan) return;
        _nextScan = Time.unscaledTime + ReScanInterval;
        HideNewTargets();
    }
}
