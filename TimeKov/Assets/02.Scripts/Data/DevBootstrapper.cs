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

        Debug.Log("[DevBoot] 로딩씬 미경유 감지 — 데이터 직접 로드 시작");

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
            if (success)
                Debug.Log("[DevBoot] 데이터 로드 완료 — 테스트 준비됨");
            else
                Debug.LogError("[DevBoot] 데이터 로드 실패 — 인터넷 연결 또는 시트 URL 확인");
        });
    }
}
