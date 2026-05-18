// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System;

[Serializable]
public class FacilityDataSheetData
{
    // 이 데이터의 키 (Dictionary 조회용)
    public FacilityDataSheetId SheetId;

    public string facilityName;
    // Enum: FacilityType
    public FacilityType facilityType;
    public int gridW;
    public int gridH;
    public int inputSlotCount;
    public int outputSlotCount;
    public bool requiresPower;
    public bool canRotate;
    public int maxLevel;
}
