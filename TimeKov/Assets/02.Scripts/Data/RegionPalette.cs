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
    /// 몬스터의 드롭 출처 ID(EnemyDropOnDeath.SourceId)로 서식 지역을 알아낸다.
    ///
    /// ★이름 규칙이 유일한 단서다 - 몬스터 시트에 지역 컬럼이 없다.
    ///   접미사가 붙는 것들(_Snow/_Desert/_Lava)은 규칙으로 잡히지만,
    ///   본드래곤·자이언트웜·자폭거미는 접미사가 없어서 여기에 직접 적어 둔다.
    ///   (필드 몬스터 목록 = Enemy/FieldMonster/Editor/FieldMonsterBuilderWindow.cs.
    ///    자폭거미는 그 목록에 없는 별도 몹이라 배치 지역을 코드에서 알 방법이 없다 - 종욱 확인: 사막.)
    ///
    ///   못 알아보면 자연으로 본다. 새로 추가한 몬스터가 자연색(초록)으로 뜨면
    ///   이 판정에 빠졌다는 신호이니 여기에 추가하면 된다.
    /// </summary>
    public static TransmissionRegion OfMonster(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return TransmissionRegion.Nature;
        string s = sourceId.Trim().ToLowerInvariant();

        if (s.Contains("_snow") || s.Contains("_frost") || s.Contains("ice_elemental") || s.Contains("bonedragon"))
            return TransmissionRegion.Snow;

        if (s.Contains("_desert") || s.Contains("sand_elemental") || s.Contains("giantworm")
            || s.Contains("suicide_spider"))
            return TransmissionRegion.Desert;

        // 용암 몬스터는 hell_ 접두사를 쓴다(hell_hound / hell_bat / ...).
        if (s.Contains("_lava") || s.StartsWith("hell_") || s.Contains("fire_boss"))
            return TransmissionRegion.Lava;

        return TransmissionRegion.Nature;
    }

    private static Color Hex(string hex)
        => ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.white;
}
