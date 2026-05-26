# TimeKov — 가공 진행 게이지 (Arrow Gauge) 구현 가이드

기존 `»»` 화살표를 **좌→우로 채워지는 진행 게이지(Filled Arrow)**로 교체합니다.
가공이 진행 중임을 직관적으로 보여주는 인디케이터예요.

---

## 📁 폴더 구조

```
TimeKov/
├── README.md                ← 이 파일 (구현 가이드)
├── Sprites/
│   ├── arrow_track.png      ← 빈 화살표 (배경, 800×560)
│   ├── arrow_fill.png       ← 채워지는 골드 화살표 (전경, 800×560)
│   └── arrow.svg            ← 벡터 원본 (재export용)
├── Preview/
│   ├── timekov.html         ← 동작 미리보기 (브라우저에서 열기)
│   └── assets/...           ← 미리보기에 쓰는 이미지
└── Reference/
    ├── original-ui.png      ← 원본 UI (화살표 살아있는 상태)
    └── ui-with-arrow-removed.png  ← 화살표 제거된 배경
```

---

## 🎯 핵심 동작 (한 문장 요약)

> **가공이 진행되는 동안 화살표가 0% → 100% 로 채워지고, 다 차면 0%로 리셋되어 다시 채워진다.**
> (마인크래프트 화로 진행바와 동일한 패턴)

기본 1사이클 = **3.5초**. 가공 시간이 다르면 그에 맞춰 조절.

---

## 🧩 Unity 구현 (Step by Step)

### 1) 스프라이트 임포트
1. `Sprites/arrow_track.png`, `arrow_fill.png`를 Unity 프로젝트 `Assets/UI/TimeKov/` 폴더에 드래그.
2. 둘 다 Inspector에서 다음 설정:
   - **Texture Type:** `Sprite (2D and UI)`
   - **Filter Mode:** `Bilinear`
   - **Compression:** `None` (또는 High Quality) — UI 라인이 깔끔하게 나오도록
   - **Pixels Per Unit:** 기본값 100 그대로
   - **Generate Mip Maps:** OFF
3. Apply.

### 2) UI 계층 만들기
기존 `»»` 화살표 GameObject가 있던 자리에 새 구조를 만듭니다.

```
ProcessingGauge      (RectTransform, 빈 GameObject)
├── ArrowTrack       (Image, arrow_track.png)
└── ArrowFill        (Image, arrow_fill.png)
```

**RectTransform 권장값 (1920×1080 캔버스 기준):**
- `ProcessingGauge` 위치: 기존 화살표가 있던 위치 그대로 (왼쪽 박스와 오른쪽 박스 사이)
- 크기: 너비 **약 95px**, 높이 **약 65px** (원본 화살표와 비슷한 크기)
- `ArrowTrack`: `Anchor = Stretch all`, `Offsets = 0,0,0,0` (부모 가득 채움)
- `ArrowFill`: 동일하게 부모 가득 채움 (Track 위에 겹침)

### 3) ArrowFill 설정 (핵심!)
ArrowFill의 Image 컴포넌트에서:
- **Image Type:** `Filled` ← 이게 핵심
- **Fill Method:** `Horizontal`
- **Fill Origin:** `Left`
- **Fill Amount:** `0` (런타임에 스크립트가 0→1로 보간)
- **Preserve Aspect:** OFF

> 이렇게 하면 `fillAmount`를 0에서 1로 올릴 때 화살표가 왼쪽부터 점점 채워집니다.

### 4) 스크립트 붙이기 — `ProcessingGauge.cs`

`ProcessingGauge` GameObject에 아래 스크립트를 어태치하고, Inspector에서 `arrowFill` 필드에 `ArrowFill` Image 컴포넌트를 드래그해서 연결.

