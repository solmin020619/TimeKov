// 자동 생성 파일 — 직접 수정 금지 (메뉴 '시트 > 코드 다시 만들기' 로 재생성)

using System;

public readonly struct DropTableSheetId : IEquatable<DropTableSheetId>
{
    // 딕셔너리 키로 사용되는 문자열 값
    private readonly string _value;

    public DropTableSheetId(int dropId, ItemDataSheetId itemId)
    {
        _value = string.Join("_", new object[] { dropId, itemId });
    }

    // 이미 조합된 문자열로 직접 생성 (explicit cast 전용)
    private DropTableSheetId(string rawValue, bool direct) { _value = rawValue; }

    // string 으로 implicit 변환 (읽기 편의)
    public static implicit operator string(DropTableSheetId id) => id._value;

    // string 에서 explicit 변환 (실수 방지)
    public static explicit operator DropTableSheetId(string value) => new DropTableSheetId(value, true);
    public bool Equals(DropTableSheetId other) => _value == other._value;
    public override bool Equals(object obj) =>
        obj is DropTableSheetId other && Equals(other);
    public override int GetHashCode() => _value?.GetHashCode() ?? 0;
    public override string ToString() => _value;
}
