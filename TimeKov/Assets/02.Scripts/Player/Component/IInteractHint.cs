/// <summary>
/// IInteractable 오브젝트가 F키 힌트 레이블을 제공할 때 추가로 구현.
/// PlayerInteractComponent가 가장 가까운 오브젝트의 레이블을 HUD에 표시한다.
/// </summary>
public interface IInteractHint
{
    string HintLabel { get; }
}
