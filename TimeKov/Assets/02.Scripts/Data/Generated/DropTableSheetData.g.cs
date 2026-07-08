// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System;

[Serializable]
public class DropTableSheetData
{
    // 이 데이터의 키 (Dictionary 조회용)
    public DropTableSheetId SheetId;

    // Enum: SourceType
    public SourceType sourceType;
    public string sourceId;
    public int dropChance;
    public string countDist;
}
