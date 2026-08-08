using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 시전(채널) 게이지 - 화면 상단 중앙에 "무엇이 얼마나 진행됐는지"를 선으로 채워 보여주는 공용 HUD.
//   워프 충전(WarpManager)과 귀환석 채널링(ReturnStoneManager)이 같은 UI 를 공유한다.
//   지점에 서 있으면 게이지가 차오르고, 다 차면 발동. 남은 시간도 함께 표시.
//
// [08-02] 런타임 자체생성 -> 씬 실물 오브젝트로 전환.
//   이전에는 Awake 에서 계층을 통째로 코드로 만들었다. 그러면 에디터에 존재하지 않아서
//   (1) 로컬라이징 컴포넌트를 붙일 수 없고 (2) 위치/색을 인스펙터에서 못 고친다.
//   지금은 Canvas 아래 실물로 존재하고, 이 스크립트는 '참조를 쓰기만' 한다.
//   (생성용 에디터 빌더는 08-03 에 팀 합의로 제거. 다시 구워야 하면 git 이력에서 꺼낸다)
//
//   단, 스프라이트는 예외로 런타임에 다시 넣는다: UISpriteFactory 가 만드는 스프라이트는 에셋이 아니라
//   메모리 생성물이라, 프리팹에 구워두면 유니티 재시작 때 사라진다(이 프로젝트에서 이미 겪은 함정).
//
//   숨김은 오브젝트 비활성이 아니라 CanvasGroup 알파로 한다(항상 활성 = Instance 를 항상 찾을 수 있음).
[SingleInstance]
public class CastGaugeUI : MonoBehaviour
{
    public static CastGaugeUI Instance { get; private set; }

    [Header("구성 요소 (빌더가 자동 연결)")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image pill;      // 알약 배경
    [SerializeField] private Image icon;      // 좌측 아이콘(기본 = 시안 링)
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private TextMeshProUGUI count;
    [SerializeField] private Image track;     // 진행바 배경
    [SerializeField] private Image fill;      // 채워지는 게이지

    [Header("색")]
    [SerializeField] private Color accent = new Color32(80, 200, 235, 255);   // 스킬바/워프와 동일 시안
    [SerializeField] private Color pillBg = new Color(0.06f, 0.09f, 0.13f, 0.88f);
    [SerializeField] private Color trackBg = new Color(0.02f, 0.03f, 0.05f, 0.9f);

    [Header("연출")]
    [SerializeField] private float fadeDuration = 0.15f;

    private Coroutine _fadeCo;
    private int _lastShownSec = -1;

    /// <summary>씬에 있는 인스턴스를 돌려준다(없으면 에러 로그). 호출측은 null 안전하게 쓰면 된다.</summary>
    public static CastGaugeUI Ensure()
    {
        if (Instance != null) return Instance;
        Instance = FindAnyObjectByType<CastGaugeUI>(FindObjectsInactive.Include);
        if (Instance == null)
            Debug.LogError("[CastGaugeUI] 씬 Canvas 에 시전 게이지가 없다. 메뉴 Tools/TIMEKOV/시전 게이지 UI 생성 을 실행해라.");
        return Instance;
    }

    private void Awake()
    {
        if (UIDuplicateGuard.Report(Instance, this)) { Destroy(gameObject); return; }
        Instance = this;
        ApplyRuntimeSprites();
        SetVisible(false, instant: true);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // UISpriteFactory 생성물은 에셋이 아니라 직렬화가 안 된다 -> 매 실행 다시 넣는다.
    private void ApplyRuntimeSprites()
    {
        if (pill != null)
        {
            pill.sprite = UISpriteFactory.RoundedRect(64, 20);
            pill.type = Image.Type.Sliced;
            pill.color = pillBg;
        }
        if (track != null)
        {
            track.sprite = UISpriteFactory.RoundedRect(32, 12);
            track.type = Image.Type.Sliced;
            track.color = trackBg;
        }
        if (fill != null)
        {
            fill.sprite = UISpriteFactory.RoundedRect(32, 12);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.color = accent;
        }
        if (count != null) count.color = accent;
    }

    // 공개 API

    /// <summary>게이지 표시 시작(0%). label 은 "워프 중"/"귀환 중" 등, icon 은 없으면 기본 링.</summary>
    public void Begin(string labelText, Sprite iconSprite = null)
    {
        if (label != null) label.text = labelText;
        if (icon != null)
        {
            // 커스텀 아이콘은 원색 그대로(흰색), 없으면 기본 시안 링.
            if (iconSprite != null) { icon.sprite = iconSprite; icon.color = Color.white; }
            else { icon.sprite = UISpriteFactory.Ring(64, 6f); icon.color = accent; }
        }
        if (fill != null) fill.fillAmount = 0f;
        _lastShownSec = -1;
        if (count != null) count.text = "";
        SetVisible(true, instant: false);
    }

    /// <summary>매 프레임 갱신. progress01=0~1(다 차면 발동), remainingSeconds=남은 시간.</summary>
    public void Report(float progress01, float remainingSeconds)
    {
        if (fill != null) fill.fillAmount = Mathf.Clamp01(progress01);

        // 초 단위가 바뀔 때만 텍스트 갱신(매 프레임 문자열 할당 방지).
        int s = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        if (s != _lastShownSec)
        {
            _lastShownSec = s;
            if (count != null) count.text = $"{s / 60}:{s % 60:00}";
        }
    }

    /// <summary>완료/취소 공통 - 게이지를 숨긴다.</summary>
    public void Hide() => SetVisible(false, instant: false);

    // 표시/페이드

    private void SetVisible(bool show, bool instant)
    {
        if (group == null) return;
        if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
        if (instant) { group.alpha = show ? 1f : 0f; return; }
        _fadeCo = StartCoroutine(FadeRoutine(show ? 1f : 0f));
    }

    private IEnumerator FadeRoutine(float target)
    {
        float from = group.alpha;
        float t = 0f;
        float dur = Mathf.Max(0.01f, fadeDuration);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;   // 정지 중(워프/귀환 연출)에도 페이드가 돌아야 한다
            group.alpha = Mathf.Lerp(from, target, t / dur);
            yield return null;
        }
        group.alpha = target;
        _fadeCo = null;
    }
}
