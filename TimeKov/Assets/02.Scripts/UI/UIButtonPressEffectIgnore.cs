// =====================================================================
// UIButtonPressEffectIgnore.cs
// 이 버튼에는 눌림 축소 연출을 붙이지 말라는 표시.
//
// 쓰는 곳: 자기 스케일을 직접 굴리는 버튼(회전/맥동 연출 등), 또는 눌림감이
//          오히려 방해가 되는 곳(드래그 손잡이처럼 누른 채로 오래 끄는 것).
// 붙이는 법: 해당 Button 오브젝트에 이 컴포넌트만 추가하면 된다.
//            (TextAutoFitIgnore 와 같은 방식)
// =====================================================================

using UnityEngine;

[DisallowMultipleComponent]
public class UIButtonPressEffectIgnore : MonoBehaviour
{
}
