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

    // 한 번에 띄울 동시 다운로드 상한. 시트 8장 기준 넉넉. 구글 throttle 걸리면 낮춰라.
    private const int MaxConcurrentDownloads = 6;

    private IEnumerator LoadAllCoroutine(Action<bool> onComplete)
    {
        var schemas = AllTableSchemas.GetAll();

        // 시트별 (이름, URL) 목록 구성
        var sources = new List<(string name, string url)>(schemas.Count);
        foreach (var schema in schemas)
            sources.Add((schema.TableName, schema.GoogleSheetUrl));

        // 병렬 다운로드 (직렬 대비 왕복지연 합 -> 가장 느린 한 개 수준으로 단축)
        Dictionary<string, CsvTable> tables = null;
        List<string> failed = null;
        yield return CsvReader.DownloadAllAsync(
            sources,
            MaxConcurrentDownloads,
            (res, fail) => { tables = res ?? new Dictionary<string, CsvTable>(); failed = fail; });

        // 다운로드에 실패해도 로컬 사본이 있으면 그걸로 뜬다.
        // 2026-08-02 에 DropTable 시트가 삭제돼 게임이 아예 안 떴다. 시트 한 장 사고로
        // 전원이 작업을 못 하는 상태는 막아야 한다(원본이 시트라는 사실은 그대로다).
        if (failed != null && failed.Count > 0)
        {
            var stillFailed = new List<string>();
            foreach (var name in failed)
            {
                string text = LocalTableSource.TryRead(name);
                if (text == null) { stillFailed.Add(name); continue; }
                tables[name] = CsvReader.Parse(text);
                Debug.LogWarning($"[테이블] {name} 다운로드 실패 -> 로컬 사본으로 대체했다. 시트 상태를 확인해라.");
            }
            failed = stillFailed;
        }

        if (failed != null && failed.Count > 0)
        {
            Debug.LogError($"[GameDataHolder] 다운로드 실패: {string.Join(", ", failed)}");
            onComplete?.Invoke(false);
            yield break;
        }

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