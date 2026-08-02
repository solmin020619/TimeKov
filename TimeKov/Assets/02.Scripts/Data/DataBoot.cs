// =====================================================================
// DataBoot.cs
// 데이터 로드 싱글톤 (DontDestroyOnLoad)
//
// 설계 원칙
//   - 씬 전환 시 파괴되지 않고 앱 전체에서 1개만 존재
//   - 직접 다운로드 하지 않음 — LoadAsync() 를 호출한 쪽(LoadingSceneManager)이
//     명시적으로 로드 시점을 제어
//   - IsLoaded = true 이면 재다운로드 없이 즉시 콜백
//   - ForceReload() 로만 강제 재다운로드 가능 (ClearAll 포함)
// =====================================================================

using System;
using UnityEngine;

public class DataBoot : MonoBehaviour
{
    public static DataBoot Instance { get; private set; }

    // 데이터 로드 완료 여부
    public static bool IsLoaded { get; private set; }

    // 로드 완료 이벤트 (구독자가 없어도 안전)
    public static event Action OnDataLoaded;

    void Awake()
    {
        // 씬 전환 시 중복 생성 방지
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── 외부 호출 API ───────────────────────────────────────────────

    /// <summary>
    /// 데이터 로드 요청.
    /// 이미 로드됐으면 onComplete(true) 즉시 호출.
    /// LoadingSceneManager 에서 호출한다.
    /// </summary>
    public void LoadAsync(Action<bool> onComplete)
    {
        if (IsLoaded)
        {
            onComplete?.Invoke(true);
            return;
        }

        GameDataHolder.I.LoadAllFromGoogle(this, success =>
        {
            if (!success)
            {
                Debug.LogError("[DataBoot] 데이터 로드 실패");
                onComplete?.Invoke(false);
                return;
            }

            // 게임 데이터 로드 성공 → 현지화 테이블 로드 (실패해도 KO로 진행)
            LocalizationLoader.LoadAsync(this, _ =>
            {
                IsLoaded = true;
                OnDataLoaded?.Invoke();
                Debug.Log("[DataBoot] 데이터 로드 완료");
                onComplete?.Invoke(true);
            });
        });
    }

    /// <summary>
    /// 기존 데이터를 초기화하고 구글 시트에서 강제 재다운로드.
    /// 데이터 갱신이 필요한 경우에만 사용.
    /// </summary>
    public void ForceReload(Action<bool> onComplete)
    {
        IsLoaded = false;

        GameDataHolder.I.ReloadAllFromGoogle(this, success =>
        {
            if (!success)
            {
                Debug.LogError("[DataBoot] 데이터 강제 재로드 실패");
                onComplete?.Invoke(false);
                return;
            }

            LocalizationLoader.LoadAsync(this, _ =>
            {
                IsLoaded = true;
                OnDataLoaded?.Invoke();
                Debug.Log("[DataBoot] 데이터 강제 재로드 완료");
                onComplete?.Invoke(true);
            });
        });
    }
}
