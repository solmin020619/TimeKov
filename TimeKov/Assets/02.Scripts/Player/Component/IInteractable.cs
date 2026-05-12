public interface IInteractable
{
    // 상호작용 가능 여부
    bool CanInteract { get; }

    // F키 눌렀을 때 호출
    void Interact(Player player);
}