```csharp
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ProcessingGauge : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image arrowFill;          // 채워지는 골드 화살표
    [SerializeField] private CanvasGroup canvasGroup;  // (선택) 전체 페이드용

    [Header("Timing")]
    [Tooltip("한 사이클(0%→100%)에 걸리는 시간(초)")]
    [SerializeField] private float cycleDuration = 3.5f;

    [Tooltip("자동 반복")]
    [SerializeField] private bool loop = true;

    [Header("Optional FX")]
    [Tooltip("0%일 때 살짝 투명하게 시작 (자연스러운 페이드 인)")]
    [SerializeField] private bool fadeInOnEmpty = false;

    [Header("State (read-only)")]
    [SerializeField, Range(0f, 1f)] private float progress01 = 0f;

    private float elapsed;
    private bool running = true;

    void Reset()
    {
        // Editor에서 컴포넌트 붙이면 자식의 ArrowFill 자동 탐색
        if (arrowFill == null && transform.childCount > 1)
            arrowFill = transform.GetChild(1).GetComponent<Image>();
    }

    void OnValidate()
    {
        // Inspector에서 progress01 조절하면 즉시 반영 (에디터 미리보기용)
        if (arrowFill != null) arrowFill.fillAmount = progress01;
    }

    void Update()
    {
        if (!running || arrowFill == null) return;

        elapsed += Time.deltaTime;
        progress01 = Mathf.Clamp01(elapsed / cycleDuration);
        arrowFill.fillAmount = progress01;

        if (progress01 >= 1f)
        {
            if (loop)
            {
                elapsed = 0f;
                // 사이클 완료 이벤트가 필요하면 여기서 발사
                // OnCycleComplete?.Invoke();
            }
            else
            {
                running = false;
            }
        }

        if (fadeInOnEmpty && canvasGroup != null)
            canvasGroup.alpha = Mathf.Lerp(0.6f, 1f, progress01);
    }

    // ── 외부 API ──────────────────────────────────────

    /// <summary>외부에서 가공 시작</summary>
    public void StartProcessing()
    {
        elapsed = 0f;
        progress01 = 0f;
        running = true;
        gameObject.SetActive(true);
    }

    /// <summary>가공 일시정지</summary>
    public void Pause() => running = false;

    /// <summary>가공 재개</summary>
    public void Resume() => running = true;

    /// <summary>가공 완료 — 100% 채워진 채로 멈춤</summary>
    public void Complete()
    {
        progress01 = 1f;
        elapsed = cycleDuration;
        arrowFill.fillAmount = 1f;
        running = false;
    }

    /// <summary>가공 중단 — 0% 비어있는 채로 멈춤 (또는 숨김)</summary>
    public void StopAndHide()
    {
        running = false;
        progress01 = 0f;
        arrowFill.fillAmount = 0f;
        gameObject.SetActive(false);
    }

    /// <summary>외부 게이지(실제 가공 진행도)에 맞춰 fillAmount를 강제 세팅</summary>
    public void SetProgress(float t01)
    {
        progress01 = Mathf.Clamp01(t01);
        arrowFill.fillAmount = progress01;
    }
}
```

### 5) 외부 시스템과 연동
가공 시작/완료를 처리하는 매니저(예: `CraftingManager.cs`)에서:

```csharp
[SerializeField] private ProcessingGauge gauge;

public void OnCraftStart() {
    gauge.cycleDuration = recipe.duration; // 레시피마다 다른 시간
    gauge.StartProcessing();
}

public void OnCraftFinish() {
    gauge.Complete();
}
```

**실제 가공 진행도와 동기화하려면** (예: 서버에서 진행도를 받는다면):
```csharp
void Update() {
    float t = (Time.time - craftStartTime) / craftDuration;
    gauge.SetProgress(t);
}
```
이 경우 `ProcessingGauge`의 `Update`는 안 돌도록 `Pause()` 시켜두세요.

---

## 🎨 시각 옵션 (취향에 따라)

### A) 깜빡임(반짝임) 효과 추가
화살표가 채워질 때 끝부분이 반짝이게 하려면 `ArrowFill`에 `Outline` 컴포넌트 추가 + 알파 펄스. 또는 작은 파티클을 화살표 머리 위치에 부착.

