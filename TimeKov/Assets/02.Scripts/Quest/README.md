# 길라잡이 퀘스트 시스템 — 디자이너 가이드

게임 시작 시 좌상단에 카테고리별 퀘스트 패널이 뜨는 길라잡이 시스템입니다. 5분 분량 + "게임의 모든 걸 해보게" 하는 게 목적.

## 진행 모델

| 축 | 동작 |
|---|---|
| 카테고리끼리 | **동시 진행** (전투/건축/공장 처음부터 다 표시) |
| 카테고리 안 퀘스트 | **순서대로** (하나 깨야 다음 슬라이드 인) |
| 한 퀘스트 안 Objective | **병렬** (다 깨야 퀘스트 완료) |

## 데이터 구조 (3단 SO 트리)

```
TutorialSO          — 전체 묶음, categories[]
  └ CategorySO      — "전투", "건축", quests[]
      └ QuestSO     — "이동하기", objectives[]
          └ ObjectiveSO — "WASD 3m 이동" (추상, 종류별 구체 클래스)
```

기획자가 하는 일은 SO 슬롯 드래그뿐. 코드 변경 0줄. 새 Objective 종류 추가될 때만 프로그래머가 클래스 한 개 만듦.

---

## 1. SO 만들기

`Assets > Create > Quest > ...` 메뉴에서 생성.

### TutorialSO
- `categories[]`: 카테고리 순서대로
- `savePrefix`: 본 게임 메인 퀘스트와 충돌 방지. ⚠️ 변경 시 진행도 리셋

### CategorySO
- `id`: ⚠️ 변경 시 해당 카테고리 진행도 영구 손실
- `quests[]`: 퀘스트 순서대로

### QuestSO
- `objectives[]`: 모든 퀘스트는 헤더(제목) + ObjectiveLine 인덴트 구조 (1개든 N개든 동일 시각, 엔드필드 패턴)
- `onShown` / `onActivated` / `onCompleted`: 디자이너 훅 (사운드, 카메라, NPC 스폰 등)

### ObjectiveSO 공통
- `label`: UI에 표시할 텍스트
- `Timing`: 클래스가 강제 (인스펙터에 안 뜸). 새 Objective 종류 만들 때 결정

### 기본 제공 Objective 종류
- **PressKey**: 특정 키 N번 누르기 (입력형)
- **MoveDistance**: N미터 이동 (입력형)
- **EnemyKill**: 특정 적 또는 아무 적 N마리 처치 (게임 이벤트형). `enemyId` 비우면 모든 적, 채우면 `MeleeEnemyData.enemyName`과 일치하는 적만
- **ReachTrigger**: 특정 트리거 영역 진입 (게임 이벤트형)

---

## 2. 씬 셋업

새 씬에 퀘스트 시스템 붙일 때 순서.

### ① QuestSystem GameObject
빈 GameObject 만들고 이름 `QuestSystem`.
- `QuestManager` 컴포넌트 부착
- 인스펙터의 `tutorial` 슬롯에 TutorialSO 에셋 드래그

### ② PlayerWatcher GameObject (임시)
빈 GameObject 만들고 이름 `PlayerWatcher`.
- `PlayerMovementWatcher` 컴포넌트 부착
- `playerTransform` 비워두면 `Player` 태그로 자동 검색. 명시 할당 권장
- `watchedKeys`: 퀘스트에서 쓸 키 목록 (기본값: WASD, Space, B, E, F, 마우스 좌/우)

> **왜 임시?** Player/ 폴더 수정 금지 제약 우회용. 나중에 Player/ 풀리면 `PlayerController`가 직접 `InputBus.RaiseKeyDown` / `GameEvents.RaiseMovedDelta` 호출하도록 이전 → PlayerWatcher 제거.

### ③ Player 태그 확인
플레이어 GameObject에 `Player` 태그가 붙어 있어야 합니다 (PlayerWatcher 자동 검색용 + QuestTrigger 충돌 판정용).
- 안 붙어 있으면: `Edit > Project Settings > Tags and Layers`에서 `Player` 태그 추가 후 플레이어 오브젝트에 부여

### ④ QuestPanelUI 배치
UI Canvas에 QuestPanelUI 프리팹 배치 (좌상단 앵커).
- 프리팹 셋업은 별도 작업 (다음 단계)

### ⑤ QuestTrigger (선택, ReachTrigger Objective 쓸 때만)
지역 진입 퀘스트가 있는 경우:
- 트리거 콜라이더(`isTrigger = true`) 있는 GameObject에 `QuestTrigger` 컴포넌트 부착
- `triggerId`: ReachTriggerObjective의 `targetTriggerId`와 일치시킴

---

## 3. ⚠️ 카운트형 퀘스트 디자인 룰

**진행도는 완료 시점에만 저장됨.** 진행 중 종료 시 카운트는 0으로 리셋.

### 안전한 패턴 (재시도 가능)
- WASD 4번 누르기 (PressKey requiredCount=4): OK
- 100m 이동 (MoveDistance): OK

### ⚠️ 함정 패턴 (한정 자원)
- 골렘 2마리 처치 (EnemyKill requiredCount=2): 첫 골렘 죽이고 종료 → 재시작 시 골렘 1마리만 남음 → **영구 미완료**

### 해결 방법

**1) N=1로 쪼개기 (권장, 코드 변경 0)**
- 퀘스트1: "골렘 처치" (N=1)
- 퀘스트2: "골렘 한 마리 더 처치" (N=1)
- 두 번째 골렘은 첫 번째 깬 후 NPC가 스폰하거나 `onCompleted` UnityEvent로 트리거

