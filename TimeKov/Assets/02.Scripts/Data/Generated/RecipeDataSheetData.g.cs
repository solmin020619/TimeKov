// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System;

[Serializable]
public class RecipeDataSheetData
{
    // 이 데이터의 키 (Dictionary 조회용)
    public RecipeDataSheetId SheetId;

    // FK → FacilityData
    public FacilityDataSheetId facilityId;
    // FK → ItemData
    public ItemDataSheetId outputItemId;
    public int outputCount;
    public float craftTime;
}