### B) 색상 변경 (시즌/등급별)
`ArrowFill`의 Color 속성을 흰색으로 두고 코드에서 색을 곱해서 사용:
```csharp
arrowFill.color = new Color(0.4f, 0.7f, 1f); // 파란 게이지
```
스프라이트의 그라데이션 색감은 유지하면서 톤이 바뀝니다.

### C) 사이즈 조정
화살표가 너무 크면 `ProcessingGauge`의 RectTransform 너비/높이를 줄이세요. SVG 원본이라 어떤 크기에서도 깨지지 않음 (스프라이트는 800×560으로 import되어 있음).

### D) 가공 완료 임팩트
`Complete()` 호출 시 짧은 스케일 펀치 효과:
```csharp
public void Complete() {
    // ... 기존 코드 ...
    transform.localScale = Vector3.one * 1.15f;
    // DOTween 쓴다면: transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack);
    // 또는 코루틴으로 직접
}
```

---

## ⚠️ 주의사항 / 트러블슈팅

| 증상 | 원인 / 해결 |
|---|---|
| 화살표가 안 채워짐 | `Image Type`이 `Filled`로 되어 있는지 확인 |
| 오른쪽부터 채워짐 | `Fill Origin = Left`로 변경 |
| 화살표가 위/아래로 채워짐 | `Fill Method = Horizontal`로 변경 |
| 화살표 모서리가 흐릿함 | `Filter Mode = Point (no filter)` 시도 (픽셀 아트 느낌) |
| Canvas Render Mode가 World Space | `Pixels Per Unit Multiplier` 조정 |
| 원본 `»»` 화살표가 아직 보임 | 기존 화살표 GameObject 비활성화/삭제 했는지 확인 |

---

## 🔄 Before / After 비교

미리보기는 `Preview/timekov.html`을 브라우저에서 열어보세요. 하단의 **BEFORE / AFTER** 토글로 비교할 수 있습니다.

- **BEFORE:** 정적 `»»` 화살표 — 가공 중인지 끝났는지 알 수 없음
- **AFTER:** 좌→우 채워지는 골드 게이지 — 진행 상태가 한 눈에

---

## 📐 디자인 스펙 요약

| 항목 | 값 |
|---|---|
| 게이지 사이즈 (1920×1080 기준) | 약 95 × 65 px |
| 위치 | 왼쪽 박스 우측 끝과 오른쪽 박스 좌측 끝 사이 (가운데) |
| 진행 색상 | `#ffb24a → #ffd178 → #ffe7a8` (좌→우 그라데이션, 골드) |
| 트랙 색상 | 검정 60% + 파란 테두리 (`#c8e1f0` 70%) |
| 1사이클 | 3.5초 (가공 레시피에 따라 조정) |
| 채움 방식 | Image.Filled · Horizontal · Origin Left |

---

## ❓ 자주 받을 질문

**Q. 가공이 끝났을 때 게이지가 사라져야 하나, 100% 채워진 상태로 남아야 하나?**
A. 둘 다 일반적. 권장:
- 결과물 받기 전까지 = **100% 채워진 상태로 유지** (수확 가능 신호)
- 결과물 수령 후 = **숨김 (`StopAndHide()`)**

**Q. 동시에 여러 가공기가 돌아가면?**
A. `ProcessingGauge`를 각 가공기마다 인스턴스로 두면 됨. 스크립트는 인스턴스 단위로 독립 동작.

**Q. 일시정지 시 시각적으로 알리고 싶다면?**
A. `Pause()` 호출 후 `arrowFill.color`를 회색으로 바꾸거나, 깜빡이는 자식 오브젝트를 활성화. 또는 별도 일시정지 아이콘 오버레이.

---

문의/이상 동작 발견 시 `Reference/` 폴더의 원본 이미지와 `Preview/timekov.html`을 비교 확인.
