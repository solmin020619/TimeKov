// =====================================================================
// InventoryManager.cs
// 임시 스텁  실제 인벤토리 로직은 추후 이 파일에 구현
// DroppedItem / MachineUI / RecipeDropSlot / TestItemSpawner 에서
// 참조하는 메서드와 타입을 미리 선언해 컴파일 에러를 방지한다
// =====================================================================

using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    // 인벤토리 소유자 유형  DroppedItem 에서 플레이어 인벤토리 구분에 사용
    public enum InventoryOwnerType { Player, Machine, Storage }

    // 이 인벤토리의 소유자 유형
    public InventoryOwnerType ownerType = InventoryOwnerType.Player;

    // 싱글톤 인스턴스
    public static InventoryManager Instance { get; private set; }

    private void Awake() { Instance = this; }

    // 인벤토리 슬롯 UI 생성
    public void CreateSlots() { }

    // itemId 에 해당하는 아이템 총 보유 수량 반환
    public int GetTotalItemCount(int itemId) => 0;

    // 인벤토리에 아이템 추가
    public void AddItem(int itemId, int amount) { }

    // 루팅으로 아이템 획득 시도  인벤토리 가득 찼으면 남은 수량 반환, 전부 들어가면 0
    public int TryAddItemFromLoot(int itemId, int count) => 0;

    // 아이템 소비 시도. 수량 부족 시 false 반환
    public bool TryConsumeItem(int itemId, int amount) => false;

    // 인벤토리 UI 슬롯 전체 강제 갱신
    public void ForceRefreshUI() { }
}