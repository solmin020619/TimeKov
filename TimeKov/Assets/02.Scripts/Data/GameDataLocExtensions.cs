// 게임 데이터 이름 필드의 현지화 헬퍼
// UI에서 .itemName / .facilityName 대신 .GetLocalizedName() 사용
public static class GameDataLocExtensions
{
    public static string GetLocalizedName(this ItemDataSheetData data)
        => data != null ? Loc.Get(data.itemName) : "";

    public static string GetLocalizedName(this FacilityDataSheetData data)
        => data != null ? Loc.Get(data.facilityName) : "";
}
