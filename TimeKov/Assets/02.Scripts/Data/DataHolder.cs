// =====================================================================
// DataHolder.cs
// Dictionary<string, T> 래퍼
// 키로 빠르게 조회하고, 전체 순회도 지원한다
// =====================================================================

using System.Collections.Generic;

public class DataHolder<T>
{
    private Dictionary<string, T> _data = new Dictionary<string, T>();

    // 키로 데이터 조회 — 없으면 예외 발생
    public T Get(string key) => _data[key];

    // 키로 데이터 조회 — 없으면 false 반환
    public bool TryGet(string key, out T value) => _data.TryGetValue(key, out value);

    // 전체 데이터 순회용
    public IEnumerable<T> All => _data.Values;

    // 키 존재 여부
    public bool Contains(string key) => _data.ContainsKey(key);

    // 총 데이터 수
    public int Count => _data.Count;

    // 데이터 추가 (Parser.g.cs 에서 호출)
    public void Add(string key, T value) => _data[key] = value;

    // 전체 초기화
    public void Clear() => _data.Clear();
}