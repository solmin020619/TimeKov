// 자동 생성 파일 — 직접 수정 금지 (메뉴 '시트 > 코드 다시 만들기' 로 재생성)

using System;

public readonly struct FacilityDataSheetId : IEquatable<FacilityDataSheetId>
{
    // 딕셔너리 키로 사용되는 문자열 값
    private readonly string _value;

    public FacilityDataSheetId(string value)
    {
        _value = value;
    }

    // string 으로 implicit 변환 (읽기 편의)
    public static implicit operator string(FacilityDataSheetId id) => id._value;

    // string 에서 explicit 변환 (실수 방지)
    public static explicit operator FacilityDataSheetId(string value) => new FacilityDataSheetId(value);
    public bool Equals(FacilityDataSheetId other) => _value == other._value;
    public override bool Equals(object obj) =>
        obj is FacilityDataSheetId other && Equals(other);
    public override int GetHashCode() => _value?.GetHashCode() ?? 0;
    public override string ToString() => _value;
}
