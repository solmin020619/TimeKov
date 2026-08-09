// =====================================================================
// DirectPlayBoot.cs (에디터 전용 동작)
// World 씬에서 바로 Play 를 눌렀을 때, 첫 씬이 열리기 전에 데이터를 채워둔다.
//
// [고치려는 문제]
//   직행 플레이는 DevBootstrapper 가 Awake 에서 그제서야 다운로드를 '시작'한다.
//   받는 1~3초 동안 나머지 Awake/Start 는 빈 테이블을 읽는다. 그래서
//   "아이템 없음" 같은 노란 경고가 뜨고, 더 나쁘게는 한 번만 읽고 마는 코드
//   (ShipRepairManager 의 레벨표 등)가 시트값을 영영 못 받은 채 인스펙터 값으로 돈다.
//   소비자마다 OnDataLoaded 구독을 붙이는 건 파일 수십 개짜리 두더지잡기다.
//   데이터가 '이미 있는' 상태로 첫 Awake 를 맞으면 그 문제군 전체가 사라진다.
//
// [어떻게]
//   Play 를 누르는 순간 에디터가 시트를 받아 로컬 사본을 갱신하고(PlayModeSheetSync),
//   여기서 그 파일을 동기로 읽는다. 파일 읽기라 즉시 끝나므로 프레임 0에 완비된다.
//   다운로드는 어차피 지금도 하고 있다. 순서를 앞으로 당기는 것뿐이라 총 시간은 같다.
//
// [정식 흐름은 건드리지 않는다]
//   메인메뉴/로딩 씬에서 시작하면 플래그가 안 켜지고 이 코드는 통째로 지나간다.
//   플레이어가 실제로 겪는 경로는 예전 그대로다.
// =====================================================================

#if UNITY_EDITOR

using UnityEngine;

public static class DirectPlayBoot
{
    /// <summary>에디터가 "이번 플레이는 직행이다"를 알리는 자리. 도메인 리로드를 넘어 살아남는다.</summary>
    public const string FlagKey = "TimeKov.DirectPlayBoot";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (!UnityEditor.SessionState.GetBool(FlagKey, false)) return;
        UnityEditor.SessionState.SetBool(FlagKey, false);   // 이번 플레이 1회용

        if (!GameDataHolder.I.LoadAllFromLocal())
        {
            // 사본이 없거나 검증에 걸림 -> 예전처럼 DevBootstrapper 가 받아온다(경고는 다시 뜬다).
            Debug.LogWarning("[직행 부팅] 로컬 사본이 부족해 평소대로 다운로드로 진행한다. " +
                             "시트/백업 저장 을 한 번 눌러 사본을 채워라.");
            return;
        }

        // 번역표. 여기서 깔면 LocalizationLoader 가 영구 캐시에도 같은 내용을 써두므로,
        // 언어 복원(LocalizationService)이 이 뒤에 돌아 캐시를 다시 읽어도 같은 값이 나온다.
        string loc = LocalTableSource.TryRead(LocalizationLoader.TableName);
        if (loc != null) LocalizationLoader.LoadFromCsvText(loc);

        DataBoot.MarkLoaded();
    }
}

#endif
