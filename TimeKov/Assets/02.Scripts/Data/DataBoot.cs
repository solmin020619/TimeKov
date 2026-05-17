// =====================================================================
// DataBoot.cs
// 런타임 데이터 로드 완료를 알리는 부트스트랩
// 구버전 DataStore.IsLoaded 를 대체한다
// =====================================================================

using System;
using UnityEngine;

public class DataBoot : MonoBehaviour
{
    // 데이터 로드 완료 여부
    public static bool IsLoaded { get; private set; }

    // 로드 완료 이벤트
    public static event Action OnDataLoaded;

    private void Awake()
    {
        // 구글 시트에서 비동기 로드
        GameDataHolder.I.LoadAllFromGoogle(this, success =>
        {
            if (success)
            {
                IsLoaded = true;
                OnDataLoaded?.Invoke();
                Debug.Log("[DataBoot] 데이터 로드 완료");
            }
            else
            {
                Debug.LogError("[DataBoot] 데이터 로드 실패");
            }
        });
    }
}
