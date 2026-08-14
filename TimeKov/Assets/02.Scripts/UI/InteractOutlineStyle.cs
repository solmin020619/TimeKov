using UnityEngine;

/// <summary>
/// 이 오브젝트(하위 포함)의 상호작용 외곽선 모드를 따로 지정한다.
///
/// 기본값은 <see cref="Outline.Mode.OutlineAll"/> — 풀·나무 뒤에서도 항상 보이라고 '가림 무시'로 그린다.
/// 그런데 땅에 박혀 있는 큰 오브젝트(추락한 우주선 등)는 그 때문에 <b>지면 아래 묻힌 부분까지</b>
/// 외곽선이 다 비쳐서 이상하게 보인다.
///
/// 그런 오브젝트에 이 컴포넌트를 붙이고 <see cref="Outline.Mode.OutlineVisible"/> 로 두면
/// 깊이 테스트(ZTest LessEqual)에 걸려 <b>지형에 가려진 부분은 자동으로 안 그려진다</b>.
/// 평면 하나로 잘라내는 방식과 달리 경사면·복잡한 지형도 그대로 따라간다.
///
/// ★붙이는 위치: 외곽선이 생기는 렌더러들의 '부모(루트)'.
///   외곽선은 InteractHintPanel 이 자식 렌더러마다 만들기 때문에, 루트에 하나만 붙이면 전부 적용된다.
/// </summary>
[DisallowMultipleComponent]
public class InteractOutlineStyle : MonoBehaviour
{
    [Tooltip("해제: 이 오브젝트(하위 포함)에는 외곽선을 아예 만들지 않는다.\n" +
             "땅에 박힌 파편 덩어리처럼 외곽선이 오히려 지저분해지는 오브젝트용 —\n" +
             "상호작용 표시는 별도 오브젝트나 F 프롬프트로 준다.")]
    public bool showOutline = true;

    [Tooltip("OutlineAll = 가림 무시(기본). OutlineVisible = 지형에 가려지면 안 그림(땅에 박힌 오브젝트용).")]
    public Outline.Mode mode = Outline.Mode.OutlineVisible;
}
