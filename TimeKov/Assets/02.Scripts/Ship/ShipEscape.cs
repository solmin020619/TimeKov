using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 우주선 완전 수리(Lv.최대) 후 "탈출" = 게임 클리어 처리.
// 지금은 연출 없이 간단히: 화면을 검게 페이드아웃 -> 메인 메뉴 씬 로드 -> 페이드인.
//   ShipRepairUI 의 "탈출하기" 버튼이 Run() 을 호출한다.
//   엔딩 연출(자막/시퀀스 등)은 나중에 이 안에서 확장하면 된다.
public static class ShipEscape
{
    // ★빌드 세팅(Scenes In Build)에 포함돼 있어야 이름으로 로드된다.
    //   실제 파일: Assets/01.Scenes/01.BuildScene/MainMenu.unity
    public const string MainMenuScene = "MainMenu";

    // 탈출(게임 클리어) 가능 조건 = 우주선 완전 수리(Lv.최대) + 시간에너지 전송률 100%.
    //   ★기획: 탈출조건은 우주선 최대레벨. 단 최종 전용부품(엔진)이 전송률 75% 보상이라
    //     그것만으론 75% 에서 끝나버려서, 마지막 25% 까지 밀어야 진짜 끝 = 100% 를 AND 로 요구한다.
    //     (ShipRepairManager.cs 주석의 "Lv5 AND 시간에너지 100%" 를 여기서 구현.)
    //   전송기(TransmissionManager)가 씬에 없으면(우주선 단독 테스트 씬) 전송률 조건은 생략한다.
    public static bool CanEscape()
    {
        var ship = ShipRepairManager.Instance;
        if (ship == null || !ship.IsFullyRepaired) return false;
        var tm = TransmissionManager.Instance;
        return tm == null || tm.TransmissionRate >= TransmissionManager.MaxRate;
    }

    private static bool _running;

    public static void Run()
    {
        if (_running) return;
        _running = true;

        var go = new GameObject("ShipEscapeTransition");
        Object.DontDestroyOnLoad(go);   // 씬 로드 넘어가도 페이드가 유지되게
        go.AddComponent<Fader>();
    }

    // 런타임 생성 풀스크린 검은 오버레이. 씬 세팅 불필요(코드로만).
    private class Fader : MonoBehaviour
    {
        private Image _black;

        private void Start()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;   // 다른 UI 위

            _black = new GameObject("Black").AddComponent<Image>();
            _black.transform.SetParent(transform, false);
            var rt = _black.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _black.color = new Color(0f, 0f, 0f, 0f);

            StartCoroutine(Co());
        }

        private IEnumerator Co()
        {
            yield return Fade(0f, 1f, 1.2f);   // 검게(페이드 아웃)

            Time.timeScale = 1f;               // UI 가 timeScale 을 멈춰뒀을 수 있으니 로드 전 정상화
            SceneManager.LoadScene(MainMenuScene);
            yield return null;                 // 새 씬 한 프레임 렌더 대기

            yield return Fade(1f, 0f, 0.5f);   // 메인 메뉴에서 밝게(페이드 인)
            _running = false;
            Destroy(gameObject);
        }

        // timeScale 영향 없이(unscaled) 알파 보간.
        private IEnumerator Fade(float from, float to, float dur)
        {
            for (float e = 0f; e < dur; e += Time.unscaledDeltaTime)
            {
                _black.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, e / dur));
                yield return null;
            }
            _black.color = new Color(0f, 0f, 0f, to);
        }
    }
}
