// 자동 생성 파일 — 직접 수정 금지 (메뉴 '시트 > 코드 다시 만들기' 로 재생성)

using System;

[Serializable]
public class ItemDataSheetData
{
    // 이 데이터의 키 (Dictionary 조회용)
    public ItemDataSheetId SheetId;

    public string itemName;
    // Enum: ItemGrade
    public ItemGrade itemGrade;
    // Enum: ItemCategory
    public ItemCategory itemCategory;
    public string iconKey;
    public int maxStack;
}
