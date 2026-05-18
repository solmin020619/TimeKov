// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System;

public readonly struct RecipeInputDataSheetId : IEquatable<RecipeInputDataSheetId>
{
    // 딕셔너리 키로 사용되는 문자열 값
    private readonly string _value;

    public RecipeInputDataSheetId(RecipeDataSheetId recipeId, ItemDataSheetId inputItemId)
    {
        _value = string.Join("_", new object[] { recipeId, inputItemId });
    }

    // 이미 조합된 문자열로 직접 생성 (explicit cast 전용)
    private RecipeInputDataSheetId(string rawValue, bool direct) { _value = rawValue; }

    // string 으로 implicit 변환 (읽기 편의)
    public static implicit operator string(RecipeInputDataSheetId id) => id._value;

    // string 에서 explicit 변환 (실수 방지)
    public static explicit operator RecipeInputDataSheetId(string value) => new RecipeInputDataSheetId(value, true);
    public bool Equals(RecipeInputDataSheetId other) => _value == other._value;
    public override bool Equals(object obj) =>
        obj is RecipeInputDataSheetId other && Equals(other);
    public override int GetHashCode() => _value?.GetHashCode() ?? 0;
    public override string ToString() => _value;
}
