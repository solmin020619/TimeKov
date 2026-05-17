// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System;

[Serializable]
public class CoreLevelDataSheetData
{
    // 이 데이터의 키 (Dictionary 조회용)
    public CoreLevelDataSheetId SheetId;

    public int maxTime;
    public int stamina;
    public int atk;
    public int def;
    // FK → ItemData
    public ItemDataSheetId requiredKitItemId;
}
