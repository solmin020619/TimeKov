// InventorySlot.cs
// 인벤토리 슬롯 하나의 데이터를 담는 클래스
// InventoryManager가 List<InventorySlot>으로 들고 있음

using System;

[Serializable]
public class InventorySlot
{
    // 아이템 ID (-1이면 빈 슬롯)
    public int itemId = -1;

    // 보유 수량
    public int amount = 0;

    // 슬롯 인덱스 (리스트 내 위치, InventoryManager에서 자동 부여)
    public int slotIndex = -1;

    // 새로 획득한 아이템인지 여부 (NEW 뱃지 표시용)
    public bool isNew = false;

    // 빈 슬롯인지 확인하는 프로퍼티
    public bool IsEmpty => itemId < 0 || amount <= 0;

    // 슬롯 데이터 설정
    public void Set(int id, int qty, bool markNew = false)
    {
        itemId = id;
        amount = qty;
        isNew = markNew;
    }

    // 슬롯 초기화 (비우기)
    public void Clear()
    {
        itemId = -1;
        amount = 0;
        isNew = false;
    }
}