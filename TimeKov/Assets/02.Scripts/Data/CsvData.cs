// =====================================================================
// CsvData.cs
// CSV 파싱 결과를 담는 자료구조
// CsvReader 가 파싱해서 이 구조로 반환하고,
// 자동생성된 Parser 가 이 구조를 읽어 SheetData 로 변환한다
// =====================================================================

using System.Collections.Generic;

// 시트 한 장 전체 데이터
public class CsvTable
{
    // 컬럼 이름 목록 (헤더 행 기준)
    public List<string> Headers { get; } = new List<string>();

    // 데이터 행 목록 (헤더 제외)
    public List<CsvRow> Rows { get; } = new List<CsvRow>();

    // 컬럼 이름으로 인덱스를 빠르게 찾기 위한 캐시
    private Dictionary<string, int> _headerIndex;

    // 헤더 인덱스 캐시 빌드 (최초 조회 시 1회 실행)
    public int GetColumnIndex(string columnName)
    {
        if (_headerIndex == null)
        {
            _headerIndex = new Dictionary<string, int>();
            for (int i = 0; i < Headers.Count; i++)
                _headerIndex[Headers[i]] = i;
        }

        if (_headerIndex.TryGetValue(columnName, out int idx))
            return idx;

        // 컬럼이 없으면 -1 반환 — Validator 가 이 상황을 감지한다
        return -1;
    }
}

// 데이터 한 행
public class CsvRow
{
    // 셀 값 배열 (컬럼 순서와 동일)
    public string[] Values { get; }

    public CsvRow(string[] values)
    {
        Values = values;
    }

    // 인덱스로 셀 값 읽기
    public string Get(int index)
    {
        if (index < 0 || index >= Values.Length)
            return string.Empty;
        return Values[index].Trim();
    }

    // 컬럼 이름으로 셀 값 읽기 (테이블 참조 필요)
    public string Get(CsvTable table, string columnName)
    {
        return Get(table.GetColumnIndex(columnName));
    }
}