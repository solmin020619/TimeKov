// =====================================================================
// GameDataHolder.cs
// 전체 게임 데이터 싱글톤 (수동 작성 partial)
// 프로퍼티, LoadAll, ClearAll 은 GameDataHolder.g.cs 에 자동 생성된다
// 이 파일에는 싱글톤 패턴과 로드 진입점만 작성한다
// =====================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class GameDataHolder
{
    // 싱글톤 인스턴스
    private static GameDataHolder _instance;
    public static GameDataHolder I
    {
        get
        {
            if (_instance == null)
                _instance = new GameDataHolder();
            return _instance;
        }
    }

    private GameDataHolder() { }

    // 구글 시트에서 전체 데이터 비동기 로드 (런타임용)
    // monoBehaviour: 코루틴 실행 주체
    // onComplete: 성공 여부를 bool 로 전달하는 콜백
    public void LoadAllFromGoogle(MonoBehaviour monoBehaviour, Action<bool> onComplete)
    {
        monoBehaviour.StartCoroutine(LoadAllCoroutine(onComplete));
    }

    // 기존 데이터를 모두 지우고 구글 시트에서 재로드
    // 데이터 갱신이 필요할 때 DataBoot.ForceReload() 에서 호출
    public void ReloadAllFromGoogle(MonoBehaviour monoBehaviour, Action<bool> onComplete)
    {
        ClearAll();
        monoBehaviour.StartCoroutine(LoadAllCoroutine(onComplete));
    }

    private IEnumerator LoadAllCoroutine(Action<bool> onComplete)
    {
        var schemas = AllTableSchemas.GetAll();
        var tables = new Dictionary<string, CsvTable>();
        bool allSuccess = true;

        // 스키마 순서대로 구글 시트 다운로드
        foreach (var schema in schemas)
        {
            CsvTable downloaded = null;
            yield return CsvReader.DownloadAsync(schema.GoogleSheetUrl, t => downloaded = t);

            if (downloaded == null)
            {
                Debug.LogError($"[GameDataHolder] 다운로드 실패: {schema.TableName}");
                allSuccess = false;
                break;
            }
            tables[schema.TableName] = downloaded;
        }

        if (!allSuccess) { onComplete?.Invoke(false); yield break; }

        // 검증 실패 시 로드 중단
        if (!TableValidator.ValidateAll(schemas, tables))
        {
            onComplete?.Invoke(false);
            yield break;
        }

        // 자동생성 LoadAll 호출 (GameDataHolder.g.cs)
        LoadAll(tables);
        onComplete?.Invoke(true);
    }

    // partial void 선언 — g.cs 없어도 컴파일 가능
    partial void LoadAll(Dictionary<string, CsvTable> tables);
    partial void ClearAll();
}