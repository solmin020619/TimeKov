// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System;

public readonly struct CoreLevelDataSheetId : IEquatable<CoreLevelDataSheetId>
{
    // 딕셔너리 키로 사용되는 문자열 값
    private readonly string _value;

    public CoreLevelDataSheetId(string value)
    {
        _value = value;
    }

    // string 으로 implicit 변환 (읽기 편의)
    public static implicit operator string(CoreLevelDataSheetId id) => id._value;

    // string 에서 explicit 변환 (실수 방지)
    public static explicit operator CoreLevelDataSheetId(string value) => new CoreLevelDataSheetId(value);
    public bool Equals(CoreLevelDataSheetId other) => _value == other._value;
    public override bool Equals(object obj) =>
        obj is CoreLevelDataSheetId other && Equals(other);
    public override int GetHashCode() => _value?.GetHashCode() ?? 0;
    public override string ToString() => _value;
}
