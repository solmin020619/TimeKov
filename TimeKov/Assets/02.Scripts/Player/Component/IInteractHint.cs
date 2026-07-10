/// <summary>
/// 플레이어가 접근하면 World Space [F] 힌트를 표시/숨기는 인터랙션 오브젝트.
/// PlayerInteractComponent가 가장 가까운 오브젝트에 ShowHint(true), 멀어지면 ShowHint(false) 호출.
/// </summary>
public interface IInteractHint
{
    void ShowHint(bool show);
}
