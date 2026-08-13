// =====================================================================
// RegionPalette.cs
// 지역(자연/설원/사막/용암)의 대표 색과 몬스터 서식지 판정을 모아 둔 곳.
//
// [왜 한 곳에 모으나]
//   전송 단말의 게이지 구간, 도감 몬스터 액자처럼 "지역을 색으로 보여주는" UI 가 여러 곳이다.
//   각자 색을 들고 있으면 한쪽만 바뀌어 같은 지역이 다른 색으로 보인다.
//
// 색 순서는 TransmissionRegion 열거형 순서(자연 -> 설원 -> 사막 -> 용암) 그대로다.
// =====================================================================

using UnityEngine;

public static class RegionPalette
{
    // 자연=초록 / 설원=하늘 / 사막=황토 / 용암=주홍. 각 테마 맵의 분위기 색.
    // ★배열을 직접 고치지 마라. 색을 바꾸려면 이 값을 바꾼다.
    public static readonly Color[] Colors =
    {
        Hex("43B06C"),   // 자연
        Hex("5BC7E8"),   // 설원
        Hex("D9A44A"),   // 사막
        Hex("E0593A"),   // 용암
    };

    public static readonly string[] KoreanNames = { "자연", "설원", "사막", "용암" };

    public static Color Of(TransmissionRegion region) => Of((int)region);

    public static Color Of(int index) => Colors[Mathf.Clamp(index, 0, Colors.Length - 1)];

    public static string KoreanName(TransmissionRegion region)
        => KoreanNames[Mathf.Clamp((int)region, 0, KoreanNames.Length - 1)];

    /// <summary>
    /// 몬스터의 서식 지역. **몬스터 스탯 시트의 region 컬럼이 권위다.**
    ///
    /// statId = SO 에셋 파일명(EnemyData_Werewolf 등) = 시트의 키.
    /// 시트에 없으면 이름 규칙으로 폴백한다 - 마이그레이션 중이거나 시트를 못 받았을 때를 위한 것이고,
    /// 평상시엔 안 쓰인다. 새 몬스터는 이름 규칙에 기대지 말고 시트에 지역을 적어라.
    /// </summary>
    /// <param name="statId">SO 에셋 이름. 시트 조회 키.</param>
    /// <param name="sourceIdFallback">드롭 출처 ID. statId 를 모를 때 이름 규칙에 쓸 보조 단서.</param>
    public static TransmissionRegion OfMonster(string statId, string sourceIdFallback = null)
    {
        if (TryFromSheet(statId, out var fromSheet)) return fromSheet;
        return FromNameRules(!string.IsNullOrEmpty(statId) ? statId : sourceIdFallback)
            ?? FromNameRules(sourceIdFallback)
            ?? TransmissionRegion.Nature;
    }

    /// 시트의 region 컬럼 조회. 값이 비어 있거나 못 알아보는 글자면 false.
    public static bool TryFromSheet(string statId, out TransmissionRegion region)
    {
        region = TransmissionRegion.Nature;
        if (string.IsNullOrEmpty(statId)) return false;

        var table = GameDataHolder.I != null ? GameDataHolder.I.MonsterStatData : null;
        if (table == null || !table.TryGet(statId, out var row) || row == null) return false;

        string v = row.region != null ? row.region.Trim() : "";
        if (v.Length == 0) return false;

        for (int i = 0; i < KoreanNames.Length; i++)
            if (KoreanNames[i] == v) { region = (TransmissionRegion)i; return true; }

        // 영문 열거형 이름으로 적어도 받아 준다(Nature / Snow / Desert / Lava).
        return System.Enum.TryParse(v, true, out region);
    }

    /// 폴백: 이름에 지역이 드러나는 몬스터만 잡는다. 못 알아보면 null.
    /// (본드래곤·자이언트웜·자폭거미처럼 이름에 지역이 없는 몹은 여기서 안 잡힌다 - 그래서 시트가 필요했다.)
    private static TransmissionRegion? FromNameRules(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        string s = id.Trim().ToLowerInvariant();

        if (s.Contains("_snow") || s.Contains("_frost") || s.Contains("ice_elemental"))
            return TransmissionRegion.Snow;
        if (s.Contains("_desert") || s.Contains("sand_elemental"))
            return TransmissionRegion.Desert;
        // 용암 몬스터는 hell_ 접두사를 쓴다(hell_hound / hell_bat / ...).
        if (s.Contains("_lava") || s.Contains("hell") || s.Contains("fire_boss"))
            return TransmissionRegion.Lava;
        return null;
    }

    private static Color Hex(string hex)
        => ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.white;
}
