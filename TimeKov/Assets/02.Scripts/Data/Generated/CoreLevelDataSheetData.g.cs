// 자동 생성 파일 — 직접 수정 금지 (메뉴 '시트 > 코드 다시 만들기' 로 재생성)

using System;

[Serializable]
public class CoreLevelDataSheetData
{
    // 이 데이터의 키 (Dictionary 조회용)
    public CoreLevelDataSheetId SheetId;

    public int maxTime;
    // FK → ItemData
    public ItemDataSheetId requiredKitItemId;
    public int requiredAmount;
    public float successRate;
}
