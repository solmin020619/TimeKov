// =====================================================================
// DevBootstrapper.cs
// World씬 직접 플레이(테스트) 시 데이터 로드를 자동으로 처리
//
// 사용법: World씬의 빈 오브젝트에 붙여두면 됨
// - 로딩씬을 거쳐온 경우 → DataBoot.IsLoaded == true → 즉시 패스 (무해)
// - World씬 직접 플레이 시 → DataBoot 생성 + LoadAsync 자동 호출
// =====================================================================

using UnityEngine;

public class DevBootstrapper : MonoBehaviour
{
    private void Awake()
    {
        // 로딩씬을 정상적으로 거쳐온 경우 — 아무것도 안 함
        if (DataBoot.IsLoaded) return;

        // 씬 직접 플레이는 테스트할 때 늘 하는 일이라 알림을 찍지 않는다(매번 도배됨).
        // 데이터가 안 오면 아래 실패 에러로 알게 된다.

        // DataBoot 없으면 생성
        if (DataBoot.Instance == null)
        {
            var go = new GameObject("[DataBoot]");
            DontDestroyOnLoad(go);
            go.AddComponent<DataBoot>();
        }

        // 데이터 로드 시작
        DataBoot.Instance.LoadAsync(success =>
        {
            if (!success)
                Debug.LogError("[DevBoot] 데이터 로드 실패 — 인터넷 연결 또는 시트 URL 확인");
        });
    }
}