**2) 무한 리스폰 시스템 (전투 튜토리얼 권장)**
- 적 스폰 매니저가 "이 지역에서 적이 죽으면 N초 후 재스폰"
- 튜토리얼 끝나면 `onCompleted` UnityEvent에서 스폰 매니저 OFF

**3) N>1 그대로 쓰기 (절대 비권장)**
- EnemyKill에서 인스펙터에 경고 LogWarning 뜸

---

## 4. 새 Objective 종류 만들기 (프로그래머 작업)

`ObjectiveSO` 상속 + 다음 구현:

| 구현 항목 | 설명 |
|---|---|
| `Timing` (abstract) | `OnUIActivated`(입력형) / `OnUIPresented`(이벤트형) |
| `Activate()` / `Deactivate()` | 이벤트 구독/해제 |
| `Progress` | 0~1 진행도 |
| `GetDisplayLabel()` | 카운트형이면 `(N/M)` 추가 |
| `IsAlreadySatisfied()` | 환경 상태형(ReachTrigger 등)만 |
| `OnValidate()` | `requiredCount = Mathf.Max(1, ...)` 검증 |
| `[CreateAssetMenu]` | 어트리뷰트 |

새 게임 이벤트가 필요하면 `GameEvents.cs`에 다음 추가:
1. `event Action<...> OnXxx`
2. `RaiseXxx(...)` 메서드
3. `Reset()`에 `OnXxx = null` 잊지 말 것
4. 이벤트 발생 지점에서 `Raise` 호출

---

## 5. QA 도구

- **에디터**: `QuestManager` 컴포넌트 우클릭 → "Reset All Progress"
- **빌드**: `QuestManager.Instance.ResetAll()`
- **패널 토글**: Tab 키 (인스펙터에서 변경 가능)

---

## 6. 빌드 테스트 항목 (코드 리뷰로 못 잡는 것)

- 카테고리 3~4개 동시 표시 시각적 밀도 — 좌상단 빡빡하면 max-height + 스크롤 추가
- 1.2초 시퀀스가 5분 누적 시 답답한지 — 길면 0.8~1.0초로 단축
- Tab 토글 반응성 + 다른 UI(인벤토리/메뉴) 충돌
- "골렘 2마리" 시나리오 실험 — 1마리 잡고 종료 후 재시작 → 진짜 막히는지 확인
- `startGracePeriod` 0.1초 체감 — 정상 사용자에게 거슬리면 OnUIActivated에선 0으로
- 카테고리 동시 슬라이드 인 우르륵 — 거슬리면 cascade 0.15초 간격 추가

---

## 7. 프로그래머 통합 노트 (TimeKov 한정)

이 시스템은 **이벤트 버스 패턴**이라 누군가 `Raise*` 메서드를 호출해야 동작합니다. 현재 프로젝트 결선 상태:

| 이벤트 | 호출 위치 | 상태 |
|---|---|---|
| `InputBus.RaiseKeyDown(key)` | `PlayerMovementWatcher.Update` | ✅ 결선됨 |
| `GameEvents.RaiseMovedDelta(delta)` | `PlayerMovementWatcher.Update` | ✅ 결선됨 |
| `GameEvents.RaiseEnemyKilled(monsterId)` | `EnemyHealth.Die()` | ✅ 결선됨 (`EnemyBrain.Data.enemyName` ID) |
| `GameEvents.RaiseTriggerEnter/Exit` | `QuestTrigger.OnTriggerEnter/Exit` | ✅ 결선됨 (Player 태그 필요) |

> 점프 카운트가 필요하면 `PressKeyObjective(KeyCode.Space)`로 사실상 동일 효과. 별도 점프 감지 휴리스틱(Y 속도 임계값)은 false positive 위험 있어 제거됨.

### 향후 정리
- **PlayerMovementWatcher 제거 예정**: Player/ 폴더 풀리면 `PlayerController`가 직접 `InputBus`/`GameEvents` 호출. 인터페이스(정적 이벤트 클래스)는 안 바뀌니 코드 한쪽만 옮기면 됨.
- **EnemyBrain 수정 사항**: `public MeleeEnemyData Data => data;` 한 줄 추가됨 (외부에서 enemyName 접근용). 다른 시스템에 영향 없음.
- **EnemyHealth.Die() 수정 사항**: `isDead = true` 직후 3줄 추가 (RaiseEnemyKilled 호출). 기존 동작 영향 없음.

---

## 8. 핵심 정책 요약 (시스템 손볼 때 반드시 지킬 것)

1. **이벤트 구독은 명명 메서드만** — 람다(`+= _ => ...`)는 메모리 누수 보장
2. **SO 런타임 상태는 `[NonSerialized]` + `Instantiate` 복제** — SO 원본 절대 건드리지 말 것
3. **정적 이벤트는 `[RuntimeInitializeOnLoadMethod]`로 리셋** — 도메인 리로드 함정
4. **UI에서 `is EnemyKillObjective` 같은 다운캐스팅 금지** — `GetDisplayLabel()` 가상 메서드 사용
5. **`PlayerPrefs` 직접 호출 금지** — `IQuestSaveStorage` 인터페이스로
6. **`OnQuestShown`과 `OnQuestActivated` 분리 유지** — 고인물 방어 정책의 핵심
7. **`ActivationTiming`은 클래스 abstract 프로퍼티** — 인스펙터 필드로 만들지 말 것
8. **`BeginAll`은 UI Setup이 단독 호출** — 매니저 Start나 ResetAll에서 호출 금지
9. **이벤트 발화 → Activate 순서** (CheckQuestComplete가 인덱스 진행시켜도 quest 참조 정확)
10. **`Time.unscaledTime` / `WaitForSecondsRealtime` 사용** — 일시정지(`Time.timeScale=0`) 호환

상세 배경은 핸드오프 문서 참조.
