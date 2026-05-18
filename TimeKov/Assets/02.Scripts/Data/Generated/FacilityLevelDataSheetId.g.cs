// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System;

public readonly struct FacilityLevelDataSheetId : IEquatable<FacilityLevelDataSheetId>
{
    // 딕셔너리 키로 사용되는 문자열 값
    private readonly string _value;

    public FacilityLevelDataSheetId(FacilityDataSheetId facilityId, int level)
    {
        _value = string.Join("_", new object[] { facilityId, level });
    }

    // 이미 조합된 문자열로 직접 생성 (explicit cast 전용)
    private FacilityLevelDataSheetId(string rawValue, bool direct) { _value = rawValue; }

    // string 으로 implicit 변환 (읽기 편의)
    public static implicit operator string(FacilityLevelDataSheetId id) => id._value;

    // string 에서 explicit 변환 (실수 방지)
    public static explicit operator FacilityLevelDataSheetId(string value) => new FacilityLevelDataSheetId(value, true);
    public bool Equals(FacilityLevelDataSheetId other) => _value == other._value;
    public override bool Equals(object obj) =>
        obj is FacilityLevelDataSheetId other && Equals(other);
    public override int GetHashCode() => _value?.GetHashCode() ?? 0;
    public override string ToString() => _value;
}
