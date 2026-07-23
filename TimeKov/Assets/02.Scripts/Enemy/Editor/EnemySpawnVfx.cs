// 몬스터 스폰 VFX(맵 테마별) 경로 + sourceId 기준 자동 배정.
// 프리팹 원본: Assets/18.외부에셋/Eric VFX Studio/몬스터스폰VFX/Prefabs/ (종욱이 통일 네이밍).
// 매핑:
//   자연맵 일반몹        -> 자연스폰
//   설산맵 일반몹        -> 설산스폰
//   용암/사막맵 일반몹   -> 용암사막스폰(공용)
//   엘리트(자연 거미여왕 / 늑대인간 / 본드래곤) -> 엘리트몹스폰
//   보스 4종            -> 스폰 VFX 없음(빌더에서 배정 안 함)
public static class EnemySpawnVfx
{
    const string Dir = "Assets/18.외부에셋/Eric VFX Studio/몬스터스폰VFX/Prefabs/";
    public const string Nature     = Dir + "자연스폰.prefab";
    public const string Snow       = Dir + "설산스폰.prefab";
    public const string LavaDesert = Dir + "용암사막스폰.prefab";
    public const string Elite      = Dir + "엘리트몹스폰.prefab";

    // sourceId 키워드로 테마 스폰 VFX 경로를 고른다.
    // ★엘리트(자연 거미여왕 / 본드래곤)를 테마 판정보다 먼저(자연 거미여왕은 _snow/_lava 등 없어 else 로 새지 않게).
    public static string ForSourceId(string sourceId)
    {
        string s = (sourceId ?? "").ToLowerInvariant();
        if (s.Contains("spiderqueen_nature") || s.Contains("bonedragon")) return Elite;
        if (s.Contains("_snow") || s.Contains("_frost")) return Snow;
        if (s.Contains("_desert") || s.Contains("_lava") || s.Contains("worm")) return LavaDesert;
        return Nature;
    }
}
